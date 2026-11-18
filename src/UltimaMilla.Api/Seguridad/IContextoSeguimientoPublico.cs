namespace UltimaMilla.Api.Seguridad;

public interface IContextoSeguimientoPublico
{
    Task<Guid?> ValidarTokenYSeleccionarOperadorAsync(
        string token,
        CancellationToken ct);
}