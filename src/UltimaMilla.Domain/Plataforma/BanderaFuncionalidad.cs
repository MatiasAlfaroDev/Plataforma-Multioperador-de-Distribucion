namespace UltimaMilla.Domain.Plataforma;

/// <summary>
/// Bandera de funcionalidad (aclaración 3.2): si una funcionalidad está disponible para un operador.
/// La administra quien ofrece la plataforma, no el operador, por eso no es una entidad con tenant.
/// Sin fila para un operador, la funcionalidad está habilitada (lo que había antes de las banderas).
/// </summary>
public sealed class BanderaFuncionalidad
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public Guid OperadorId { get; private set; }
    public bool Habilitada { get; private set; }
    public string ModificadoPor { get; private set; } = string.Empty;
    public DateTimeOffset ModificadoEn { get; private set; }

    private BanderaFuncionalidad() { }

    public static BanderaFuncionalidad Crear(string nombre, Guid operadorId) => new()
    {
        Id = Guid.NewGuid(),
        Nombre = string.IsNullOrWhiteSpace(nombre) ? throw new ArgumentException("El nombre es obligatorio.") : nombre,
        OperadorId = operadorId == Guid.Empty ? throw new ArgumentException("El operador es obligatorio.") : operadorId,
        Habilitada = true
    };

    /// <summary>Cambia el valor y devuelve el registro de auditoría: quién y cuándo.</summary>
    public CambioBandera Cambiar(bool habilitada, string modificadoPor, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(modificadoPor))
            throw new ArgumentException("Hay que registrar quién modifica la bandera.");

        Habilitada = habilitada;
        ModificadoPor = modificadoPor.Trim();
        ModificadoEn = ahora;

        return new CambioBandera(Guid.NewGuid(), Nombre, OperadorId, habilitada, ModificadoPor, ahora);
    }
}

/// <summary>Historial de cambios de las banderas (aclaración 3.2: quién modificó cada bandera y cuándo).</summary>
public sealed record CambioBandera(
    Guid Id,
    string Nombre,
    Guid OperadorId,
    bool Habilitada,
    string ModificadoPor,
    DateTimeOffset ModificadoEn);
