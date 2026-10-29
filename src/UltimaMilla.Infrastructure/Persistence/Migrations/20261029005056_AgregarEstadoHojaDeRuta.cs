using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEstadoHojaDeRuta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DespachadaEn",
                table: "hojas_de_ruta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "hojas_de_ruta",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "EnPreparacion");

            // Las hojas existentes que ya tuvieron un despacho
            // deben conservar su condición de despachadas.
            migrationBuilder.Sql("""
                UPDATE hojas_de_ruta h
                SET "Estado" = 'Despachada',
                    "DespachadaEn" = (
                        SELECT MIN(ev."OcurridoEn")
                        FROM paradas_ruta p
                        JOIN eventos_envio ev
                            ON ev."EnvioId" = p."EnvioId"
                        WHERE p."HojaDeRutaId" = h."Id"
                        AND ev."Disparador" = 'Despachar'
                        AND ev."OcurridoEn" >= p."CreadoEn"
                    )
                WHERE EXISTS (
                    SELECT 1
                    FROM paradas_ruta p
                    JOIN eventos_envio ev
                        ON ev."EnvioId" = p."EnvioId"
                    WHERE p."HojaDeRutaId" = h."Id"
                    AND ev."Disparador" = 'Despachar'
                    AND ev."OcurridoEn" >= p."CreadoEn"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DespachadaEn",
                table: "hojas_de_ruta");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "hojas_de_ruta");
        }
    }
}
