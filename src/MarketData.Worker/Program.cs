using MarketData.Application;
using MarketData.Application.Configuration;
using MarketData.Infrastructure;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, cfg) => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

builder.Services
    .AddOptions<PipelineOptions>()
    .Bind(builder.Configuration.GetSection(PipelineOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<DedupOptions>()
    .Bind(builder.Configuration.GetSection(DedupOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<ReconnectOptions>()
    .Bind(builder.Configuration.GetSection(ReconnectOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
builder.Services.AddPersistence(connectionString);

// Источники из конфига — добавление биржи = строка в appsettings, без перекомпиляции.
var exchanges = builder.Configuration
    .GetSection(ExchangeOptions.SectionName)
    .Get<List<ExchangeOptions>>() ?? [];
builder.Services.AddExchangeIngestion(exchanges);

var host = builder.Build();
host.Run();
