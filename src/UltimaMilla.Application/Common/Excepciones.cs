namespace UltimaMilla.Application.Common;

/// <summary>El recurso pedido no existe. La API lo traduce a 404.</summary>
public sealed class NoEncontradoException(string mensaje) : Exception(mensaje);

/// <summary>El pedido choca con datos existentes (por ejemplo, referencia repetida). La API lo traduce a 409.</summary>
public sealed class ConflictoException(string mensaje) : Exception(mensaje);
