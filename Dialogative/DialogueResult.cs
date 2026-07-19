namespace Dialogative;

// ---------------------------------------------------------------------------
// Public result types returned by DialogueHandler and Dialogue
// ---------------------------------------------------------------------------

/// <summary>
/// The result of a single <see cref="DialogueHandler.Continue"/> or
/// <see cref="DialogueHandler.ChooseOption"/> call.
/// </summary>
public sealed class DialogueResult
{
    // ---- Static helpers -----------------------------------------------

    /// <summary>A finished result with no text or options.</summary>
    public static DialogueResult Finished { get; } = new() { IsFinished = true };

    /// <summary>Creates an error result with no text.</summary>
    public static DialogueResult FromError(string error) =>
        new() { Error = error ?? throw new ArgumentNullException(nameof(error)) };

    // ---- Dialogue content ---------------------------------------------

    /// <summary>The line of text spoken. Null only when the dialogue has finished.</summary>
    public string? Text { get; init; }

    /// <summary>Speaker name, if declared on this line.</summary>
    public string? Speaker { get; init; }

    /// <summary>Mood tag, if declared on this line.</summary>
    public string? Mood { get; init; }

    /// <summary>Sound-effect name to play, if declared on this line.</summary>
    public string? Sound { get; init; }

    /// <summary>Music track to switch to, if declared on this line.</summary>
    public string? Music { get; init; }

    /// <summary>Arbitrary event string emitted by this line.</summary>
    public string? Event { get; init; }

    /// <summary>Visible (possibly locked) options the player can choose from.</summary>
    public IReadOnlyList<OptionResult> Options { get; init; } = [];

    /// <summary>A game-state mutation requested by this line, or null.</summary>
    public StateChange? StateChange { get; init; }

    // ---- Status -------------------------------------------------------

    /// <summary>True when the dialogue has reached its end.</summary>
    public bool IsFinished { get; init; }

    /// <summary>Non-null when a recoverable problem occurred (e.g. invalid option chosen).</summary>
    public string? Error { get; init; }

    /// <summary>Convenience: true when <see cref="Error"/> is non-null.</summary>
    public bool HasError => Error is not null;

    /// <summary>True when the caller must call ChooseOption before calling Continue again.</summary>
    public bool HasOptions => Options.Count > 0;
}

/// <summary>One option presented to the player.</summary>
public sealed class OptionResult
{
    /// <summary>Display text for this option.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// When true the option is visible but unavailable — the game should
    /// show it in a disabled / greyed-out state.
    /// </summary>
    public bool IsLocked { get; init; }
}

/// <summary>A requested mutation to the game state.</summary>
public sealed class StateChange
{
    /// <summary>Name of the game-state variable to update.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>New value (bool, number, or string as declared in the YAML).</summary>
    public object? Value { get; init; }
}
