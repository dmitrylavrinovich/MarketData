namespace MarketData.Domain.Exceptions;

/// <summary>
/// Тик нарушает доменный инвариант (пустой тикер/биржа, неположительная цена,
/// отрицательный объём). Бросается при конструировании <see cref="Entities.Tick"/>.
/// </summary>
public sealed class InvalidTickException : DomainException
{
    public InvalidTickException(string message) : base(message) { }
}
