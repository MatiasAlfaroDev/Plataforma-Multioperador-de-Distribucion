using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion.Commands.ActualizarReglasOperativas;

public sealed class ActualizarReglasOperativasHandler(
    IVersionReglasOperativasRepository reglas,
    IMotivoNoEntregaRepository motivosRepository,
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

            IReadOnlyList<MotivoNoEntregaItem> catalogo;

            if (command.Motivos is not null)
            {
                catalogo = command.Motivos;
            }
            else if (versionActual is not null)
            {
                var existentes =
                    await motivosRepository.ListarPorVersionAsync(
                        command.OperadorId,
                        versionActual.Id,
                        token);

                catalogo = existentes
                    .Select(m => new MotivoNoEntregaItem(
                        m.Codigo,
                        m.Descripcion,
                        m.RequiereDevolucion))
                    .ToList();
            }
            else
            {
                throw new ArgumentException(
                    "La primera configuración requiere motivos de no entrega.");
            }

            ValidarCatalogo(catalogo);

            var nuevaVersion = VersionReglasOperativas.Crear(
                command.OperadorId,
                command.MaximoIntentosEntrega,
                command.HorasMinimasEntreIntentos,
                command.RequierePruebaEntrega,
                command.DiasMaximosParaDevolucion,
                ahora);

            var nuevosMotivos = catalogo
                .Select(m => MotivoNoEntrega.Crear(
                    command.OperadorId,
                    nuevaVersion.Id,
                    m.Codigo,
                    m.Descripcion,
                    m.RequiereDevolucion))
                .ToList();

            if (versionActual is not null)
                versionActual.FinalizarVigencia(ahora);

            await reglas.AgregarAsync(nuevaVersion, token);

            await motivosRepository.AgregarRangoAsync(
                nuevosMotivos,
                token);

            await unitOfWork.GuardarCambiosAsync(token);

            nuevaVersionId = nuevaVersion.Id;
        }, ct);

        return nuevaVersionId;
    }

    private static void ValidarCatalogo(
        IReadOnlyList<MotivoNoEntregaItem> catalogo)
    {
        if (catalogo.Count == 0)
            throw new ArgumentException(
                "Debe existir al menos un motivo de no entrega.");

        if (catalogo.Any(m =>
            string.IsNullOrWhiteSpace(m.Codigo) ||
            string.IsNullOrWhiteSpace(m.Descripcion) ||
            m.Codigo.Trim().Length > 100 ||
            m.Descripcion.Trim().Length > 300))
        {
            throw new ArgumentException(
                "Los motivos deben tener código y descripción válidos.");
        }

        var codigos = catalogo
            .Select(m => m.Codigo.Trim())
            .ToList();

        if (codigos.Distinct(StringComparer.OrdinalIgnoreCase)
                   .Count() != codigos.Count)
        {
            throw new ArgumentException(
                "No puede haber motivos con códigos duplicados.");
        }
    }
}