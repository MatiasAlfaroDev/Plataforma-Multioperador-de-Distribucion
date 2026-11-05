using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAvisosAComercios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "suscripciones_aviso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaComercialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Secreto = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TiposEvento = table.Column<List<string>>(type: "text[]", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suscripciones_aviso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_suscripciones_aviso_cuentas_comerciales_CuentaComercialId",
                        column: x => x.CuentaComercialId,
                        principalTable: "cuentas_comerciales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entregas_aviso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuscripcionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaComercialId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEvento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClaveEvento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Cuerpo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    UltimoError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UltimoIntentoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntregadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entregas_aviso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entregas_aviso_suscripciones_aviso_SuscripcionId",
                        column: x => x.SuscripcionId,
                        principalTable: "suscripciones_aviso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entregas_aviso_OperadorId_CuentaComercialId_Estado",
                table: "entregas_aviso",
                columns: new[] { "OperadorId", "CuentaComercialId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_entregas_aviso_OperadorId_SuscripcionId_ClaveEvento",
                table: "entregas_aviso",
                columns: new[] { "OperadorId", "SuscripcionId", "ClaveEvento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entregas_aviso_SuscripcionId",
                table: "entregas_aviso",
                column: "SuscripcionId");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_aviso_CuentaComercialId",
                table: "suscripciones_aviso",
                column: "CuentaComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_aviso_OperadorId_CuentaComercialId",
                table: "suscripciones_aviso",
                columns: new[] { "OperadorId", "CuentaComercialId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entregas_aviso");

            migrationBuilder.DropTable(
                name: "suscripciones_aviso");
        }
    }
}
