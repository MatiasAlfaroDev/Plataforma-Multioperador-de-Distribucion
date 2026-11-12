using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClaveApiComercio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveApiHash",
                table: "usuarios_comercios",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_comercios_ClaveApiHash",
                table: "usuarios_comercios",
                column: "ClaveApiHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_comercios_ClaveApiHash",
                table: "usuarios_comercios");

            migrationBuilder.DropColumn(
                name: "ClaveApiHash",
                table: "usuarios_comercios");
        }
    }
}
