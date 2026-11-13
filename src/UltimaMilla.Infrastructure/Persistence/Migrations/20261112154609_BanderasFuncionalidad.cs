using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BanderasFuncionalidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "banderas_funcionalidad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Habilitada = table.Column<bool>(type: "boolean", nullable: false),
                    ModificadoPor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banderas_funcionalidad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_banderas_funcionalidad_operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cambios_bandera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Habilitada = table.Column<bool>(type: "boolean", nullable: false),
                    ModificadoPor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cambios_bandera", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_banderas_funcionalidad_OperadorId_Nombre",
                table: "banderas_funcionalidad",
                columns: new[] { "OperadorId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cambios_bandera_ModificadoEn",
                table: "cambios_bandera",
                column: "ModificadoEn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banderas_funcionalidad");

            migrationBuilder.DropTable(
                name: "cambios_bandera");
        }
    }
}
