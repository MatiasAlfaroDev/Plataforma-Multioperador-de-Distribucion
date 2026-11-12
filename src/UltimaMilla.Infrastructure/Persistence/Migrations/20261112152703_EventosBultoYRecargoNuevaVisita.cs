using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventosBultoYRecargoNuevaVisita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RecargoNuevasVisitas",
                table: "envios",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "eventos_bulto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identificacion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EstadoAnterior = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EstadoNuevo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Resultado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_bulto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eventos_bulto_envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_bulto_EnvioId",
                table: "eventos_bulto",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_bulto_OperadorId_EnvioId_OcurridoEn",
                table: "eventos_bulto",
                columns: new[] { "OperadorId", "EnvioId", "OcurridoEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_bulto");

            migrationBuilder.DropColumn(
                name: "RecargoNuevasVisitas",
                table: "envios");
        }
    }
}
