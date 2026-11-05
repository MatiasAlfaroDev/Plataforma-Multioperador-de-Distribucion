using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria.Commands.ReintentarMensajesFallidos;

namespace UltimaMilla.UnitTests;

/// <summary>
/// Reintento de mensajes fallidos (RNF 6.8): el nombre de la cola llega de la URL y va a la API del broker.
/// </summary>
public class MensajesFallidosTests
{
    private readonly ColasEnMemoria _colas = new();

    [Theory]
    [InlineData("entregar-aviso_error")]
    [InlineData("generar-avisos_error")]
    public async Task Cola_de_fallidos_se_reintenta(string cola)
    {
        var n = await new ReintentarMensajesFallidosHandler(_colas).Handle(new ReintentarMensajesFallidosCommand(cola), CancellationToken.None);

        Assert.Equal(3, n);
        Assert.Equal(cola, _colas.Reintentada);
    }

    [Theory]
    [InlineData("entregar-aviso")]
    [InlineData("entregar-aviso_error/../../api/users")]
    [InlineData("_error")]
    public async Task Cola_que_no_es_de_fallidos_se_rechaza(string cola)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ReintentarMensajesFallidosHandler(_colas).Handle(new ReintentarMensajesFallidosCommand(cola), CancellationToken.None));

        Assert.Null(_colas.Reintentada);
    }

    private sealed class ColasEnMemoria : IColaDeFallidos
    {
        public string? Reintentada { get; private set; }

        public Task<IReadOnlyList<ColaFallidosDto>> ListarAsync(int muestra, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ColaFallidosDto>>([]);

        public Task<int> ReintentarAsync(string colaDeFallidos, CancellationToken ct)
        {
            Reintentada = colaDeFallidos;
            return Task.FromResult(3);
        }
    }
}
