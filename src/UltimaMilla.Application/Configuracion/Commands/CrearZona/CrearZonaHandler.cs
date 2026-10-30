
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion.Commands.CrearZona;

public sealed class CrearZonaHandler(
    IZonaCoberturaRepository zonas,
    IUnitOfWork unitOfWork)
{
    public async Task<Guid> Handle(
        CrearZonaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new ArgumentException(
                "El nombre de la zona es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.Departamento))
            throw new ArgumentException(
                "El departamento es obligatorio.");

        if (command.Coberturas is null ||
            command.Coberturas.Count == 0)
            throw new ArgumentException(
                "La zona debe tener al menos una cobertura.");

        if (command.Franjas is null ||
            command.Franjas.Count == 0)
            throw new ArgumentException(
                "La zona debe tener al menos una franja.");

        if (await zonas.ExisteNombreAsync(
                command.OperadorId,
                command.Nombre.Trim(),
                ct))
        {
            throw new InvalidOperationException(
                "Ya existe una zona con ese nombre para el operador.");
        }

        var zona = ZonaCobertura.Crear(
            command.OperadorId,
            command.Nombre,
            command.Departamento);

        zona.ConfigurarTiempoEstimado(command.MinutosTraslado);

        foreach (var cobertura in command.Coberturas)
        {
            if (cobertura is null)
                throw new ArgumentException(
                    "Hay una cobertura inválida.");

            if (string.IsNullOrWhiteSpace(cobertura.Sector))
                zona.AgregarLocalidad(cobertura.Localidad);
            else
                zona.AgregarSector(
                    cobertura.Localidad,
                    cobertura.Sector);
        }

        foreach (var franja in command.Franjas)
        {
            if (franja is null)
                throw new ArgumentException(
                    "Hay una franja inválida.");

            zona.AgregarFranja(
                franja.HoraDesde,
                franja.HoraHasta);
        }

        // La cobertura no debe generar ambigüedades
        // con otras zonas activas del mismo operador.
        var existentes = await zonas.ListarPorOperadorAsync(
            command.OperadorId,
            ct);

        ValidacionCoberturas.ValidarSinSuperposicion(zona, existentes);

        await zonas.AgregarAsync(zona, ct);
        await unitOfWork.GuardarCambiosAsync(ct);

        return zona.Id;
    }
}
