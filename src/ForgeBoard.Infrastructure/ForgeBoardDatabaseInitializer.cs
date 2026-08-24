using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ForgeBoard.Infrastructure;

public static class ForgeBoardDatabaseInitializer
{
    public static async Task EnsureCreatedAndUpgradedAsync(ForgeBoardDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        if (!dbContext.Database.IsSqlite()) return;

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            var columns = new HashSet<string>(StringComparer.Ordinal);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info('Missions');";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    columns.Add(reader.GetString(1));
            }
            if (!columns.Contains("SampleId"))
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"Missions\" ADD COLUMN \"SampleId\" TEXT NULL;", cancellationToken);
            if (!columns.Contains("TemplateId"))
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"Missions\" ADD COLUMN \"TemplateId\" TEXT NULL;", cancellationToken);
            if (!columns.Contains("MaxTokenUsage"))
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"Missions\" ADD COLUMN \"MaxTokenUsage\" INTEGER NULL;", cancellationToken);
            if (!columns.Contains("MaxEstimatedCost"))
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"Missions\" ADD COLUMN \"MaxEstimatedCost\" TEXT NULL;", cancellationToken);
            if (!columns.Contains("OperationPolicy"))
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"Missions\" ADD COLUMN \"OperationPolicy\" TEXT NOT NULL DEFAULT '[]';", cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }
}
