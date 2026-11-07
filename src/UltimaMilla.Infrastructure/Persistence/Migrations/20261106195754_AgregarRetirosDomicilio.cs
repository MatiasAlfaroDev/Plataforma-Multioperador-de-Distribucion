using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRetirosDomicilio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retiros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaComercialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    retiro_calle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    retiro_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retiro_localidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    retiro_departamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    destino_calle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destino_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destino_localidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destino_departamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FechaSolicitada = table.Column<DateOnly>(type: "date", nullable: false),
                    HoraDesde = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    HoraHasta = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SolicitadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HojaDeRutaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenciaFotografia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenciaFirma = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RetiradoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retiros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_retiros_cuentas_comerciales_CuentaComercialId",
                        column: x => x.CuentaComercialId,
                        principalTable: "cuentas_comerciales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bultos_retiro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identificacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PesoKg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    AltoCm = table.Column<int>(type: "integer", nullable: false),
                    AnchoCm = table.Column<int>(type: "integer", nullable: false),
                    ProfundidadCm = table.Column<int>(type: "integer", nullable: false),
                    RetiroId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bultos_retiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bultos_retiro_retiros_RetiroId",
                        column: x => x.RetiroId,
                        principalTable: "retiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bultos_retiro_RetiroId_Identificacion",
                table: "bultos_retiro",
                columns: new[] { "RetiroId", "Identificacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retiros_CuentaComercialId",
                table: "retiros",
                column: "CuentaComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_retiros_OperadorId_CuentaComercialId_Estado",
                table: "retiros",
                columns: new[] { "OperadorId", "CuentaComercialId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_retiros_OperadorId_SolicitadoEn",
                table: "retiros",
                columns: new[] { "OperadorId", "SolicitadoEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bultos_retiro");

            migrationBuilder.DropTable(
                name: "retiros");
        }
    }
}
