using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UsuarioUnicoPorComercio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_comercios_UsuarioId_ComercioId",
                table: "usuarios_comercios");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_comercios_UsuarioId",
                table: "usuarios_comercios",
                column: "UsuarioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_comercios_UsuarioId",
                table: "usuarios_comercios");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_comercios_UsuarioId_ComercioId",
                table: "usuarios_comercios",
                columns: new[] { "UsuarioId", "ComercioId" },
                unique: true);
        }
    }
}
