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

            // Las bases sembradas antes de esta regla tienen un usuario en dos comercios (el seed vinculaba
            // admin@modaexpress.test también a Tienda Norte). Queda un vínculo por usuario: el de administrador
            // si lo tiene y, entre iguales, el de menor Id.
            migrationBuilder.Sql("""
                DELETE FROM usuarios_comercios uc
                USING usuarios_comercios otro
                WHERE uc."UsuarioId" = otro."UsuarioId"
                  AND uc."Id" <> otro."Id"
                  AND (CASE WHEN uc."Perfil" = 'AdministradorComercio' THEN 0 ELSE 1 END, uc."Id")
                    > (CASE WHEN otro."Perfil" = 'AdministradorComercio' THEN 0 ELSE 1 END, otro."Id");
                """);

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
