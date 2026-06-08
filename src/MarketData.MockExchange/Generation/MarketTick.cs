namespace MarketData.MockExchange.Generation;

/// <summary>
/// Внутренняя модель тика mock-биржи (канонический символ "BASE-QUOTE").
/// Форматтеры приводят её к конвенции конкретной биржи.
/// </summary>
public sealed record MarketTick(
    string Symbol,
    decimal Price,
    decimal Volume,
    DateTimeOffset Timestamp);
