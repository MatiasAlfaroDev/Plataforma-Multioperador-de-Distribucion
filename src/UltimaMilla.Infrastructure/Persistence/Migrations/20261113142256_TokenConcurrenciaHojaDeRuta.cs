using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TokenConcurrenciaHojaDeRuta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin es una columna de sistema de PostgreSQL: ya existe en hojas_de_ruta.
            // Esta migración solo actualiza el modelo para que EF la use como token de concurrencia.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}