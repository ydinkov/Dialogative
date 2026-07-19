namespace Dialogative;

/// <summary>
/// Core dialogue engine. Manages progression through a YAML dialogue tree,
/// evaluating PowerFX conditions against the game state.
///
/// <para>
/// Use <see cref="DialogueBuilder"/> to construct instances.
/// Use <see cref="DialogueHandler"/> for the simplified Continue/ChooseOption API.
/// </para>
/// </summary>
public sealed class Dialogue
{
    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private readonly DialogueModel _model;
    private readonly Func<Dictionary<string, object>> _stateProvider;
    private readonly Random _random;

    private string _nextSceneName;
    private int    _nextLineIndex;

    private string?       _currentSceneName;
    private int           _currentLineIndex;
    private DialogueLine? _currentLine;

    private bool _awaitingChoice;
    private bool _finished;

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Fired when the speaking subject changes.</summary>
    public event Action<string>? SubjectChanged;

    /// <summary>Fired when a sound effect should play.</summary>
    public event Action<string>? SoundTriggered;

    /// <summary>Fired when background music should change.</summary>
    public event Action<string>? MusicChanged;

    /// <summary>Fired when the speaker's mood changes.</summary>
    public event Action<string>? MoodChanged;

    /// <summary>Fired when options are available for the player to choose.</summary>
    public event Action<IReadOnlyList<OptionResult>>? OptionsProvided;

    /// <summary>Fired when an arbitrary game event is triggered.</summary>
    public event Action<string>? EventTriggered;

    /// <summary>Fired when the dialogue requests a game-state mutation.</summary>
    public event Action<string, object?>? StateUpdateRequested;

    /// <summary>Fired when the dialogue reaches its end.</summary>
    public event Action? Finished;

    // -----------------------------------------------------------------------
    // Constructor (internal — use DialogueBuilder)
    // -----------------------------------------------------------------------

    internal Dialogue(
        DialogueModel model,
        Func<Dictionary<string, object>> stateProvider,
        Random? random = null)
    {
        _model         = model;
        _stateProvider = stateProvider;
        _random        = random ?? Random.Shared;

        _nextSceneName = _model.Scenes.Keys.First();
        _nextLineIndex = 0;
        _currentSceneName = null;
        _currentLineIndex = 0;
    }

    // -----------------------------------------------------------------------
    // Public properties
    // -----------------------------------------------------------------------

    public string Title => _model.Title;

    /// <summary>True once the dialogue has reached its end.</summary>
    public bool IsFinished => _finished;

    /// <summary>True when the player must call ChooseOption before continuing.</summary>
    public bool AwaitingChoice => _awaitingChoice;

    // -----------------------------------------------------------------------
    // Public methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Advances the dialogue to the next line and fires relevant events.
    /// </summary>
    /// <param name="gameState">
    /// Current game state used for condition evaluation.
    /// Falls back to the configured state provider when null.
    /// </param>
    /// <returns>
    /// A <see cref="DialogueResult"/> describing the current line.
    /// <see cref="DialogueResult.IsFinished"/> is true when the dialogue ends.
    /// <see cref="DialogueResult.HasError"/> is true for recoverable problems.
    /// </returns>
    public DialogueResult Continue(Dictionary<string, object>? gameState = null)
    {
        if (_finished)
            return DialogueResult.Finished;

        if (_awaitingChoice)
            return DialogueResult.FromError(
                "The dialogue is waiting for an option choice. Call ChooseOption() before calling Continue().");

        var state = gameState ?? _stateProvider();

        // Advance to the next position.
        _currentSceneName = _nextSceneName;
        _currentLineIndex = _nextLineIndex;

        if (_currentSceneName == "exit")
            return MarkFinished();

        if (!_model.Scenes.TryGetValue(_currentSceneName, out var scene))
        {
            _finished = true;
            return DialogueResult.FromError(
                $"Scene '{_currentSceneName}' does not exist in dialogue '{_model.Title}'.");
        }

        // Resolve the line at the current index (honouring if/else/skip logic).
        var resolved = FindValidLine(scene, _currentSceneName, _currentLineIndex, state);
        if (resolved is null)
            return MarkFinished();

        var (line, effectiveIndex) = resolved.Value;
        _currentLine      = line;
        _currentLineIndex = effectiveIndex;

        // Fire per-line events.
        if (line.Speaker is not null) SubjectChanged?.Invoke(line.Speaker);
        if (line.Sound   is not null) SoundTriggered?.Invoke(line.Sound);
        if (line.Music   is not null) MusicChanged?.Invoke(line.Music);
        if (line.Mood    is not null) MoodChanged?.Invoke(line.Mood);
        if (line.Event   is not null) EventTriggered?.Invoke(line.Event);
        if (line.Update  is not null) StateUpdateRequested?.Invoke(line.Update.Set, line.Update.To);

        // Pre-calculate next position.
        SetNextPosition(line, _currentSceneName, _currentLineIndex, state);

        // Build visible options (filtered by show_if / locked_if).
        List<OptionResult> options = [];
        if (line.Options is { Count: > 0 })
        {
            options = BuildOptionResults(line.Options, state);
            _awaitingChoice = true;
            OptionsProvided?.Invoke(options);
        }

        return new DialogueResult
        {
            Text        = PickText(line.Line),
            Speaker     = line.Speaker,
            Sound       = line.Sound,
            Music       = line.Music,
            Mood        = line.Mood,
            Event       = line.Event,
            StateChange = line.Update is not null
                ? new StateChange { Key = line.Update.Set, Value = line.Update.To }
                : null,
            Options = options
        };
    }

    /// <summary>
    /// Chooses a player option and advances to the resulting line.
    /// </summary>
    /// <param name="optionText">Exact text of the option to choose (case-insensitive).</param>
    /// <param name="gameState">
    /// Current game state. Falls back to the configured state provider when null.
    /// </param>
    public DialogueResult ChooseOption(string? optionText, Dictionary<string, object>? gameState = null)
    {
        if (_finished)
            return DialogueResult.Finished;

        if (!_awaitingChoice)
            return DialogueResult.FromError(
                "The dialogue is not currently waiting for an option choice.");

        if (string.IsNullOrWhiteSpace(optionText))
            return DialogueResult.FromError("Option text must not be null or empty.");

        if (_currentLine?.Options is null or { Count: 0 })
            return DialogueResult.FromError("Internal error: awaiting choice but current line has no options.");

        var state = gameState ?? _stateProvider();

        // Find the chosen option.
        var option = _currentLine.Options.FirstOrDefault(o =>
            string.Equals(o.Text, optionText, StringComparison.OrdinalIgnoreCase));

        if (option is null)
            return DialogueResult.FromError(
                $"'{optionText}' is not a valid option. Available: " +
                string.Join(", ", _currentLine.Options.Select(o => $"'{o.Text}'")));

        // Reject hidden options.
        if (!string.IsNullOrWhiteSpace(option.ShowIf))
        {
            var visible = PowerFxHelper.EvaluateBool(option.ShowIf, state, out var evalError);
            if (evalError is not null)
                throw new DialogueException(
                    $"Error evaluating show_if for option '{optionText}': {evalError}");
            if (!visible)
                return DialogueResult.FromError($"Option '{optionText}' is not currently available.");
        }

        // Fire option event.
        if (option.Event is not null)
            EventTriggered?.Invoke(option.Event);

        // Determine where to go next.
        _awaitingChoice = false;

        if (!string.IsNullOrWhiteSpace(option.Goto))
        {
            if (option.Goto == "exit")
                return MarkFinished();

            _nextSceneName = option.Goto;
            _nextLineIndex = 0;
        }
        else
        {
            // Fall through to the next line after the options line.
            if (!_model.Scenes.TryGetValue(_currentSceneName!, out var scene))
                return MarkFinished();

            var nextIdx = FindNextValidLineIndex(scene, _currentSceneName!, _currentLineIndex, state);
            if (nextIdx < 0)
                return MarkFinished();

            _nextSceneName = _currentSceneName!;
            _nextLineIndex = nextIdx;
        }

        return Continue(state);
    }

    /// <summary>Resets the dialogue to its starting position.</summary>
    public void Reset()
    {
        _nextSceneName    = _model.Scenes.Keys.First();
        _nextLineIndex    = 0;
        _currentSceneName = null;
        _currentLineIndex = 0;
        _currentLine      = null;
        _awaitingChoice   = false;
        _finished         = false;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private DialogueResult MarkFinished()
    {
        _finished = true;
        Finished?.Invoke();
        return DialogueResult.Finished;
    }

    /// <summary>
    /// Returns the first valid line at or after <paramref name="startIndex"/>,
    /// honouring if/else/skip semantics.
    /// </summary>
    private (DialogueLine line, int index)? FindValidLine(
        List<DialogueLine> scene,
        string sceneName,
        int startIndex,
        Dictionary<string, object> state)
    {
        var i = startIndex;
        while (i < scene.Count)
        {
            var line = scene[i];

            if (string.IsNullOrWhiteSpace(line.Condition))
                return (line, i);

            var met = PowerFxHelper.EvaluateBool(line.Condition, state, out var err);
            if (err is not null)
                throw new DialogueException(
                    $"Error evaluating 'if' condition in scene '{sceneName}' line {i + 1}: {err}");

            if (met)            return (line, i);          // condition met → show line
            if (line.ElseLine is not null) return (line.ElseLine, i);  // else branch
            i++;                                           // skip line, try next
        }
        return null; // no more lines
    }

    /// <summary>
    /// Returns the index of the next valid line after <paramref name="fromIndex"/>.
    /// Returns -1 when the scene is exhausted.
    /// </summary>
    private int FindNextValidLineIndex(
        List<DialogueLine> scene,
        string sceneName,
        int fromIndex,
        Dictionary<string, object> state)
    {
        var i = fromIndex + 1;
        while (i < scene.Count)
        {
            var line = scene[i];

            if (string.IsNullOrWhiteSpace(line.Condition))
                return i;

            var met = PowerFxHelper.EvaluateBool(line.Condition, state, out var err);
            if (err is not null)
                throw new DialogueException(
                    $"Error evaluating 'if' condition in scene '{sceneName}' line {i + 1}: {err}");

            if (met || line.ElseLine is not null) return i;
            i++;
        }
        return -1;
    }

    private void SetNextPosition(
        DialogueLine line,
        string sceneName,
        int lineIndex,
        Dictionary<string, object> state)
    {
        if (!string.IsNullOrWhiteSpace(line.Goto))
        {
            _nextSceneName = line.Goto;  // "exit" is handled on the next Continue() call
            _nextLineIndex = 0;
            return;
        }

        if (!_model.Scenes.TryGetValue(sceneName, out var scene))
        {
            _nextSceneName = "exit";
            _nextLineIndex = 0;
            return;
        }

        var nextIdx = FindNextValidLineIndex(scene, sceneName, lineIndex, state);
        if (nextIdx < 0)
        {
            _nextSceneName = "exit";
            _nextLineIndex = 0;
        }
        else
        {
            _nextSceneName = sceneName;
            _nextLineIndex = nextIdx;
        }
    }

    private List<OptionResult> BuildOptionResults(
        List<DialogueOption> options,
        Dictionary<string, object> state)
    {
        var results = new List<OptionResult>(options.Count);

        foreach (var opt in options)
        {
            // Filter hidden options.
            if (!string.IsNullOrWhiteSpace(opt.ShowIf))
            {
                var visible = PowerFxHelper.EvaluateBool(opt.ShowIf, state, out var err);
                if (err is not null)
                    throw new DialogueException(
                        $"Error evaluating show_if for option '{opt.Text}': {err}");
                if (!visible) continue;
            }

            // Evaluate locked status.
            var locked = false;
            if (!string.IsNullOrWhiteSpace(opt.LockedIf))
            {
                locked = PowerFxHelper.EvaluateBool(opt.LockedIf, state, out var err);
                if (err is not null)
                    throw new DialogueException(
                        $"Error evaluating locked_if for option '{opt.Text}': {err}");
            }

            results.Add(new OptionResult { Text = opt.Text, IsLocked = locked });
        }

        return results;
    }

    private string PickText(List<string> lines)
    {
        if (lines.Count == 0) return string.Empty;
        return lines.Count == 1 ? lines[0] : lines[_random.Next(lines.Count)];
    }
}
