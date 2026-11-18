using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CierresDiarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cierres_diarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    EnviosCreados = table.Column<int>(type: "integer", nullable: false),
                    EntregasCompletas = table.Column<int>(type: "integer", nullable: false),
                    EntregasParciales = table.Column<int>(type: "integer", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "integer", nullable: false),
                    PorcentajeCumplimiento = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ATiempo = table.Column<int>(type: "integer", nullable: false),
                    FueraDePlazo = table.Column<int>(type: "integer", nullable: false),
                    PorcentajePuntualidad = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CalculadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cierres_diarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cierres_diarios_OperadorId_Fecha",
                table: "cierres_diarios",
                columns: new[] { "OperadorId", "Fecha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cierres_diarios");
        }
    }
}
