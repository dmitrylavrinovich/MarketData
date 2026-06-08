using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence;

public sealed class MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : DbContext(options)
{
    public DbSet<TickEntity> Ticks => Set<TickEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tick = modelBuilder.Entity<TickEntity>();
        tick.ToTable("ticks");

        // Композитный ключ = дедуп-набор. Включает ts (требование hypertable) и гарантирует уникальность.
        tick.HasKey(t => new { t.Exchange, t.Ticker, t.Timestamp, t.Price, t.Volume });

        tick.Property(t => t.Exchange).HasColumnName("exchange");
        tick.Property(t => t.Ticker).HasColumnName("ticker");
        tick.Property(t => t.Price).HasColumnName("price").HasColumnType("numeric(20,8)");
        tick.Property(t => t.Volume).HasColumnName("volume").HasColumnType("numeric(20,8)");
        tick.Property(t => t.Timestamp).HasColumnName("ts").HasColumnType("timestamptz");
        tick.Property(t => t.IngestedAt).HasColumnName("ingested_at").HasColumnType("timestamptz");

        // Запросы последних тиков по инструменту.
        tick.HasIndex(t => new { t.Ticker, t.Timestamp }).HasDatabaseName("ix_ticks_ticker_ts");
    }
}
