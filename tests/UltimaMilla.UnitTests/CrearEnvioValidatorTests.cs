using UltimaMilla.Application.Envios.Commands.CrearEnvio;

namespace UltimaMilla.UnitTests;

public class CrearEnvioValidatorTests
{
    private readonly CrearEnvioValidator _validator = new();

    private static CrearEnvioCommand ComandoValido() => new(
        Guid.NewGuid(), "PED-001", "Estandar", "Ana Pérez", null,
        "Av. Italia", "1234", "Montevideo", "Montevideo",
        [new BultoCommand(2m, 20, 20, 20)]);

    [Fact]
    public void Comando_completo_es_valido()
    {
        Assert.True(_validator.Validate(ComandoValido()).IsValid);
    }

    [Fact]
    public void Modalidad_desconocida_es_invalida()
    {
        var resultado = _validator.Validate(ComandoValido() with { Modalidad = "Express" });
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CrearEnvioCommand.Modalidad));
    }

    [Fact]
    public void Sin_bultos_es_invalido()
    {
        var resultado = _validator.Validate(ComandoValido() with { Bultos = [] });
        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Bulto_con_peso_cero_es_invalido()
    {
        var resultado = _validator.Validate(ComandoValido() with { Bultos = [new BultoCommand(0m, 20, 20, 20)] });
        Assert.False(resultado.IsValid);
    }
}
