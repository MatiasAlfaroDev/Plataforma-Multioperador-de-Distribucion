using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEntregasParciales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "bultos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pendiente");

            migrationBuilder.CreateTable(
                name: "resultados_bultos_intento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentoEntregaId = table.Column<Guid>(type: "uuid", nullable: false),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Entregado = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoNoEntregaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenciaEvidenciaFallo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resultados_bultos_intento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resultados_bultos_intento_bultos_BultoId",
                        column: x => x.BultoId,
                        principalTable: "bultos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resultados_bultos_intento_intentos_entrega_IntentoEntregaId",
                        column: x => x.IntentoEntregaId,
                        principalTable: "intentos_entrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resultados_bultos_intento_motivos_no_entrega_MotivoNoEntreg~",
                        column: x => x.MotivoNoEntregaId,
                        principalTable: "motivos_no_entrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.Sql("""
                UPDATE bultos AS b
                SET "Estado" = CASE
                    WHEN e."Estado" = 'Entregado' THEN 'Entregado'
                    WHEN e."Estado" = 'Extraviado' THEN 'Extraviado'
                    WHEN e."Estado" = 'EnDevolucion' THEN 'EnDevolucion'
                    WHEN e."Estado" = 'Devuelto' THEN 'Devuelto'
                    ELSE 'Pendiente'
                END
                FROM envios AS e
                WHERE b."EnvioId" = e."Id";
                """);
            migrationBuilder.CreateIndex(
                name: "IX_resultados_bultos_intento_BultoId",
                table: "resultados_bultos_intento",
                column: "BultoId");

            migrationBuilder.CreateIndex(
                name: "IX_resultados_bultos_intento_IntentoEntregaId_BultoId",
                table: "resultados_bultos_intento",
                columns: new[] { "IntentoEntregaId", "BultoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resultados_bultos_intento_MotivoNoEntregaId",
                table: "resultados_bultos_intento",
                column: "MotivoNoEntregaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resultados_bultos_intento");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "bultos");
        }
    }
}
