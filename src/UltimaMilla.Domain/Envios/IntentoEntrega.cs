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
}
