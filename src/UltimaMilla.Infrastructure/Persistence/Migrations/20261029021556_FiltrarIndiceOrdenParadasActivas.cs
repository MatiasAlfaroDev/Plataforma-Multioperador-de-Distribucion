using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FiltrarIndiceOrdenParadasActivas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_paradas_ruta_HojaDeRutaId_Orden",
                table: "paradas_ruta");

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_HojaDeRutaId_Orden",
                table: "paradas_ruta",
                columns: new[] { "HojaDeRutaId", "Orden" },
                unique: true,
                filter: "\"liberada_en\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_paradas_ruta_HojaDeRutaId_Orden",
                table: "paradas_ruta");

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_HojaDeRutaId_Orden",
                table: "paradas_ruta",
                columns: new[] { "HojaDeRutaId", "Orden" },
                unique: true);
        }
    }
}
