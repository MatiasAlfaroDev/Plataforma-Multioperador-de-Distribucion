using StackExchange.Redis;

namespace UltimaMilla.Api.Seguridad;

public sealed class LimiteReprogramacionPublica(
    IConnectionMultiplexer redis)
{
    private const int MaximoPorMinuto = 5;

    private const string Script = """
        local n = redis.call('INCR', KEYS[1])
        if n == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return n
        """;

    public async Task<bool> ExcedidoAsync(
        string tokenHash)
    {
        if (!redis.IsConnected)
            return false;

        var ventana =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;

        var clave =
            $"limite:reprogramacion:{tokenHash}:{ventana}";

        try
        {
            var resultado = await redis.GetDatabase()
                .ScriptEvaluateAsync(
                    Script,
                    [new RedisKey(clave)],
                    [70]);

            return (long)resultado > MaximoPorMinuto;
        }
        catch (Exception ex) when (
            ex is RedisException or TimeoutException)
        {
            return false;
        }
    }
}