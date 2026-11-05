using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarReglasEntregaParcial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccionBultosPendientes",
                table: "versiones_reglas_operativas",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NuevaVisita");

            migrationBuilder.AddColumn<bool>(
                name: "AdmiteEntregaParcial",
                table: "versiones_reglas_operativas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NuevaVisitaConRecargo",
                table: "versiones_reglas_operativas",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccionBultosPendientes",
                table: "versiones_reglas_operativas");

            migrationBuilder.DropColumn(
                name: "AdmiteEntregaParcial",
                table: "versiones_reglas_operativas");

            migrationBuilder.DropColumn(
                name: "NuevaVisitaConRecargo",
                table: "versiones_reglas_operativas");
        }
    }
}
