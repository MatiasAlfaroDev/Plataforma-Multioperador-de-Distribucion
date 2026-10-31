
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion.Commands.ActualizarReglasOperativas;

public sealed class ActualizarReglasOperativasHandler(
    IVersionReglasOperativasRepository reglas,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<Guid> Handle(
        ActualizarReglasOperativasCommand command,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();

        Guid nuevaVersionId = Guid.Empty;

        await unitOfWork.EjecutarEnTransaccionAsync(async token =>
        {
            var versionActual =
                await reglas.ObtenerVigenteParaActualizarAsync(
                    command.OperadorId,
                    ahora,
                    token);

            if (versionActual is not null)
                versionActual.FinalizarVigencia(ahora);

            var nuevaVersion = VersionReglasOperativas.Crear(
                command.OperadorId,
                command.MaximoIntentosEntrega,
                command.HorasMinimasEntreIntentos,
                command.RequierePruebaEntrega,
                command.DiasMaximosParaDevolucion,
                ahora);

            await reglas.AgregarAsync(nuevaVersion, token);

            await unitOfWork.GuardarCambiosAsync(token);

            nuevaVersionId = nuevaVersion.Id;
        }, ct);

        return nuevaVersionId;
    }
}