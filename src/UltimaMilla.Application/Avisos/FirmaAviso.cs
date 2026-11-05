using System.Security.Cryptography;
using System.Text;

namespace UltimaMilla.Application.Avisos;

/// <summary>
/// Firma de los avisos (RNF 6.9): HMAC-SHA256 del cuerpo con el secreto de la suscripción, en hexadecimal minúscula.
/// El receptor la recalcula con el mismo secreto para verificar que el aviso viene de la plataforma y no fue alterado.
/// </summary>
public static class FirmaAviso
{
    public const string Cabecera = "X-UltimaMilla-Firma";

    /// <summary>
    /// Calcula la firma HMAC-SHA256 del cuerpo con el secreto, en formato hexadecimal minúscula.
    /// </summary>
    public static string Calcular(string secreto, string cuerpo) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(cuerpo))).ToLowerInvariant();
}
