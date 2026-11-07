using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTarifasPorTipoServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoServicio",
                table: "versiones_tarifarias",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Entrega");

            migrationBuilder.AddColumn<decimal>(
                name: "TarifaCalculada",
                table: "retiros",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VersionTarifariaId",
                table: "retiros",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_retiros_VersionTarifariaId",
                table: "retiros",
                column: "VersionTarifariaId");

            migrationBuilder.AddForeignKey(
                name: "FK_retiros_versiones_tarifarias_VersionTarifariaId",
                table: "retiros",
                column: "VersionTarifariaId",
                principalTable: "versiones_tarifarias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_retiros_versiones_tarifarias_VersionTarifariaId",
                table: "retiros");

            migrationBuilder.DropIndex(
                name: "IX_retiros_VersionTarifariaId",
                table: "retiros");

            migrationBuilder.DropColumn(
                name: "TipoServicio",
                table: "versiones_tarifarias");

            migrationBuilder.DropColumn(
                name: "TarifaCalculada",
                table: "retiros");

            migrationBuilder.DropColumn(
                name: "VersionTarifariaId",
                table: "retiros");
        }
    }
}
