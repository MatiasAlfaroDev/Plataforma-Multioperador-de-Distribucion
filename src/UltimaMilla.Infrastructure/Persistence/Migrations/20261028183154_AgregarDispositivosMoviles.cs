using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDispositivosMoviles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispositivos_moviles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Plataforma = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UltimoRegistroEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispositivos_moviles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispositivos_moviles_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispositivos_moviles_OperadorId_Token",
                table: "dispositivos_moviles",
                columns: new[] { "OperadorId", "Token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispositivos_moviles_OperadorId_UsuarioId",
                table: "dispositivos_moviles",
                columns: new[] { "OperadorId", "UsuarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_dispositivos_moviles_UsuarioId",
                table: "dispositivos_moviles",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispositivos_moviles");
        }
    }
}
