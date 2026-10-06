using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Operadores.Queries.ListarOperadores;

public sealed class ListarOperadoresHandler(IOperadorRepository operadores)
{
    public async Task<IReadOnlyList<OperadorDto>> Handle(ListarOperadoresQuery query, CancellationToken ct)
    {
        var lista = await operadores.ListarAsync(ct);
        return lista.Select(o => new OperadorDto(o.Id, o.Nombre, o.ColorPrimario)).ToList();
    }
}
