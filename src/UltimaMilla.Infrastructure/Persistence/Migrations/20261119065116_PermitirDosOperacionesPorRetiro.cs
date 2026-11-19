using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermitirDosOperacionesPorRetiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_paradas_ruta_RetiroId",
                table: "paradas_ruta");

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_RetiroId_TipoOperacion",
                table: "paradas_ruta",
                columns: new[] { "RetiroId", "TipoOperacion" },
                unique: true,
                filter: "\"RetiroId\" IS NOT NULL AND \"liberada_en\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_paradas_ruta_RetiroId_TipoOperacion",
                table: "paradas_ruta");

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_RetiroId",
                table: "paradas_ruta",
                column: "RetiroId",
                unique: true,
                filter: "\"RetiroId\" IS NOT NULL AND \"liberada_en\" IS NULL");
        }
    }
}
