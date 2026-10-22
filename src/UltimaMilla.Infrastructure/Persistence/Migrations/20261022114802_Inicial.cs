using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comercios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Rut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comercios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiveCount = table.Column<int>(type: "integer", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "operadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ColorPrimario = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    EmailContacto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "cuadros_tarifarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuadros_tarifarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cuadros_tarifarios_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cuentas_comerciales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComercioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuentas_comerciales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cuentas_comerciales_comercios_ComercioId",
                        column: x => x.ComercioId,
                        principalTable: "comercios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cuentas_comerciales_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versiones_reglas_operativas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaximoIntentosEntrega = table.Column<int>(type: "integer", nullable: false),
                    HorasMinimasEntreIntentos = table.Column<int>(type: "integer", nullable: false),
                    RequierePruebaEntrega = table.Column<bool>(type: "boolean", nullable: false),
                    DiasMaximosParaDevolucion = table.Column<int>(type: "integer", nullable: false),
                    VigenciaDesde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VigenciaHasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_versiones_reglas_operativas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_versiones_reglas_operativas_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zonas_cobertura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Departamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zonas_cobertura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_zonas_cobertura_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnqueueTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "motivos_no_entrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionReglasOperativasId = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RequiereDevolucion = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_motivos_no_entrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_motivos_no_entrega_versiones_reglas_operativas_VersionRegla~",
                        column: x => x.VersionReglasOperativasId,
                        principalTable: "versiones_reglas_operativas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versiones_tarifarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuadroTarifarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZonaCoberturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrecioBase = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PrecioPorKg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PrecioPorMetroCubico = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RecargoUrgentePorcentaje = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    BonificacionPorcentaje = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    VigenciaDesde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VigenciaHasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_versiones_tarifarias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_versiones_tarifarias_cuadros_tarifarios_CuadroTarifarioId",
                        column: x => x.CuadroTarifarioId,
                        principalTable: "cuadros_tarifarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_versiones_tarifarias_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_versiones_tarifarias_zonas_cobertura_ZonaCoberturaId",
                        column: x => x.ZonaCoberturaId,
                        principalTable: "zonas_cobertura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "envios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaComercialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenciaExterna = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Modalidad = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Estado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    destinatario_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destinatario_telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    direccion_calle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direccion_localidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direccion_departamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FechaAlta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TarifaCalculada = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    VersionTarifariaId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionReglasOperativasId = table.Column<Guid>(type: "uuid", nullable: true),
                    IntentosRealizados = table.Column<int>(type: "integer", nullable: false),
                    FechaReprogramada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FranjaReprogramada = table.Column<string>(type: "text", nullable: true),
                    UltimoMotivoNoEntregaId = table.Column<Guid>(type: "uuid", nullable: true),
                    UltimoMotivoRequiereDevolucion = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_envios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_envios_cuentas_comerciales_CuentaComercialId",
                        column: x => x.CuentaComercialId,
                        principalTable: "cuentas_comerciales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_envios_versiones_reglas_operativas_VersionReglasOperativasId",
                        column: x => x.VersionReglasOperativasId,
                        principalTable: "versiones_reglas_operativas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_envios_versiones_tarifarias_VersionTarifariaId",
                        column: x => x.VersionTarifariaId,
                        principalTable: "versiones_tarifarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bultos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identificacion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PesoKg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    AltoCm = table.Column<int>(type: "integer", nullable: false),
                    AnchoCm = table.Column<int>(type: "integer", nullable: false),
                    ProfundidadCm = table.Column<int>(type: "integer", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bultos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bultos_envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eventos_envio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EstadoNuevo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Disparador = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Origen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Responsable = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Latitud = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitud = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_envio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eventos_envio_envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventos_movil",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Receptor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecibidoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_movil", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eventos_movil_envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "intentos_entrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Entregado = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoNoEntregaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenciaEvidenciaFallida = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intentos_entrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_intentos_entrega_envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pruebas_entrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreReceptor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentoReceptor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenciaFirma = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReferenciaFotografia = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Latitud = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitud = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    IntentoEntregaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pruebas_entrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pruebas_entrega_intentos_entrega_IntentoEntregaId",
                        column: x => x.IntentoEntregaId,
                        principalTable: "intentos_entrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bultos_EnvioId",
                table: "bultos",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_comercios_Rut",
                table: "comercios",
                column: "Rut",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cuadros_tarifarios_OperadorId_Nombre",
                table: "cuadros_tarifarios",
                columns: new[] { "OperadorId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_comerciales_ComercioId",
                table: "cuentas_comerciales",
                column: "ComercioId");

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_comerciales_OperadorId_ComercioId",
                table: "cuentas_comerciales",
                columns: new[] { "OperadorId", "ComercioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_envios_CuentaComercialId_ReferenciaExterna",
                table: "envios",
                columns: new[] { "CuentaComercialId", "ReferenciaExterna" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_envios_OperadorId_FechaAlta",
                table: "envios",
                columns: new[] { "OperadorId", "FechaAlta" });

            migrationBuilder.CreateIndex(
                name: "IX_envios_VersionReglasOperativasId",
                table: "envios",
                column: "VersionReglasOperativasId");

            migrationBuilder.CreateIndex(
                name: "IX_envios_VersionTarifariaId",
                table: "envios",
                column: "VersionTarifariaId");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_envio_EnvioId",
                table: "eventos_envio",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_envio_OperadorId_EnvioId_OcurridoEn",
                table: "eventos_envio",
                columns: new[] { "OperadorId", "EnvioId", "OcurridoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_movil_EnvioId",
                table: "eventos_movil",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_movil_OperadorId_OcurridoEn",
                table: "eventos_movil",
                columns: new[] { "OperadorId", "OcurridoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_intentos_entrega_EnvioId_Numero",
                table: "intentos_entrega",
                columns: new[] { "EnvioId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_intentos_entrega_OperadorId_OcurridoEn",
                table: "intentos_entrega",
                columns: new[] { "OperadorId", "OcurridoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_motivos_no_entrega_OperadorId_VersionReglasOperativasId_Cod~",
                table: "motivos_no_entrega",
                columns: new[] { "OperadorId", "VersionReglasOperativasId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_motivos_no_entrega_VersionReglasOperativasId",
                table: "motivos_no_entrega",
                column: "VersionReglasOperativasId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_pruebas_entrega_IntentoEntregaId",
                table: "pruebas_entrega",
                column: "IntentoEntregaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_versiones_reglas_operativas_OperadorId_VigenciaDesde",
                table: "versiones_reglas_operativas",
                columns: new[] { "OperadorId", "VigenciaDesde" });

            migrationBuilder.CreateIndex(
                name: "IX_versiones_tarifarias_CuadroTarifarioId",
                table: "versiones_tarifarias",
                column: "CuadroTarifarioId");

            migrationBuilder.CreateIndex(
                name: "IX_versiones_tarifarias_OperadorId_CuadroTarifarioId_ZonaCober~",
                table: "versiones_tarifarias",
                columns: new[] { "OperadorId", "CuadroTarifarioId", "ZonaCoberturaId", "VigenciaDesde" });

            migrationBuilder.CreateIndex(
                name: "IX_versiones_tarifarias_ZonaCoberturaId",
                table: "versiones_tarifarias",
                column: "ZonaCoberturaId");

            migrationBuilder.CreateIndex(
                name: "IX_zonas_cobertura_OperadorId_Nombre",
                table: "zonas_cobertura",
                columns: new[] { "OperadorId", "Nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bultos");

            migrationBuilder.DropTable(
                name: "eventos_envio");

            migrationBuilder.DropTable(
                name: "eventos_movil");

            migrationBuilder.DropTable(
                name: "motivos_no_entrega");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "pruebas_entrega");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "OutboxState");

            migrationBuilder.DropTable(
                name: "intentos_entrega");

            migrationBuilder.DropTable(
                name: "envios");

            migrationBuilder.DropTable(
                name: "cuentas_comerciales");

            migrationBuilder.DropTable(
                name: "versiones_reglas_operativas");

            migrationBuilder.DropTable(
                name: "versiones_tarifarias");

            migrationBuilder.DropTable(
                name: "comercios");

            migrationBuilder.DropTable(
                name: "cuadros_tarifarios");

            migrationBuilder.DropTable(
                name: "zonas_cobertura");

            migrationBuilder.DropTable(
                name: "operadores");
        }
    }
}
