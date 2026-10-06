using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Envios.Queries.ListarEnvios;

public sealed class ListarEnviosHandler(IEnvioRepository envios, ICuentaComercialRepository cuentas)
{
    public async Task<IReadOnlyList<EnvioDto>> Handle(ListarEnviosQuery query, CancellationToken ct)
    {
        var lista = await envios.ListarAsync(query.OperadorId, ct);
        var cuentasPorId = (await cuentas.ListarResumenAsync(ct)).ToDictionary(c => c.Id);

        return lista
            .Select(e =>
            {
                cuentasPorId.TryGetValue(e.CuentaComercialId, out var cuenta);
                return new EnvioDto(
                    e.Id,
                    e.ReferenciaExterna,
                    e.OperadorId,
                    cuenta?.OperadorNombre ?? "(desconocido)",
                    cuenta?.ComercioNombre ?? "(desconocido)",
                    e.Modalidad.ToString(),
                    e.Estado.ToString(),
                    e.Destinatario.Nombre,
                    e.Direccion.ToString(),
                    e.Bultos.Count,
                    e.PesoTotalKg,
                    e.FechaAlta);
            })
            .ToList();
    }
}
