using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEvidenciasDevolucionMovil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentoReceptor",
                table: "eventos_movil",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaFirma",
                table: "eventos_movil",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaFotografia",
                table: "eventos_movil",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentoReceptor",
                table: "eventos_movil");

            migrationBuilder.DropColumn(
                name: "ReferenciaFirma",
                table: "eventos_movil");

            migrationBuilder.DropColumn(
                name: "ReferenciaFotografia",
                table: "eventos_movil");
        }
    }
}
