using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Szamla.Application.Common.Interfaces;
using Szamla.Infrastructure.Persistence;

namespace Szamla.Infrastructure.Invoicing;

/// <summary>
/// Allocates numbers via a single atomic PostgreSQL UPSERT (INSERT ... ON CONFLICT DO UPDATE
/// ... RETURNING) rather than a SELECT-then-UPDATE plus an explicit lock: Postgres guarantees
/// the whole statement is atomic per row on its own, so two concurrent callers for the same
/// (tenant, series, year) can never read the same LastNumber and each get a distinct, gapless
/// increment — the one that arrives second simply waits for the first row-level lock to release
/// and then applies its own +1 on top.
/// </summary>
public sealed class InvoiceNumberGenerator(ApplicationDbContext dbContext) : IInvoiceNumberGenerator
{
    public async Task<long> NextAsync(Guid tenantId, Guid seriesId, int year, CancellationToken cancellationToken = default)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "InvoiceNumberCounters" ("TenantId", "SeriesId", "Year", "LastNumber")
            VALUES ($1, $2, $3, 1)
            ON CONFLICT ("TenantId", "SeriesId", "Year")
            DO UPDATE SET "LastNumber" = "InvoiceNumberCounters"."LastNumber" + 1
            RETURNING "LastNumber";
            """;
        command.Parameters.Add(new NpgsqlParameter { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter { Value = seriesId });
        command.Parameters.Add(new NpgsqlParameter { Value = year });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (long)result!;
    }
}
