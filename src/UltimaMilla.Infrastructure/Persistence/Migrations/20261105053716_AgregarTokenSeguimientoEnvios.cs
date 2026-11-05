using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTokenSeguimientoEnvios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenSeguimientoHash",
                table: "envios",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_envios_TokenSeguimientoHash",
                table: "envios",
                column: "TokenSeguimientoHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_envios_TokenSeguimientoHash",
                table: "envios");

            migrationBuilder.DropColumn(
                name: "TokenSeguimientoHash",
                table: "envios");
        }
    }
}
