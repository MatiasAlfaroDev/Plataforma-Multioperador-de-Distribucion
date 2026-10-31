using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnicaReglaVigentePorOperador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_reglas_operativas_una_vigente_por_operador",
                table: "versiones_reglas_operativas",
                column: "OperadorId",
                unique: true,
                filter: "\"VigenciaHasta\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reglas_operativas_una_vigente_por_operador",
                table: "versiones_reglas_operativas");
        }
    }
}
