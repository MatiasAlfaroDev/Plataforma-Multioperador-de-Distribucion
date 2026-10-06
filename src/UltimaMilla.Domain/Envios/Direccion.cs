namespace UltimaMilla.Domain.Envios;

/// <summary>Objeto de valor con la dirección de entrega.</summary>
public sealed record Direccion
{
    public string Calle { get; }
    public string Numero { get; }
    public string Localidad { get; }
    public string Departamento { get; }

    public Direccion(string calle, string numero, string localidad, string departamento)
    {
        if (string.IsNullOrWhiteSpace(calle))
            throw new ArgumentException("La calle es obligatoria.", nameof(calle));
        if (string.IsNullOrWhiteSpace(localidad))
            throw new ArgumentException("La localidad es obligatoria.", nameof(localidad));
        if (string.IsNullOrWhiteSpace(departamento))
            throw new ArgumentException("El departamento es obligatorio.", nameof(departamento));

        Calle = calle.Trim();
        Numero = (numero ?? string.Empty).Trim();
        Localidad = localidad.Trim();
        Departamento = departamento.Trim();
    }

    public override string ToString() => $"{Calle} {Numero}, {Localidad}, {Departamento}";
}
