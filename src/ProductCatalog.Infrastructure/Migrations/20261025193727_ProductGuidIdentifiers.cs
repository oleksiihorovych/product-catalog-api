using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductCatalog.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ProductGuidIdentifiers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "GuidId", schema: "dbo", table: "Products",
            type: "uniqueidentifier", nullable: true);

        // SQL cannot directly cast bigint to uniqueidentifier. Preserve each ID with a
        // reversible hexadecimal encoding; EXEC delays binding until GuidId exists.
        migrationBuilder.Sql("""
            EXEC(N'UPDATE [dbo].[Products]
                SET [GuidId] = CONVERT(uniqueidentifier,
                    ''00000000-0000-0000-'' +
                    STUFF(CONVERT(varchar(16), CONVERT(binary(8), [Id]), 2), 5, 0, ''-''))');
            """);

        migrationBuilder.DropCheckConstraint(name: "CK_Products_Id", schema: "dbo", table: "Products");
        migrationBuilder.DropPrimaryKey(name: "PK_Products", schema: "dbo", table: "Products");
        migrationBuilder.DropColumn(name: "Id", schema: "dbo", table: "Products");
        migrationBuilder.RenameColumn(name: "GuidId", schema: "dbo", table: "Products", newName: "Id");
        migrationBuilder.AlterColumn<Guid>(name: "Id", schema: "dbo", table: "Products",
            type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid),
            oldType: "uniqueidentifier", oldNullable: true);
        migrationBuilder.AddPrimaryKey(name: "PK_Products", schema: "dbo", table: "Products", column: "Id");
        migrationBuilder.AddCheckConstraint(name: "CK_Products_Id", schema: "dbo", table: "Products",
            sql: "[Id] <> '00000000-0000-0000-0000-000000000000'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("GUID identifiers cannot generally be converted to bigint. Restore a pre-upgrade backup with its application version.");
}
