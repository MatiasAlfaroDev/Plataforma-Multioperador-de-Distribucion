using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConstanciaDevolucion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModalidadDevolucion",
                table: "eventos_envio",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceptorDevolucion",
                table: "eventos_envio",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaConstanciaDevolucion",
                table: "eventos_envio",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModalidadDevolucion",
                table: "eventos_envio");

            migrationBuilder.DropColumn(
                name: "ReceptorDevolucion",
                table: "eventos_envio");

            migrationBuilder.DropColumn(
                name: "ReferenciaConstanciaDevolucion",
                table: "eventos_envio");
        }
    }
}
