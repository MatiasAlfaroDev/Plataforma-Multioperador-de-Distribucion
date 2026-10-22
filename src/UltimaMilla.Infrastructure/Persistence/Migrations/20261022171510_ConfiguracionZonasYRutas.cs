using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracionZonasYRutas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinutosEntregaEstimado",
                table: "zonas_cobertura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinutosTrasladoEstimado",
                table: "zonas_cobertura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaEntregaComprometida",
                table: "envios",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraEntregaDesde",
                table: "envios",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraEntregaHasta",
                table: "envios",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "franjas_entrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZonaCoberturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    HoraDesde = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    HoraHasta = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_franjas_entrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_franjas_entrega_zonas_cobertura_ZonaCoberturaId",
                        column: x => x.ZonaCoberturaId,
                        principalTable: "zonas_cobertura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "localidades_cubiertas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZonaCoberturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sector = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_localidades_cubiertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_localidades_cubiertas_zonas_cobertura_ZonaCoberturaId",
                        column: x => x.ZonaCoberturaId,
                        principalTable: "zonas_cobertura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repartidores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repartidores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repartidores_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CapacidadPesoKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    CapacidadVolumenM3 = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehiculos_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hojas_de_ruta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepartidorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehiculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    HoraSalida = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MaximoParadas = table.Column<int>(type: "integer", nullable: false),
                    CapacidadPesoKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    CapacidadVolumenM3 = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    PesoAsignadoKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    VolumenAsignadoM3 = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hojas_de_ruta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hojas_de_ruta_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hojas_de_ruta_repartidores_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "repartidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hojas_de_ruta_vehiculos_VehiculoId",
                        column: x => x.VehiculoId,
                        principalTable: "vehiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "paradas_ruta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HojaDeRutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    liberada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paradas_ruta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paradas_ruta_envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_paradas_ruta_hojas_de_ruta_HojaDeRutaId",
                        column: x => x.HojaDeRutaId,
                        principalTable: "hojas_de_ruta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_franjas_entrega_ZonaCoberturaId_HoraDesde_HoraHasta",
                table: "franjas_entrega",
                columns: new[] { "ZonaCoberturaId", "HoraDesde", "HoraHasta" });

            migrationBuilder.CreateIndex(
                name: "IX_hojas_de_ruta_OperadorId_Fecha",
                table: "hojas_de_ruta",
                columns: new[] { "OperadorId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_hojas_de_ruta_RepartidorId",
                table: "hojas_de_ruta",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_hojas_de_ruta_VehiculoId",
                table: "hojas_de_ruta",
                column: "VehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_localidades_cubiertas_ZonaCoberturaId_Nombre_Sector",
                table: "localidades_cubiertas",
                columns: new[] { "ZonaCoberturaId", "Nombre", "Sector" });

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_EnvioId",
                table: "paradas_ruta",
                column: "EnvioId",
                unique: true,
                filter: "\"liberada_en\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_HojaDeRutaId_Orden",
                table: "paradas_ruta",
                columns: new[] { "HojaDeRutaId", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repartidores_OperadorId_Activo",
                table: "repartidores",
                columns: new[] { "OperadorId", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_vehiculos_OperadorId_Matricula",
                table: "vehiculos",
                columns: new[] { "OperadorId", "Matricula" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "franjas_entrega");

            migrationBuilder.DropTable(
                name: "localidades_cubiertas");

            migrationBuilder.DropTable(
                name: "paradas_ruta");

            migrationBuilder.DropTable(
                name: "hojas_de_ruta");

            migrationBuilder.DropTable(
                name: "repartidores");

            migrationBuilder.DropTable(
                name: "vehiculos");

            migrationBuilder.DropColumn(
                name: "MinutosEntregaEstimado",
                table: "zonas_cobertura");

            migrationBuilder.DropColumn(
                name: "MinutosTrasladoEstimado",
                table: "zonas_cobertura");

            migrationBuilder.DropColumn(
                name: "FechaEntregaComprometida",
                table: "envios");

            migrationBuilder.DropColumn(
                name: "HoraEntregaDesde",
                table: "envios");

            migrationBuilder.DropColumn(
                name: "HoraEntregaHasta",
                table: "envios");
        }
    }
}
