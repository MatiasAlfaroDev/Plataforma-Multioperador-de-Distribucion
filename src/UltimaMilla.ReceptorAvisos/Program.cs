using System.Security.Cryptography;
using System.Text;

// Receptor de prueba de los avisos a comercios (RNF 6.9). Hace de sistema de un comercio:
// verifica la firma HMAC por su cuenta, sin referenciar la solución, y simula fallas.
// Modos disponibles:
// - ok: responde 200 siempre
// - falla: responde 500
// - intermitente: responde 503 los dos primeros intentos, luego 200
// - lento: retrasa 15 segundos antes de responder (más que el timeout de 10s)
//
// Ejemplo de uso:
// curl -X POST http://receptor.localhost:8000/modo/falla
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var secreto = app.Configuration["Receptor:Secreto"] ?? throw new InvalidOperationException("Falta Receptor:Secreto.");
var estado = new EstadoReceptor(app.Configuration["Receptor:Modo"] ?? "ok");

// RF 36: /avisos recibe el contrato v1 (el de siempre) y /avisos/v2 el que agrega los bultos.
app.MapPost("/avisos", (HttpRequest request) => RecibirAsync(request, "v1"));
app.MapPost("/avisos/v2", (HttpRequest request) => RecibirAsync(request, "v2"));

async Task<IResult> RecibirAsync(HttpRequest request, string contrato)
{
    using var lector = new StreamReader(request.Body);
    var cuerpo = await lector.ReadToEndAsync();

    var firma = request.Headers["X-UltimaMilla-Firma"].ToString();
    var entrega = request.Headers["X-UltimaMilla-Entrega"].ToString();
    var evento = $"{request.Headers["X-UltimaMilla-Evento"]} ({contrato})";

    var esperada = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(cuerpo))).ToLowerInvariant();

    // Comparación en tiempo constante, para no filtrar la firma por el tiempo de respuesta.
    if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(firma), Encoding.ASCII.GetBytes(esperada)))
    {
        estado.Registrar(entrega, evento, "firma inválida", cuerpo);
        return Results.Unauthorized();
    }

    var intento = estado.ContarIntento(entrega);

    switch (estado.Modo)
    {
        case "falla":
            estado.Registrar(entrega, evento, $"500 (intento {intento})", cuerpo);
            return Results.StatusCode(500);
        case "intermitente" when intento <= 2:
            estado.Registrar(entrega, evento, $"503 (intento {intento})", cuerpo);
            return Results.StatusCode(503);
        case "lento":
            await Task.Delay(TimeSpan.FromSeconds(15)); // más que el timeout de 10 s de la plataforma
            break;
    }

    // Idempotencia del lado del comercio: un reintento de un aviso ya recibido no se procesa dos veces.
    var duplicado = !estado.MarcarRecibido(entrega);
    estado.Registrar(entrega, evento, duplicado ? "200 (duplicado, ignorado)" : $"200 (intento {intento})", cuerpo);
    return Results.Ok();
}

app.MapPost("/modo/{modo}", (string modo) =>
{
    if (EstadoReceptor.Modos.Contains(modo))
    {
        estado.Modo = modo;
        return Results.Ok(new { modo });
    }
    return Results.BadRequest(new { error = $"Modos válidos: {string.Join(", ", EstadoReceptor.Modos)}" });
});

app.MapGet("/", () => Results.Ok(new { modo = estado.Modo, recibidos = estado.Recibidos() }));

app.Run();

sealed class EstadoReceptor(string modoInicial)
{
    public static readonly string[] Modos = ["ok", "falla", "intermitente", "lento"];
    public string Modo { get; set; } = modoInicial;

    private readonly Dictionary<string, int> _intentos = [];
    private readonly HashSet<string> _recibidos = [];
    private readonly LinkedList<RegistroAviso> _registro = new();
    private readonly object _lock = new();

    public int ContarIntento(string entrega)
    {
        lock (_lock)
        {
            return _intentos[entrega] = _intentos.GetValueOrDefault(entrega) + 1;
        }
    }

    public bool MarcarRecibido(string entrega)
    {
        lock (_lock)
        {
            return _recibidos.Add(entrega);
        }
    }

    public void Registrar(string entrega, string evento, string resultado, string cuerpo)
    {
        lock (_lock)
        {
            _registro.AddFirst(new RegistroAviso(DateTimeOffset.UtcNow, entrega, evento, resultado, cuerpo));
            if (_registro.Count > 50)
                _registro.RemoveLast();
        }
    }

    public IReadOnlyList<RegistroAviso> Recibidos()
    {
        lock (_lock)
        {
            return _registro.ToList();
        }
    }
}

sealed record RegistroAviso(DateTimeOffset RecibidoEn, string Entrega, string Evento, string Resultado, string Cuerpo);
