
namespace UltimaMilla.Application.Envios.Commands.ImportarEnviosCsv;

public enum EstadoImportacionCsv
{
    Creado,
    YaExistente,
    Error
}

public sealed record ResultadoEnvioImportado(
    string ReferenciaExterna,
    int FilaInicial,
    EstadoImportacionCsv Estado,
    Guid? EnvioId,
    string? Mensaje);

public sealed record ResultadoImportacionCsv(
    int TotalEnvios,
    int Creados,
    int YaExistentes,
    int ConErrores,
    IReadOnlyList<ResultadoEnvioImportado> Resultados);
