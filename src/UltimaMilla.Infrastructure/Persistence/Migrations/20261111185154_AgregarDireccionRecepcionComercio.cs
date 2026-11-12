using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDireccionRecepcionComercio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recepcion_calle",
                table: "cuentas_comerciales",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recepcion_departamento",
                table: "cuentas_comerciales",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recepcion_localidad",
                table: "cuentas_comerciales",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recepcion_numero",
                table: "cuentas_comerciales",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recepcion_calle",
                table: "cuentas_comerciales");

            migrationBuilder.DropColumn(
                name: "recepcion_departamento",
                table: "cuentas_comerciales");

            migrationBuilder.DropColumn(
                name: "recepcion_localidad",
                table: "cuentas_comerciales");

            migrationBuilder.DropColumn(
                name: "recepcion_numero",
                table: "cuentas_comerciales");
        }
    }
}
