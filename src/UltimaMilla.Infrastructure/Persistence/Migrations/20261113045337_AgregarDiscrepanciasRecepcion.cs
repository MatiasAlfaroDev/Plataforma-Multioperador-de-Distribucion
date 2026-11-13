using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDiscrepanciasRecepcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discrepancias_recepcion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Identificacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Responsable = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegistradaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discrepancias_recepcion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_discrepancias_recepcion_envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_discrepancias_recepcion_EnvioId",
                table: "discrepancias_recepcion",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_discrepancias_recepcion_OperadorId_EnvioId_RegistradaEn",
                table: "discrepancias_recepcion",
                columns: new[] { "OperadorId", "EnvioId", "RegistradaEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discrepancias_recepcion");
        }
    }
}
