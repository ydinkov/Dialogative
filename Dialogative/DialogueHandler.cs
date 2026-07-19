namespace Dialogative;

/// <summary>
/// A simplified, imperative wrapper around <see cref="Dialogue"/>.
///
/// <para>
/// Each call to <see cref="Continue"/> or <see cref="ChooseOption"/> accepts the
/// full game state and returns a self-contained <see cref="DialogueResult"/> —
/// no event subscriptions required.
/// </para>
///
/// <example>
/// <code>
/// var handler = new DialogueBuilder()
///     .FromFile("intro.yml")
///     .BuildHandler();
///
/// var result = handler.Continue(gameState);
/// while (!result.IsFinished &amp;&amp; !result.HasError)
/// {
///     Console.WriteLine($"{result.Speaker}: {result.Text}");
///
///     if (result.HasOptions)
///     {
///         var choice = PromptPlayer(result.Options);
///         result = handler.ChooseOption(choice, gameState);
///     }
///     else
///     {
///         result = handler.Continue(gameState);
///     }
/// }
/// </code>
/// </example>
/// </summary>
public sealed class DialogueHandler
{
    private readonly Dialogue _dialogue;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Wraps an existing <see cref="Dialogue"/> instance.
    /// </summary>
    public DialogueHandler(Dialogue dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue, nameof(dialogue));
        _dialogue = dialogue;
    }

    // -----------------------------------------------------------------------
    // Forwarded events
    // -----------------------------------------------------------------------

    /// <inheritdoc cref="Dialogue.SubjectChanged"/>
    public event Action<string>? SubjectChanged
    {
        add    => _dialogue.SubjectChanged += value;
        remove => _dialogue.SubjectChanged -= value;
    }

    /// <inheritdoc cref="Dialogue.SoundTriggered"/>
    public event Action<string>? SoundTriggered
    {
        add    => _dialogue.SoundTriggered += value;
        remove => _dialogue.SoundTriggered -= value;
    }

    /// <inheritdoc cref="Dialogue.MusicChanged"/>
    public event Action<string>? MusicChanged
    {
        add    => _dialogue.MusicChanged += value;
        remove => _dialogue.MusicChanged -= value;
    }

    /// <inheritdoc cref="Dialogue.MoodChanged"/>
    public event Action<string>? MoodChanged
    {
        add    => _dialogue.MoodChanged += value;
        remove => _dialogue.MoodChanged -= value;
    }

    /// <inheritdoc cref="Dialogue.OptionsProvided"/>
    public event Action<IReadOnlyList<OptionResult>>? OptionsProvided
    {
        add    => _dialogue.OptionsProvided += value;
        remove => _dialogue.OptionsProvided -= value;
    }

    /// <inheritdoc cref="Dialogue.EventTriggered"/>
    public event Action<string>? EventTriggered
    {
        add    => _dialogue.EventTriggered += value;
        remove => _dialogue.EventTriggered -= value;
    }

    /// <inheritdoc cref="Dialogue.StateUpdateRequested"/>
    public event Action<string, object?>? StateUpdateRequested
    {
        add    => _dialogue.StateUpdateRequested += value;
        remove => _dialogue.StateUpdateRequested -= value;
    }

    /// <inheritdoc cref="Dialogue.Finished"/>
    public event Action? Finished
    {
        add    => _dialogue.Finished += value;
        remove => _dialogue.Finished -= value;
    }

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    public string Title         => _dialogue.Title;
    public bool   IsFinished    => _dialogue.IsFinished;
    public bool   AwaitingChoice => _dialogue.AwaitingChoice;

    // -----------------------------------------------------------------------
    // Methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Advances the dialogue to the next line.
    /// </summary>
    /// <param name="gameState">
    /// The current game state as a key → value dictionary used for PowerFX
    /// condition evaluation. Pass an empty dictionary when the dialogue has
    /// no conditions.
    /// </param>
    /// <returns>
    /// A <see cref="DialogueResult"/> containing the spoken text, any options,
    /// side-effect fields (speaker, mood, sound, music, event, state change),
    /// and status flags (<see cref="DialogueResult.IsFinished"/>,
    /// <see cref="DialogueResult.HasError"/>).
    /// </returns>
    public DialogueResult Continue(Dictionary<string, object> gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState, nameof(gameState));
        return _dialogue.Continue(gameState);
    }

    /// <summary>
    /// Selects a player option and advances to the resulting line.
    /// </summary>
    /// <param name="optionText">
    /// The exact text of the option (case-insensitive) as returned in
    /// the previous <see cref="DialogueResult.Options"/> list.
    /// </param>
    /// <param name="gameState">
    /// The current game state used for condition evaluation.
    /// </param>
    /// <returns>
    /// A <see cref="DialogueResult"/> for the line that follows the chosen option.
    /// Returns an error result when the option is not valid or not available.
    /// </returns>
    public DialogueResult ChooseOption(string optionText, Dictionary<string, object> gameState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionText, nameof(optionText));
        ArgumentNullException.ThrowIfNull(gameState, nameof(gameState));
        return _dialogue.ChooseOption(optionText, gameState);
    }

    /// <summary>Resets the dialogue to its starting position.</summary>
    public void Reset() => _dialogue.Reset();
}
