using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTipoOperacionParada : Migration
    {
        /// <inheritdoc />
        
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Agregar la columna temporalmente como opcional.
        migrationBuilder.AddColumn<string>(
            name: "TipoOperacion",
            table: "paradas_ruta",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        // 2. Clasificar las paradas que ya existen.
        migrationBuilder.Sql("""
            UPDATE paradas_ruta
            SET "TipoOperacion" = CASE
                WHEN "RetiroId" IS NOT NULL THEN 'Retiro'
                WHEN "EnvioId" IS NOT NULL THEN 'Entrega'
            END;
            """);

        // 3. La columna pasa a ser obligatoria.
        migrationBuilder.AlterColumn<string>(
            name: "TipoOperacion",
            table: "paradas_ruta",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(40)",
            oldMaxLength: 40,
            oldNullable: true);
    }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoOperacion",
                table: "paradas_ruta");
        }
    }
}
