using System.Data;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Products;

namespace ProductCatalog.Infrastructure.Persistence;

/// <summary>Prepares a SQL catalog through the explicit administrative startup mode.</summary>
public static class CatalogDatabaseSetup
{
    /// <summary>Applies pending migrations and optionally inserts missing demo products.</summary>
    /// <param name="db">The context configured with migration and seed permissions.</param>
    /// <param name="seed">Whether to add missing demo IDs without overwriting existing rows.</param>
    /// <param name="cancellationToken">Cancels migration and seed operations.</param>
    /// <remarks>
    /// HTTP startup does not call this method. Migrations run before the seed transaction;
    /// a failed seed does not roll back already applied schema changes.
    /// </remarks>
    public static async Task InitializeAsync(CatalogDbContext db, bool seed, CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (!seed)
        {
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var demo = DemoProducts.Create();
        var ids = demo.Select(p => p.Id).ToArray();
        var existing = await db.Products
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        db.Products.AddRange(demo.Where(p => !existing.Contains(p.Id)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
