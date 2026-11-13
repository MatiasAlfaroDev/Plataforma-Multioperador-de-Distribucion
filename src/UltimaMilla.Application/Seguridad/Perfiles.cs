namespace UltimaMilla.Application.Seguridad;

public static class Perfiles
{
    public const string AdministradorPlataforma = "AdministradorPlataforma";
    public const string AdministradorOperador = "AdministradorOperador";
    // Operador es el perfil anterior a la separación de roles; equivale al despachador.
    public const string Operador = "Operador";
    public const string Despachador = "Despachador";
    public const string OperarioDeposito = "OperarioDeposito";
    public const string AdministradorComercio = "AdministradorComercio";
    public const string Comercio = "Comercio";
    public const string Repartidor = "Repartidor";

    // Letra 3.3: el administrador configura; el despachador planifica y resuelve incidencias;
    // el operario de depósito recibe, clasifica y despacha bultos.
    public static readonly string[] Configuracion = [AdministradorOperador];
    public static readonly string[] Despacho = [AdministradorOperador, Despachador, Operador];
    public static readonly string[] Deposito = [AdministradorOperador, Despachador, Operador, OperarioDeposito];
    public static readonly string[] PersonalOperador = [AdministradorOperador, Despachador, Operador, OperarioDeposito];
    public static readonly string[] PersonalComercio = [AdministradorComercio, Comercio];
    public static readonly string[] OperadorYComercio = [.. PersonalOperador, .. PersonalComercio];
    public static readonly string[] DespachoYComercio = [.. Despacho, .. PersonalComercio];

    // Perfiles de la membresía que puede elegir cada aplicación al seleccionar operador.
    public static readonly string[] Backoffice = [AdministradorOperador, Despachador, Operador, OperarioDeposito, AdministradorPlataforma];
}
