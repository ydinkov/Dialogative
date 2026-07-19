using YamlDotNet.Serialization;

namespace Dialogative;

// ---------------------------------------------------------------------------
// YAML deserialization models (internal shape of the .yml file)
// ---------------------------------------------------------------------------

/// <summary>Root YAML document. Maps to the top-level keys: title + scenes.</summary>
internal sealed class DialogueModel
{
    public string Title { get; init; } = string.Empty;
    public Dictionary<string, List<DialogueLine>> Scenes { get; init; } = [];
}

/// <summary>A single entry in a scene array.</summary>
internal sealed class DialogueLine
{
    /// <summary>One or more variant texts; one is picked at random when displayed.</summary>
    public List<string> Line { get; init; } = [];

    public string? Speaker { get; init; }
    public string? Sound  { get; init; }
    public string? Music  { get; init; }
    public string? Mood   { get; init; }
    public string? Event  { get; init; }

    /// <summary>Scene to jump to after this line. Use "exit" to end the dialogue.</summary>
    public string? Goto { get; init; }

    /// <summary>PowerFX boolean expression. When false the line is skipped (or replaced by ElseLine).</summary>
    [YamlMember(Alias = "if")]
    public string? Condition { get; init; }

    /// <summary>Fallback line shown when Condition evaluates to false.</summary>
    [YamlMember(Alias = "else")]
    public DialogueLine? ElseLine { get; init; }

    /// <summary>Requests a game-state mutation when this line is displayed.</summary>
    public StateUpdate? Update { get; init; }

    public List<DialogueOption>? Options { get; init; }
}

/// <summary>A player-selectable option attached to a dialogue line.</summary>
internal sealed class DialogueOption
{
    public string Text { get; init; } = string.Empty;

    /// <summary>Scene to jump to when this option is chosen. Use "exit" to end the dialogue.</summary>
    public string? Goto { get; init; }

    public string? Event { get; init; }

    /// <summary>PowerFX expression — option is hidden unless this is true.</summary>
    [YamlMember(Alias = "show_if")]
    public string? ShowIf { get; init; }

    /// <summary>PowerFX expression — option is shown but locked while this is true.</summary>
    [YamlMember(Alias = "locked_if")]
    public string? LockedIf { get; init; }
}

/// <summary>Requests a game-state variable change.</summary>
internal sealed class StateUpdate
{
    /// <summary>Variable name to update.</summary>
    public string Set { get; init; } = string.Empty;

    /// <summary>New value (bool, number, or string). Coerced from YAML string after parse.</summary>
    public object? To { get; set; }
}
