using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace UltimaMilla.Api.Seguridad;

/// <summary>
/// Token del enlace público de un retiro (RF 41). Lleva operador y retiro cifrados con Data Protection
/// (las claves están en Redis, así que cualquier réplica lo lee) y vence a los 30 días.
/// </summary>
public static class TokenRetiro
{
    public const int VigenciaDias = 30;
    private const string Proposito = "UltimaMilla.RetiroSeguimiento.v1";

    public static string Proteger(IDataProtectionProvider proveedor, Guid operadorId, Guid retiroId) =>
        proveedor.CreateProtector(Proposito).ToTimeLimitedDataProtector()
            .Protect($"{operadorId:N}:{retiroId:N}", TimeSpan.FromDays(VigenciaDias));

    /// <summary>Operador y retiro del token, o null si no es válido o venció.</summary>
    public static (Guid OperadorId, Guid RetiroId)? Leer(IDataProtectionProvider proveedor, string token)
    {
        string contenido;
        try
        {
            contenido = proveedor.CreateProtector(Proposito).ToTimeLimitedDataProtector().Unprotect(token);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }

        var partes = contenido.Split(':');
        return partes.Length == 2 &&
               Guid.TryParseExact(partes[0], "N", out var operadorId) &&
               Guid.TryParseExact(partes[1], "N", out var retiroId)
            ? (operadorId, retiroId)
            : null;
    }
}
