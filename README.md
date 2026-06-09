# MarketData

Система агрегации биржевых данных: несколько WebSocket-источников → нормализация → in-process channel → дедуп → batch-запись в PostgreSQL/TimescaleDB.

Стек: **.NET 8**, **PostgreSQL 16 + TimescaleDB**, **Serilog**, **EF Core**, **xUnit + Testcontainers**.

## Быстрый старт (Docker)

Требования: [Docker Desktop](https://www.docker.com/products/docker-desktop/) (или Docker Engine + Compose v2).

```bash
docker compose up --build
```

Порядок старта:

1. `postgres` — TimescaleDB, healthcheck
2. `migrate` — one-shot `dotnet ef database update` (схема + hypertable)
3. `aggregator` (Worker) + `mock-exchange` — параллельно после успешной миграции

Проверка:

```bash
# логи Worker — connect к трём биржам и metrics-репорт
docker compose logs -f aggregator

# тики в БД
docker compose exec postgres psql -U postgres -d marketdata -c "select count(*) from ticks;"
docker compose exec postgres psql -U postgres -d marketdata \
  -c "select exchange, count(*) from ticks group by exchange order by exchange;"
```

MockExchange с хоста: `ws://localhost:5095/ws/exchange-a?rate=10`

Остановка: `docker compose down` (данные Postgres в volume по умолчанию не сохраняются — при `down` контейнер удаляется).

### Почему migrate — отдельный init-сервис, а не `Database.Migrate()` в Worker

- Миграция выполняется **один раз** до старта Worker — нет гонки при нескольких репликах aggregator.
- Worker в runtime-образе **без SDK** (~200 MB aspnet vs ~800 MB sdk).
- DDL-права нужны только контейнеру `migrate`, не ingestion-процессу.

Multi-stage `Dockerfile` Worker: `target: migrate` (SDK + `dotnet ef`) и `target: runtime` (slim).

## Локальный запуск (без Docker)

```bash
# 1. Postgres (пример)
docker run -d --name md-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=marketdata -p 5432:5432 postgres:16-alpine

# 2. Миграции
$env:MARKETDATA_POSTGRES = "Host=127.0.0.1;Database=marketdata;Username=postgres;Password=postgres"
dotnet tool restore
dotnet ef database update --project src/MarketData.Infrastructure --startup-project src/MarketData.Infrastructure

# 3. MockExchange (терминал 1)
dotnet run --project src/MarketData.MockExchange

# 4. Worker (терминал 2)
dotnet run --project src/MarketData.Worker
```

## Тесты

```bash
dotnet test
```

- **Unit** (`tests/MarketData.UnitTests`) — парсеры, дедуп, батчинг, backoff.
- **Integration** (`tests/MarketData.IntegrationTests`) — Testcontainers поднимает Postgres **автоматически**; нужен запущенный Docker. Первый прогон ~2 мин (pull образа Timescale).

## Архитектура

```
MockExchange (3 WS формата)
    ExchangeA ──┐
    ExchangeB ──┼──► parse/normalize ──► Channel<Tick> ──► dedup ──► batch ──► ITickSink ──► PostgreSQL
    ExchangeC ──┘         (producers)      (bounded)      (consumer)
```

Clean Architecture:

| Проект | Роль |
|--------|------|
| `MarketData.Domain` | `Tick`, `DedupKey`, инварианты |
| `MarketData.Application` | Порты, channel, consumer, options |
| `MarketData.Infrastructure` | WS-клиенты, парсеры, EF, dedup |
| `MarketData.Worker` | Composition root, host |
| `MarketData.MockExchange` | Mock WS-сервер (изолирован) |

Зависимости: `Worker → Infrastructure → Application → Domain`.

### Ключевые решения

**Channel, а не Kafka/RabbitMQ.** 50–100 тик/сек в одном процессе — bounded `System.Threading.Channels` даёт producer/consumer развязку и backpressure без сетевого оверхеда и at-least-once дублей. Брокер оправдан при split на микросервисы или 50k+ тик/сек.

**EF Core, не COPY.** `EfCoreTickSink` с `INSERT ... ON CONFLICT DO NOTHING` достаточен для нагрузки ТЗ. `NpgsqlCopyTickSink` — stub; переключение в `DependencyInjection.AddPersistence()` закомментировано для масштабирования.

**Один consumer канала.** Батчинг и один writer в БД эффективнее при текущей нагрузке; параллелизм — на стороне WS-producer'ов (по `BackgroundService` на источник).

**In-memory dedup + UNIQUE в БД.** Быстрый фильтр в процессе + composite PK `(exchange, ticker, ts, price, volume)` как финальная гарантия при рестарте.

**Reconnect без Polly.** `IAsyncEnumerable` поток сообщений; exponential backoff + jitter в `ReconnectBackoff` + idle-watchdog.

## Конфигурация

Источники в `appsettings.json` → секция `Exchanges`. Добавить биржу = строка в конфиг, без перекомпиляции.

В Docker: `appsettings.Docker.json` загружается при `DOTNET_ENVIRONMENT=Docker` (compose задаёт оба `DOTNET_*` и `ASPNETCORE_*`) — хосты `mock-exchange` и `postgres` внутри compose-сети.

## Структура репозитория

```
src/
  MarketData.Domain
  MarketData.Application
  MarketData.Infrastructure
  MarketData.Worker          ← entrypoint + Dockerfile (build/migrate/runtime)
  MarketData.MockExchange    ← mock WS + Dockerfile
tests/
  MarketData.UnitTests
  MarketData.IntegrationTests
docker-compose.yml
.config/dotnet-tools.json    ← локальный dotnet-ef
```
