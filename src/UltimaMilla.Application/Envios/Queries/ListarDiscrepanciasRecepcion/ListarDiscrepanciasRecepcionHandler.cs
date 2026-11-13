using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.RecibirEnDeposito;

namespace UltimaMilla.Application.Envios.Queries.ListarDiscrepanciasRecepcion;

public sealed class ListarDiscrepanciasRecepcionHandler(
    IEnvioRepository envios,
    IDiscrepanciaRecepcionRepository discrepancias)
{
    public async Task<IReadOnlyList<DiscrepanciaRecepcionDto>> Handle(
        Guid envioId,
        CancellationToken ct)
    {
        // El repositorio de envíos filtra por operador: uno ajeno aparece como inexistente.
        _ = await envios.ObtenerAsync(envioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        var lista = await discrepancias.ListarPorEnvioAsync(envioId, ct);

        return lista
            .Select(d => new DiscrepanciaRecepcionDto(
                d.Tipo, d.Identificacion, d.Responsable, d.RegistradaEn))
            .ToList();
    }
}
