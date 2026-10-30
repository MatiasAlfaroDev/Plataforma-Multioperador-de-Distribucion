using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VincularRepartidorConUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                table: "repartidores",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_repartidores_OperadorId_UsuarioId",
                table: "repartidores",
                columns: new[] { "OperadorId", "UsuarioId" },
                unique: true,
                filter: "\"UsuarioId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_repartidores_UsuarioId",
                table: "repartidores",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_repartidores_AspNetUsers_UsuarioId",
                table: "repartidores",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_repartidores_AspNetUsers_UsuarioId",
                table: "repartidores");

            migrationBuilder.DropIndex(
                name: "IX_repartidores_OperadorId_UsuarioId",
                table: "repartidores");

            migrationBuilder.DropIndex(
                name: "IX_repartidores_UsuarioId",
                table: "repartidores");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "repartidores");
        }
    }
}
