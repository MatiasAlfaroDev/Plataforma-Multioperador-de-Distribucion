
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Configuracion;

public class ZonaCobertura : AuditableEntity, ITenantEntity
{
    private readonly List<LocalidadCubierta> _localidades = [];
    private readonly List<FranjaEntrega> _franjas = [];

    public Guid OperadorId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Departamento { get; private set; } = string.Empty;
    public bool Activa { get; private set; }

    public int? MinutosTrasladoEstimado { get; private set; }

    public IReadOnlyCollection<LocalidadCubierta> Localidades =>
        _localidades.AsReadOnly();

    public IReadOnlyCollection<FranjaEntrega> Franjas =>
        _franjas.AsReadOnly();

    private ZonaCobertura() { }

    public static ZonaCobertura Crear(
        Guid operadorId,
        string nombre,
        string departamento)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre de la zona es obligatorio.");

        if (string.IsNullOrWhiteSpace(departamento))
            throw new ArgumentException("El departamento es obligatorio.");

        return new ZonaCobertura
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            Nombre = nombre.Trim(),
            Departamento = departamento.Trim(),
            Activa = true
        };
    }

    // Una zona puede cubrir una localidad entera.
    public void AgregarLocalidad(string nombre)
    {
        AgregarCobertura(nombre, null);
    }

    // O solamente un sector de esa localidad.
    public void AgregarSector(string localidad, string sector)
    {
        if (string.IsNullOrWhiteSpace(sector))
            throw new ArgumentException("El sector es obligatorio.");

        AgregarCobertura(localidad, sector.Trim());
    }

    private void AgregarCobertura(string localidad, string? sector)
    {
        if (string.IsNullOrWhiteSpace(localidad))
            throw new ArgumentException("La localidad es obligatoria.");

        localidad = localidad.Trim();

        var existentes = _localidades
            .Where(x => string.Equals(
                x.Nombre,
                localidad,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sector is null)
        {
            if (existentes.Count > 0)
                throw new InvalidOperationException(
                    "La localidad ya tiene cobertura en esta zona.");
        }
        else
        {
            if (existentes.Any(x =>
                x.Sector is null ||
                string.Equals(
                    x.Sector,
                    sector,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "La zona ya cubre ese sector o toda la localidad.");
            }
        }

        _localidades.Add(
            LocalidadCubierta.Crear(Id, localidad, sector));
    }

    public bool CubreDireccion(string localidad, string? sector)
    {
        if (!Activa || string.IsNullOrWhiteSpace(localidad))
            return false;

        return _localidades.Any(x =>
            string.Equals(
                x.Nombre,
                localidad.Trim(),
                StringComparison.OrdinalIgnoreCase)
            &&
            (x.Sector is null ||
             (!string.IsNullOrWhiteSpace(sector) &&
              string.Equals(
                  x.Sector,
                  sector.Trim(),
                  StringComparison.OrdinalIgnoreCase))));
    }

    public void AgregarFranja(TimeOnly desde, TimeOnly hasta)
    {
        if (desde >= hasta)
            throw new ArgumentException(
                "La hora de inicio debe ser anterior a la de fin.");

        if (_franjas.Any(x =>
            desde < x.HoraHasta && hasta > x.HoraDesde))
            throw new InvalidOperationException(
                "La franja se superpone con otra de esta zona.");

        _franjas.Add(FranjaEntrega.Crear(Id, desde, hasta));
    }

    public bool OfreceFranja(TimeOnly desde, TimeOnly hasta)
    {
        return Activa && _franjas.Any(x =>
            x.HoraDesde == desde &&
            x.HoraHasta == hasta);
    }

    public void ConfigurarTiempoEstimado(int minutosEntreParadas)
    {
        if (minutosEntreParadas <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(minutosEntreParadas),
                "El tiempo estimado entre paradas debe ser positivo.");

        MinutosTrasladoEstimado = minutosEntreParadas;
    }

    /// <summary>
    /// Edición desde el Backoffice (RF 2): cambia el nombre y el departamento, y reemplaza
    /// las coberturas y las franjas. Primero valida todo sobre una copia: si algo no es
    /// válido, lanza la excepción y la zona queda exactamente como estaba.
    /// </summary>
    public void Editar(
        string nombre,
        string departamento,
        IReadOnlyList<(string Localidad, string? Sector)> coberturas,
        IReadOnlyList<(TimeOnly Desde, TimeOnly Hasta)> franjas)
    {
        // Las mismas reglas que al crear: nombre, departamento, coberturas sin
        // repetir y franjas sin superponer. Si algo falla, falla acá.
        var copia = Crear(OperadorId, nombre, departamento);
        copia.Cargar(coberturas, franjas);

        Nombre = copia.Nombre;
        Departamento = copia.Departamento;

        _localidades.Clear();
        _franjas.Clear();
        Cargar(coberturas, franjas);
    }

    private void Cargar(
        IReadOnlyList<(string Localidad, string? Sector)> coberturas,
        IReadOnlyList<(TimeOnly Desde, TimeOnly Hasta)> franjas)
    {
        if (coberturas is null || coberturas.Count == 0)
            throw new ArgumentException("La zona debe tener al menos una cobertura.");

        if (franjas is null || franjas.Count == 0)
            throw new ArgumentException("La zona debe tener al menos una franja.");

        foreach (var (localidad, sector) in coberturas)
        {
            if (string.IsNullOrWhiteSpace(sector))
                AgregarLocalidad(localidad);
            else
                AgregarSector(localidad, sector);
        }

        foreach (var (desde, hasta) in franjas)
            AgregarFranja(desde, hasta);
    }

    public void Desactivar() => Activa = false;

    public void Activar() => Activa = true;
}
