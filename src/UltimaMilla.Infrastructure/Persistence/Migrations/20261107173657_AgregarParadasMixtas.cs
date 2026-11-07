using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarParadasMixtas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_paradas_ruta_EnvioId",
                table: "paradas_ruta");

            migrationBuilder.AlterColumn<Guid>(
                name: "EnvioId",
                table: "paradas_ruta",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "RetiroId",
                table: "paradas_ruta",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_EnvioId",
                table: "paradas_ruta",
                column: "EnvioId",
                unique: true,
                filter: "\"EnvioId\" IS NOT NULL AND \"liberada_en\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_RetiroId",
                table: "paradas_ruta",
                column: "RetiroId",
                unique: true,
                filter: "\"RetiroId\" IS NOT NULL AND \"liberada_en\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_paradas_ruta_referencia",
                table: "paradas_ruta",
                sql: "(\"EnvioId\" IS NOT NULL AND \"RetiroId\" IS NULL)\r\nOR\r\n(\"EnvioId\" IS NULL AND \"RetiroId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_paradas_ruta_retiros_RetiroId",
                table: "paradas_ruta",
                column: "RetiroId",
                principalTable: "retiros",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_paradas_ruta_retiros_RetiroId",
                table: "paradas_ruta");

            migrationBuilder.DropIndex(
                name: "IX_paradas_ruta_EnvioId",
                table: "paradas_ruta");

            migrationBuilder.DropIndex(
                name: "IX_paradas_ruta_RetiroId",
                table: "paradas_ruta");

            migrationBuilder.DropCheckConstraint(
                name: "CK_paradas_ruta_referencia",
                table: "paradas_ruta");

            migrationBuilder.DropColumn(
                name: "RetiroId",
                table: "paradas_ruta");

            migrationBuilder.AlterColumn<Guid>(
                name: "EnvioId",
                table: "paradas_ruta",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ruta_EnvioId",
                table: "paradas_ruta",
                column: "EnvioId",
                unique: true,
                filter: "\"liberada_en\" IS NULL");
        }
    }
}
