using UltimaMilla.Web.Portal.Components;
using UltimaMilla.Web.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "UltimaMilla.Portal.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";

        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = false;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Blazor InteractiveServer (informe 6.1). Con más de una réplica hará falta
// Data Protection y backplane de SignalR en Redis; con una sola no.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<EnviosApiClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/"));

builder.Services.AddHttpClient<AuthApiClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/auth/login", async (
    HttpContext contexto,
    AuthApiClient authApi,
    IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(contexto);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest(
            "La solicitud no es válida. Recargá la página e intentá nuevamente.");
    }

    var formulario = await contexto.Request.ReadFormAsync();

    var email = formulario["Email"].ToString();
    var password = formulario["Password"].ToString();

    if (string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password))
    {
        return Results.BadRequest("El correo y la contraseña son obligatorios.");
    }

    var resultado = await authApi.LoginAsync(new LoginModel
    {
        Email = email,
        Password = password,
        ComercioId = null
    });

    if (!resultado.Exito || resultado.Datos is null)
    {
        return Results.Redirect("/login?error=credenciales");
    }

    var usuario = resultado.Datos.Usuario;

    if (usuario.TipoContexto != "COMERCIO" ||
        usuario.ComercioId is null)
    {
        return Results.Forbid();
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new(ClaimTypes.Name, usuario.NombreCompleto),
        new(ClaimTypes.Email, usuario.Email),
        new(ClaimTypes.Role, usuario.Rol),
        new("tipo_contexto", usuario.TipoContexto),
        new("comercio_id", usuario.ComercioId.Value.ToString())
    };

    var identidad = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme);

    var principal = new ClaimsPrincipal(identidad);

    var propiedades = new AuthenticationProperties
{
    IsPersistent = false,
    ExpiresUtc = resultado.Datos.ExpiresAt,
    AllowRefresh = false
};

propiedades.StoreTokens(
[
    new AuthenticationToken
    {
        Name = "access_token",
        Value = resultado.Datos.AccessToken
    }
]);

await contexto.SignInAsync(
    CookieAuthenticationDefaults.AuthenticationScheme,
    principal,
    propiedades);

    return Results.Redirect("/");
})
.AllowAnonymous();  

app.Run();
