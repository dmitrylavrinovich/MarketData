using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketData.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticks",
                columns: table => new
                {
                    exchange = table.Column<string>(type: "text", nullable: false),
                    ticker = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    volume = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    ts = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticks", x => new { x.exchange, x.ticker, x.ts, x.price, x.volume });
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticks_ticker_ts",
                table: "ticks",
                columns: new[] { "ticker", "ts" });

            // Timescale hypertable, если расширение доступно; иначе остаётся обычная таблица (§6 fallback).
            // PK включает ts — обязательное условие hypertable для UNIQUE-ключа.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'timescaledb') THEN
                        CREATE EXTENSION IF NOT EXISTS timescaledb;
                        PERFORM create_hypertable('ticks', 'ts',
                            chunk_time_interval => INTERVAL '1 day',
                            if_not_exists => TRUE);
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticks");
        }
    }
}
