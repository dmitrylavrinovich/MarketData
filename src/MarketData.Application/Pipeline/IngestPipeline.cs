using System.Threading.Channels;
using MarketData.Application.Configuration;
using MarketData.Domain.Entities;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Pipeline;

/// <summary>
/// Владеет bounded-каналом между продьюсерами (WS-клиенты) и консьюмером (batch writer).
/// Канал даёт развязку producer/consumer и backpressure (<see cref="BoundedChannelFullMode.Wait"/>).
/// Продьюсеры пишут в <see cref="Writer"/>, консьюмер читает из <see cref="Reader"/>.
/// </summary>
public sealed class IngestPipeline
{
    private readonly Channel<Tick> _channel;

    public IngestPipeline(IOptions<PipelineOptions> options)
    {
        var opts = options.Value;
        _channel = Channel.CreateBounded<Tick>(new BoundedChannelOptions(opts.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Точка записи для продьюсеров (по одному WS-клиенту на источник).</summary>
    public ChannelWriter<Tick> Writer => _channel.Writer;

    /// <summary>Точка чтения для консьюмера (единственный batch writer).</summary>
    public ChannelReader<Tick> Reader => _channel.Reader;
}
