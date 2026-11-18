
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Operadores.Commands.ActualizarColorOperador;
using UltimaMilla.Domain.Organizacion;
using Xunit;

namespace UltimaMilla.UnitTests;

public sealed class ActualizarColorOperadorTests
{
    [Fact]
    public async Task ActualizarColor_ModificaSoloElOperadorIndicado()
    {
        // Arrange: dos operadores independientes.
        var operadorA = Operador.Crear(
            Guid.NewGuid(),
            "RapiEnvíos",
            "#123456",
            "rapi@example.com");

        var operadorB = Operador.Crear(
            Guid.NewGuid(),
            "Otro Operador",
            "#ABCDEF",
            "otro@example.com");

        var repositorio = new OperadoresEnMemoria();
        repositorio.Lista.Add(operadorA);
        repositorio.Lista.Add(operadorB);

        var unidadDeTrabajo = new UnidadDeTrabajoEnMemoria();

        var handler = new ActualizarColorOperadorHandler(
            repositorio,
            unidadDeTrabajo);

        // Act: actualizar solamente el operador A.
        var resultado = await handler.Handle(
            operadorA.Id,
            "#EC2618",
            CancellationToken.None);

        // Assert.
        Assert.True(resultado);
        Assert.Equal("#EC2618", operadorA.ColorPrimario);
        Assert.Equal("#ABCDEF", operadorB.ColorPrimario);
        Assert.Equal(1, unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task ActualizarColor_OperadorInexistente_NoGuardaCambios()
    {
        var repositorio = new OperadoresEnMemoria();
        var unidadDeTrabajo = new UnidadDeTrabajoEnMemoria();

        var handler = new ActualizarColorOperadorHandler(
            repositorio,
            unidadDeTrabajo);

        var resultado = await handler.Handle(
            Guid.NewGuid(),
            "#EC2618",
            CancellationToken.None);

        Assert.False(resultado);
        Assert.Equal(0, unidadDeTrabajo.Guardados);
    }

    [Theory]
    [InlineData("rojo")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("123456")]
    [InlineData("")]
    public async Task ActualizarColor_ColorInvalido_LanzaExcepcion(
        string color)
    {
        var operador = Operador.Crear(
            Guid.NewGuid(),
            "RapiEnvíos",
            "#123456",
            "rapi@example.com");

        var repositorio = new OperadoresEnMemoria();
        repositorio.Lista.Add(operador);

        var unidadDeTrabajo = new UnidadDeTrabajoEnMemoria();

        var handler = new ActualizarColorOperadorHandler(
            repositorio,
            unidadDeTrabajo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(
                operador.Id,
                color,
                CancellationToken.None));

        Assert.Equal("#123456", operador.ColorPrimario);
        Assert.Equal(0, unidadDeTrabajo.Guardados);
    }

    private sealed class OperadoresEnMemoria : IOperadorRepository
    {
        public List<Operador> Lista { get; } = [];

        public Task<IReadOnlyList<Operador>> ListarAsync(
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Operador>>(
                Lista.ToList());

        public Task<Operador?> ObtenerAsync(
            Guid id,
            CancellationToken ct) =>
            Task.FromResult(
                Lista.FirstOrDefault(o => o.Id == id));

        public Task<Operador?> ObtenerParaActualizarAsync(
            Guid id,
            CancellationToken ct) =>
            ObtenerAsync(id, ct);
    }

    private sealed class UnidadDeTrabajoEnMemoria : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(
            CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public Task EjecutarEnTransaccionAsync(
            Func<CancellationToken, Task> operacion,
            CancellationToken ct) =>
            operacion(ct);
    }
}
