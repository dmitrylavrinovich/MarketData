namespace MarketData.MockExchange.Generation;

/// <summary>Источник синтетических тиков. Экземпляр не потокобезопасен — один на подключение.</summary>
public interface ITickGenerator
{
    MarketTick Next();
}
