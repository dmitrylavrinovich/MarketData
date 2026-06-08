namespace MarketData.Infrastructure.Exchange;

/// <summary>Параметры подключения к одному источнику: имя биржи и WebSocket-адрес.</summary>
public sealed record ExchangeConnection(string Name, string Url);
