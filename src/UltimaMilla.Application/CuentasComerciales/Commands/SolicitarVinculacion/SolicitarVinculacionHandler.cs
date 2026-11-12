using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.CuentasComerciales.Commands.SolicitarVinculacion;

public sealed class SolicitarVinculacionHandler(
    ICuentaComercialRepository cuentas,
    IOperadorRepository operadores,
    IUnitOfWork unitOfWork,
    ITenantContext tenant)
{
    public async Task<Guid> Handle(
        SolicitarVinculacionCommand command,
        CancellationToken ct)
    {
        // 1. Verificar que el usuario tenga un comercio seleccionado.
        if (!tenant.EstaAutenticado || !tenant.ComercioId.HasValue)
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión y seleccionar un comercio.");

        // 2. Solo el administrador del comercio puede solicitar vínculos.
        if (tenant.Perfil != Perfiles.AdministradorComercio)
            throw new UnauthorizedAccessException(
                "Solo el administrador del comercio puede solicitar vinculaciones.");

        // 3. Validar el operador seleccionado.
        if (command.OperadorId == Guid.Empty)
            throw new ArgumentException(
                "Debe seleccionar un operador válido.");

        var operador = await operadores.ObtenerAsync(
            command.OperadorId, ct);

        if (operador is null)
            throw new KeyNotFoundException(
                "El operador seleccionado no existe.");

        var comercioId = tenant.ComercioId.Value;

        // 4. Verificar que no exista una vinculación previa.
        var existente =
            await cuentas.ObtenerPorOperadorYComercioAsync(
                command.OperadorId,
                comercioId,
                ct);

        if (existente is not null)
            throw new InvalidOperationException(
                "Ya existe una vinculación o solicitud con este operador.");
        
        if (command.DireccionRecepcion is null)
            throw new ArgumentException(
                "La dirección de recepción es obligatoria.");

        var direccion = new Direccion(
            command.DireccionRecepcion.Calle,
            command.DireccionRecepcion.Numero,
            command.DireccionRecepcion.Localidad,
            command.DireccionRecepcion.Departamento);

        // 5. Crear la solicitud pendiente.
        var solicitud = CuentaComercial.Solicitar(
            Guid.NewGuid(),
            command.OperadorId,
            comercioId);

        solicitud.ConfigurarDireccionRecepcion(direccion);
        // 6. Guardar en la base de datos.
        await cuentas.AgregarAsync(solicitud, ct);
        await unitOfWork.GuardarCambiosAsync(ct);

        return solicitud.Id;
    }
}