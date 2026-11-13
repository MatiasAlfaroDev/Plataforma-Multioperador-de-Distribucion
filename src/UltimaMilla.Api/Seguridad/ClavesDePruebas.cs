using System.Security.Cryptography;
using System.Text;

namespace UltimaMilla.Api.Seguridad;

/// <summary>
/// Clave del ambiente de pruebas de la API pública. No se guarda: se deriva de la clave real con HMAC.
/// Formato: <c>ulm_test_{id del vínculo}_{firma}</c>. La firma usa como secreto el hash de la clave real, así que
/// generar una clave real nueva invalida la de pruebas anterior, y quien obtenga la de pruebas no puede deducir la real.
/// </summary>
public static class ClavesDePruebas
{
    public const string Prefijo = "ulm_test_";

    private const int LargoId = 32;     // Guid en formato "N"
    private const int LargoFirma = 64;  // SHA-256 en hexadecimal

    public static string Generar(Guid vinculoId, string claveApiHash) =>
        $"{Prefijo}{vinculoId:N}_{Firmar(vinculoId, claveApiHash)}";

    public static bool EsClaveDePruebas(string clave) =>
        clave.StartsWith(Prefijo, StringComparison.Ordinal);

    /// <summary>Separa el id del vínculo y la firma; no comprueba que la firma sea correcta.</summary>
    public static bool TryLeer(string clave, out Guid vinculoId, out string firma)
    {
        vinculoId = Guid.Empty;
        firma = string.Empty;

        if (!EsClaveDePruebas(clave))
            return false;

        var resto = clave[Prefijo.Length..];

        if (resto.Length != LargoId + 1 + LargoFirma || resto[LargoId] != '_')
            return false;

        if (!Guid.TryParseExact(resto[..LargoId], "N", out vinculoId))
            return false;

        firma = resto[(LargoId + 1)..];
        return true;
    }

    public static bool EsValida(Guid vinculoId, string firma, string claveApiHash)
    {
        var esperada = Encoding.UTF8.GetBytes(Firmar(vinculoId, claveApiHash));
        var recibida = Encoding.UTF8.GetBytes(firma.ToLowerInvariant());

        return esperada.Length == recibida.Length &&
               CryptographicOperations.FixedTimeEquals(esperada, recibida);
    }

    private static string Firmar(Guid vinculoId, string claveApiHash) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(claveApiHash),
            Encoding.UTF8.GetBytes($"pruebas:{vinculoId:N}")))
        .ToLowerInvariant();
}