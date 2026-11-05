namespace UltimaMilla.Domain.Envios;

public sealed class IntentoEntrega
{
    public Guid Id { get; private set; }
    public Guid OperadorId { get; private set; }
    public Guid EnvioId { get; private set; }
    public int Numero { get; private set; }
    public DateTimeOffset OcurridoEn { get; private set; }
    public bool Entregado { get; private set; }
    public Guid? MotivoNoEntregaId { get; private set; }
    public string? ReferenciaEvidenciaFallida { get; private set; }
    public PruebaEntrega? PruebaEntrega { get; private set; }

    private readonly List<ResultadoBultoIntento> _resultadosBultos = [];
    public IReadOnlyCollection<ResultadoBultoIntento> ResultadosBultos =>
        _resultadosBultos;

    private IntentoEntrega() { }

    public static IntentoEntrega RegistrarEntrega(
        Guid operadorId,
        Guid envioId,
        int numero,
        PruebaEntrega prueba)
    {
        ValidarIdentificacion(operadorId, envioId, numero);
        ArgumentNullException.ThrowIfNull(prueba);

        return new IntentoEntrega
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            EnvioId = envioId,
            Numero = numero,
            OcurridoEn = prueba.OcurridoEn,
            Entregado = true,
            PruebaEntrega = prueba
        };
    }

    public static IntentoEntrega RegistrarFallo(
        Guid operadorId,
        Guid envioId,
        int numero,
        Guid motivoNoEntregaId,
        string referenciaEvidencia,
        DateTimeOffset ocurridoEn)
    {
        ValidarIdentificacion(operadorId, envioId, numero);

        if (motivoNoEntregaId == Guid.Empty)
            throw new ArgumentException("El motivo de no entrega es obligatorio.");

        if (string.IsNullOrWhiteSpace(referenciaEvidencia))
            throw new ArgumentException("La evidencia del intento fallido es obligatoria.");

        return new IntentoEntrega
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            EnvioId = envioId,
            Numero = numero,
            OcurridoEn = ocurridoEn,
            Entregado = false,
            MotivoNoEntregaId = motivoNoEntregaId,
            ReferenciaEvidenciaFallida = referenciaEvidencia.Trim()
        };
    }

    private static void ValidarIdentificacion(
        Guid operadorId, Guid envioId, int numero)
    {
        if (operadorId == Guid.Empty || envioId == Guid.Empty)
            throw new ArgumentException("El operador y el envío son obligatorios.");

        if (numero < 1)
            throw new ArgumentException("El número de intento debe ser positivo.");
    }
    public static IntentoEntrega RegistrarEntregaParcial(
        Guid operadorId,
        Guid envioId,
        int numero,
        IReadOnlyCollection<Guid> bultosEntregados,
        IReadOnlyCollection<(Guid BultoId, Guid MotivoId, string Evidencia)> bultosNoEntregados,
        PruebaEntrega prueba)
    {
        ValidarIdentificacion(operadorId, envioId, numero);

        ArgumentNullException.ThrowIfNull(bultosEntregados);
        ArgumentNullException.ThrowIfNull(bultosNoEntregados);
        ArgumentNullException.ThrowIfNull(prueba);

        if (bultosEntregados.Count == 0 ||
            bultosNoEntregados.Count == 0)
        {
            throw new ArgumentException(
                "Una entrega parcial requiere bultos entregados y no entregados.");
        }

        var identificaciones = bultosEntregados
            .Concat(bultosNoEntregados.Select(b => b.BultoId))
            .ToList();

        if (identificaciones.Any(id => id == Guid.Empty) ||
            identificaciones.Distinct().Count() != identificaciones.Count)
        {
            throw new ArgumentException(
                "Los bultos deben tener identificaciones válidas y únicas.");
        }

        var intento = new IntentoEntrega
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            EnvioId = envioId,
            Numero = numero,
            OcurridoEn = prueba.OcurridoEn,
            Entregado = false,
            PruebaEntrega = prueba
        };

        foreach (var bultoId in bultosEntregados)
        {
            intento._resultadosBultos.Add(
                ResultadoBultoIntento.CrearEntregado(
                    intento.Id,
                    bultoId));
        }

        foreach (var resultado in bultosNoEntregados)
        {
            intento._resultadosBultos.Add(
                ResultadoBultoIntento.CrearNoEntregado(
                    intento.Id,
                    resultado.BultoId,
                    resultado.MotivoId,
                    resultado.Evidencia));
        }

        return intento;
    }


}
