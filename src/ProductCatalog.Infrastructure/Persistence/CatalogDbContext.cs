using Microsoft.EntityFrameworkCore;
using ProductCatalog.Domain;
using ProductCatalog.Domain.Products;

namespace ProductCatalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<Product>();
        product.ToTable("Products", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Products_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
            table.HasCheckConstraint("CK_Products_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });

        // The clustered primary key supports the complete listing's Id ordering.
        product.HasKey(p => p.Id);
        product.Property(p => p.Id).ValueGeneratedNever();
        product.Property(p => p.Name).HasMaxLength(DomainConstants.ProductMaxNameLength).IsRequired();
        product.Property(p => p.ImgUri).HasMaxLength(DomainConstants.ProductMaxImageUriLength).IsRequired();
        product.Property(p => p.Price).HasPrecision(18, 2);
        product.Property(p => p.Description).HasMaxLength(DomainConstants.ProductMaxDescriptionLength);
    }
}
