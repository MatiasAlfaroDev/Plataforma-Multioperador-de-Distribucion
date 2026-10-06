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

        var envio = Envio.Crear(
            cuenta,
            referencia,
            Enum.Parse<Modalidad>(command.Modalidad, ignoreCase: true),
            new Destinatario(command.DestinatarioNombre, command.DestinatarioTelefono),
            new Direccion(command.Calle, command.Numero, command.Localidad, command.Departamento),
            command.Bultos.Select(b => new NuevoBulto(b.PesoKg, b.AltoCm, b.AnchoCm, b.ProfundidadCm)).ToList(),
            reloj.GetUtcNow());

        await envios.AgregarAsync(envio, ct);
        await unitOfWork.GuardarCambiosAsync(ct);
        return envio.Id;
    }
}
