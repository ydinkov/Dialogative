namespace Dialogative;

/// <summary>
/// Thrown when a dialogue YAML file cannot be found, parsed, or when a
/// PowerFX expression is syntactically invalid. These represent programmer
/// errors (bad input) that should be caught during development.
/// </summary>
public sealed class DialogueException : Exception
{
    public DialogueException(string message) : base(message) { }
    public DialogueException(string message, Exception innerException) : base(message, innerException) { }
}
