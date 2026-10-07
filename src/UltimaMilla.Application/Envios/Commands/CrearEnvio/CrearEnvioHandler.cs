using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.CrearEnvio;

/// <summary>
/// Caso de uso "dar de alta un envío" (RF 7): valida, busca la cuenta comercial,
/// controla duplicados, crea el Envio con las reglas del dominio y confirma con el Unit of Work.
/// </summary>
public sealed class CrearEnvioHandler(
    IEnvioRepository envios,
    ICuentaComercialRepository cuentas,
    IVersionTarifariaRepository tarifas,
    IUnitOfWork unitOfWork,
    IValidator<CrearEnvioCommand> validator,
    TimeProvider reloj)
{
    public async Task<Guid> Handle(CrearEnvioCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var cuenta = await cuentas.ObtenerAsync(command.CuentaComercialId, ct)
            ?? throw new NoEncontradoException("La cuenta comercial no existe.");

        var referencia = command.ReferenciaExterna.Trim();
        if (await envios.ExisteReferenciaAsync(cuenta.Id, referencia, ct))
            throw new ConflictoException($"Ya existe un envío con la referencia '{referencia}' en esta cuenta.");
        var ahora = reloj.GetUtcNow();

        var envio = Envio.Crear(
            cuenta,
            referencia,
            Enum.Parse<Modalidad>(command.Modalidad, ignoreCase: true),
            new Destinatario(command.DestinatarioNombre, command.DestinatarioTelefono),
            new Direccion(command.Calle, command.Numero, command.Localidad, command.Departamento),
            command.Bultos.Select(b => new NuevoBulto(b.PesoKg, b.AltoCm, b.AnchoCm, b.ProfundidadCm)).ToList(),
            ahora);

        var versionTarifaria = await tarifas.ObtenerVigenteAsync(
            cuenta.OperadorId,
            command.Departamento,
            ahora,
            ct)
            ?? throw new NoEncontradoException(
                $"No existe una tarifa vigente para el departamento '{command.Departamento}'.");

        var tarifaCalculada = versionTarifaria.Calcular(
            envio.PesoTotalKg,
            envio.VolumenTotalMetrosCubicos,
            envio.Modalidad);

        envio.AplicarTarifa(
            versionTarifaria.Id,
            tarifaCalculada);
        await envios.AgregarAsync(envio, ct);
        await unitOfWork.GuardarCambiosAsync(ct);
        return envio.Id;
    }
}
