namespace UltimaMilla.Application.Avisos.Commands.ReintentarAvisosFallidos;

/// <summary>
/// Reencola los avisos fallidos del operador actual cuya espera ya venció; lo dispara la tarea programada del Worker.
/// </summary>
public sealed record ReintentarAvisosFallidosCommand;
