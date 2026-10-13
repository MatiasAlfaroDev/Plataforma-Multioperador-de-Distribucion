namespace UltimaMilla.Application.Auth.Commands.Login;

public enum LoginStatus
{
    Exitoso,
    CredencialesInvalidas,
    OperadorNoAutorizado,
    ComercioNoAutorizado,
    TipoContextoInvalido
}

public sealed record LoginResult(
    LoginStatus Status,
    LoginResponse? Response = null);