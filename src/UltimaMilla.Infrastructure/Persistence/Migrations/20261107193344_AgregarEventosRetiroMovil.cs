using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEventosRetiroMovil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventos_retiro_movil",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RetiroId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecibidoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MotivoFallo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenciaFotografia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenciaFirma = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IdentificacionesBultos = table.Column<string[]>(type: "text[]", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_retiro_movil", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eventos_retiro_movil_retiros_RetiroId",
                        column: x => x.RetiroId,
                        principalTable: "retiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_retiro_movil_OperadorId_RetiroId_OcurridoEn",
                table: "eventos_retiro_movil",
                columns: new[] { "OperadorId", "RetiroId", "OcurridoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_retiro_movil_RetiroId",
                table: "eventos_retiro_movil",
                column: "RetiroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_retiro_movil");
        }
    }
}
