using MarketData.MockExchange.Configuration;
using MarketData.MockExchange.Formats;
using MarketData.MockExchange.Generation;
using MarketData.MockExchange.Streaming;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MockExchangeOptions>(
    builder.Configuration.GetSection(MockExchangeOptions.SectionName));

builder.Services.AddTransient<ITickGenerator, RandomWalkTickGenerator>();
builder.Services.AddTransient<WebSocketTickStream>();

var app = builder.Build();
app.UseWebSockets();

app.MapGet("/", () => Results.Ok(new
{
    service = "MarketData.MockExchange",
    streams = new[]
    {
        new { exchange = "ExchangeA", format = "JSON snake_case", path = "/ws/exchange-a" },
        new { exchange = "ExchangeB", format = "JSON nested", path = "/ws/exchange-b" },
        new { exchange = "ExchangeC", format = "CSV-like", path = "/ws/exchange-c" },
    },
    hint = "Connect a WebSocket client; optional ?rate=<ticks/sec> overrides the configured rate.",
}));

app.Map("/ws/exchange-a", StreamEndpoint(new JsonSnakeFormatter()));
app.Map("/ws/exchange-b", StreamEndpoint(new JsonNestedFormatter()));
app.Map("/ws/exchange-c", StreamEndpoint(new CsvFormatter()));

app.Run();

static RequestDelegate StreamEndpoint(ITickFormatter formatter) => async context =>
{
    var options = context.RequestServices.GetRequiredService<IOptions<MockExchangeOptions>>().Value;
    var stream = context.RequestServices.GetRequiredService<WebSocketTickStream>();

    var rate = options.TicksPerSecondPerStream;
    if (int.TryParse(context.Request.Query["rate"], out var overrideRate) && overrideRate > 0)
        rate = overrideRate;

    await stream.RunAsync(context, formatter, rate, context.RequestAborted);
};
