using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Avisos;

/// <summary>
/// Estado de una entrega de aviso: Pendiente = en cola o reintentando;
/// Fallida = el último intento falló (puede seguir reintentando o haber agotado los reintentos);
/// Entregada = el receptor respondió 2xx.
/// </summary>
public enum EstadoEntregaAviso
{
    Pendiente,
    Entregada,
    Fallida
}

/// <summary>
/// Un aviso a enviar a una suscripción por un evento concreto; es el historial que consulta el comercio (RNF 6.9).
/// </summary>
public sealed class EntregaAviso : AuditableEntity, ITenantEntity
{
    /// <summary>
    /// Máximo de caracteres permitidos en el mensaje de error.
    /// </summary>
    public const int LargoMaximoError = 500;

    /// <summary>
    /// Intentos de cada ronda: el primero más los 5 reintentos en memoria del Worker (EntregarAvisoDefinition).
    /// </summary>
    public const int IntentosPorRonda = 6;

    /// <summary>
    /// Pasado este tope el reintento programado deja de reencolarlo (ocho rondas, unas tres horas); solo queda el reenvío manual.
    /// </summary>
    public const int MaximoIntentos = IntentosPorRonda * 8;

    // Espera creciente entre rondas automáticas. La primera supera la ventana de los reintentos en memoria
    // (el mayor intervalo es de 1 min), así no se reencola un aviso que todavía está reintentando.
    private static readonly TimeSpan[] EsperasEntreRondas =
        [TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)];

    public Guid OperadorId { get; private set; }

    public Guid SuscripcionId { get; private set; }

    public Guid CuentaComercialId { get; private set; }

    /// <summary>
    /// Tipo de evento que originó el aviso (uno de <see cref="TiposAviso"/>).
    /// </summary>
    public string TipoEvento { get; private set; } = string.Empty;

    /// <summary>
    /// Identifica el evento de origen; con SuscripcionId es única, así el mismo evento procesado dos veces no genera dos avisos.
    /// </summary>
    public string ClaveEvento { get; private set; } = string.Empty;

    /// <summary>
    /// Contenido JSON del aviso que se enviará.
    /// </summary>
    public string Cuerpo { get; private set; } = string.Empty;

    /// <summary>
    /// Estado actual de la entrega del aviso.
    /// </summary>
    public EstadoEntregaAviso Estado { get; private set; }

    /// <summary>
    /// Número de intentos realizados para entregar el aviso.
    /// </summary>
    public int Intentos { get; private set; }

    /// <summary>
    /// Último mensaje de error ocurrido durante un intento de entrega.
    /// </summary>
    public string? UltimoError { get; private set; }

    /// <summary>
    /// Fecha y hora del último intento de entrega.
    /// </summary>
    public DateTimeOffset? UltimoIntentoEn { get; private set; }

    /// <summary>
    /// Fecha y hora en que se entregó el aviso exitosamente.
    /// </summary>
    public DateTimeOffset? EntregadaEn { get; private set; }

    // Constructor vacío que necesita EF Core.
    private EntregaAviso() { }

    /// <summary>
    /// Crea una nueva entrega de aviso a partir de una suscripción y datos del evento.
    /// </summary>
    public static EntregaAviso Crear(
        SuscripcionAviso suscripcion,
        string tipoEvento,
        string claveEvento,
        string cuerpo)
    {
        ArgumentNullException.ThrowIfNull(suscripcion);

        if (!suscripcion.Escucha(tipoEvento))
            throw new InvalidOperationException("La suscripción no escucha ese tipo de evento.");

        if (string.IsNullOrWhiteSpace(claveEvento) || string.IsNullOrWhiteSpace(cuerpo))
            throw new ArgumentException("La clave del evento y el cuerpo son obligatorios.");

        return new EntregaAviso
        {
            Id = Guid.NewGuid(),
            OperadorId = suscripcion.OperadorId,
            CuentaComercialId = suscripcion.CuentaComercialId,
            SuscripcionId = suscripcion.Id,
            TipoEvento = tipoEvento.Trim(),
            ClaveEvento = claveEvento.Trim(),
            Cuerpo = cuerpo.Trim(),
            Estado = EstadoEntregaAviso.Pendiente,
            Intentos = 0
        };
    }

    /// <summary>
    /// Registra un éxito en la entrega del aviso.
    /// </summary>
    public void RegistrarExito(DateTimeOffset ahora)
    {
        Intentos++;
        UltimoIntentoEn = ahora;
        EntregadaEn = ahora;
        UltimoError = null;
        Estado = EstadoEntregaAviso.Entregada;
    }

    /// <summary>
    /// Registra un fallo en la entrega del aviso.
    /// </summary>
    public void RegistrarFallo(string error, DateTimeOffset ahora)
    {
        Intentos++;
        UltimoIntentoEn = ahora;
        UltimoError = string.IsNullOrWhiteSpace(error) ? "Error desconocido." : error[..Math.Min(error.Length, LargoMaximoError)];
        Estado = EstadoEntregaAviso.Fallida;
    }

    /// <summary>
    /// Fallida y con todos los intentos automáticos gastados.
    /// </summary>
    public bool Agotada => Estado == EstadoEntregaAviso.Fallida && Intentos >= MaximoIntentos;

    /// <summary>
    /// Si el reintento programado ya puede reencolarlo: fallido, sin agotar y con la espera de su ronda cumplida.
    /// </summary>
    public bool PuedeReintentar(DateTimeOffset ahora)
    {
        if (Estado != EstadoEntregaAviso.Fallida || Agotada || UltimoIntentoEn is null)
            return false;

        var ronda = Math.Max(Intentos / IntentosPorRonda, 1);
        var espera = EsperasEntreRondas[Math.Min(ronda, EsperasEntreRondas.Length) - 1];
        return ahora - UltimoIntentoEn.Value >= espera;
    }

    /// <summary>
    /// Reencola el aviso para reintentar la entrega.
    /// </summary>
    public void Reencolar()
    {
        if (Estado != EstadoEntregaAviso.Fallida)
            throw new InvalidOperationException("Solo se puede reenviar un aviso fallido.");

        Estado = EstadoEntregaAviso.Pendiente;
    }
}
