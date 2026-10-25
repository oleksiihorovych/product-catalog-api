using System.Data;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Products;

namespace ProductCatalog.Infrastructure.Persistence;

public static class CatalogDatabaseSetup
{
    // Explicit administrative command only; the HTTP host never migrates or seeds.
    public static async Task InitializeAsync(CatalogDbContext db, bool seed, CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);
        if (!seed)
            return;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var demo = DemoProducts.Create();
        var ids = demo.Select(p => p.Id).ToArray();
        var existing = await db.Products.Where(p => ids.Contains(p.Id)).Select(p => p.Id)
            .ToListAsync(cancellationToken);
        db.Products.AddRange(demo.Where(p => !existing.Contains(p.Id)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
