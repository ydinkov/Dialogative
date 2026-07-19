using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dialogative;

/// <summary>
/// Fluent builder for creating <see cref="Dialogue"/> and <see cref="DialogueHandler"/> instances.
///
/// <example>
/// <code>
/// // Event-based usage
/// var dialogue = new DialogueBuilder()
///     .WithYaml(yamlString)
///     .WithGameStateProvider(() => myGame.State)
///     .Build();
///
/// dialogue.SubjectChanged += speaker => ...;
/// dialogue.Continue();
///
/// // Handler-based usage
/// var handler = new DialogueBuilder()
///     .FromFile("intro.yml")
///     .BuildHandler();
///
/// var result = handler.Continue(myGame.State);
/// </code>
/// </example>
/// </summary>
public sealed class DialogueBuilder
{
    private string? _yamlContent;
    private Func<Dictionary<string, object>>? _stateProvider;
    private Random? _random;

    // -----------------------------------------------------------------------
    // Source
    // -----------------------------------------------------------------------

    /// <summary>Loads the dialogue from a raw YAML string.</summary>
    /// <exception cref="ArgumentException">When <paramref name="yamlContent"/> is null or whitespace.</exception>
    public DialogueBuilder WithYaml(string yamlContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlContent, nameof(yamlContent));
        _yamlContent = yamlContent;
        return this;
    }

    /// <summary>Loads the dialogue from a YAML file on disk.</summary>
    /// <exception cref="ArgumentException">When <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">When the file does not exist.</exception>
    /// <exception cref="IOException">On other file-system errors.</exception>
    public DialogueBuilder FromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                $"Dialogue file not found: '{Path.GetFullPath(filePath)}'", filePath);

        _yamlContent = File.ReadAllText(filePath);
        return this;
    }

    // -----------------------------------------------------------------------
    // Game-state provider
    // -----------------------------------------------------------------------

    /// <summary>
    /// Supplies a function that returns the current game state as a
    /// key → value dictionary.  Used when calling <see cref="Dialogue.Continue()"/>
    /// or <see cref="Dialogue.ChooseOption"/> without an explicit state parameter.
    /// </summary>
    public DialogueBuilder WithGameStateProvider(Func<Dictionary<string, object>> provider)
    {
        ArgumentNullException.ThrowIfNull(provider, nameof(provider));
        _stateProvider = provider;
        return this;
    }

    // -----------------------------------------------------------------------
    // Optional overrides
    // -----------------------------------------------------------------------

    /// <summary>
    /// Supplies a custom <see cref="Random"/> instance for picking variant lines.
    /// Useful for deterministic tests.
    /// </summary>
    public DialogueBuilder WithRandom(Random random)
    {
        ArgumentNullException.ThrowIfNull(random, nameof(random));
        _random = random;
        return this;
    }

    // -----------------------------------------------------------------------
    // Build
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses and validates the YAML, then returns a <see cref="Dialogue"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">When no YAML source was provided.</exception>
    /// <exception cref="DialogueException">When the YAML is malformed or validation fails.</exception>
    public Dialogue Build()
    {
        EnsureSource();
        var model    = ParseAndValidate(_yamlContent!);
        var provider = _stateProvider ?? (() => []);
        return new Dialogue(model, provider, _random);
    }

    /// <summary>
    /// Parses and validates the YAML, then returns a <see cref="DialogueHandler"/>
    /// that wraps the resulting <see cref="Dialogue"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">When no YAML source was provided.</exception>
    /// <exception cref="DialogueException">When the YAML is malformed or validation fails.</exception>
    public DialogueHandler BuildHandler()
    {
        return new DialogueHandler(Build());
    }

    // -----------------------------------------------------------------------
    // Private — YAML parsing + validation
    // -----------------------------------------------------------------------

    private void EnsureSource()
    {
        if (_yamlContent is null)
            throw new InvalidOperationException(
                "No YAML source has been provided. Call WithYaml() or FromFile() first.");
    }

    private static DialogueModel ParseAndValidate(string yamlContent)
    {
        DialogueModel model;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            model = deserializer.Deserialize<DialogueModel>(yamlContent)
                ?? throw new DialogueException("YAML content is empty or did not deserialize to a dialogue model.");
        }
        catch (Exception ex) when (ex is not DialogueException)
        {
            throw new DialogueException($"Failed to parse YAML: {ex.Message}", ex);
        }

        ValidateModel(model);
        CoerceStateValues(model);
        return model;
    }

    /// <summary>
    /// YAML scalars deserialize to <c>string</c> when the target property type
    /// is <c>object?</c>. This pass converts those strings to proper CLR types.
    /// </summary>
    private static void CoerceStateValues(DialogueModel model)
    {
        foreach (var lines in model.Scenes.Values)
            foreach (var line in lines)
                CoerceLine(line);
    }

    private static void CoerceLine(DialogueLine line)
    {
        if (line.Update is not null && line.Update.To is string s)
            line.Update.To = CoerceScalar(s);

        if (line.ElseLine is not null)
            CoerceLine(line.ElseLine);
    }

    private static object? CoerceScalar(string s)
    {
        if (bool.TryParse(s, out var b)) return b;
        if (long.TryParse(s, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var l)) return l;
        if (double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        return s;
    }

    private static void ValidateModel(DialogueModel model)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(model.Title))
            errors.Add("'title' must be a non-empty string.");

        if (model.Scenes is { Count: 0 })
        {
            errors.Add("'scenes' must contain at least one scene.");
            ThrowIfErrors(errors, model.Title);
            return;
        }

        var sceneNames = model.Scenes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (sceneName, lines) in model.Scenes)
        {
            if (lines is { Count: 0 })
            {
                errors.Add($"Scene '{sceneName}' has no lines.");
                continue;
            }

            for (var i = 0; i < lines.Count; i++)
                ValidateLine(lines[i], $"scene '{sceneName}' line {i + 1}", sceneNames, errors);
        }

        ThrowIfErrors(errors, model.Title);
    }

    private static void ValidateLine(
        DialogueLine line,
        string context,
        HashSet<string> sceneNames,
        List<string> errors)
    {
        if (line.Line is { Count: 0 })
            errors.Add($"{context}: 'line' must be a non-empty list of strings.");

        ValidateGoto(line.Goto, context, sceneNames, errors);

        if (!string.IsNullOrWhiteSpace(line.Condition))
            TryValidatePowerFx(line.Condition, $"{context} 'if'", errors);

        if (line.ElseLine is not null)
            ValidateLine(line.ElseLine, $"{context} (else)", sceneNames, errors);

        if (line.Update is not null && string.IsNullOrWhiteSpace(line.Update.Set))
            errors.Add($"{context}: 'update.set' must not be empty.");

        if (line.Options is not null)
        {
            for (var j = 0; j < line.Options.Count; j++)
                ValidateOption(line.Options[j], $"{context} option {j + 1}", sceneNames, errors);
        }
    }

    private static void ValidateOption(
        DialogueOption opt,
        string context,
        HashSet<string> sceneNames,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(opt.Text))
            errors.Add($"{context}: option 'text' must not be empty.");

        ValidateGoto(opt.Goto, context, sceneNames, errors);

        if (!string.IsNullOrWhiteSpace(opt.ShowIf))
            TryValidatePowerFx(opt.ShowIf, $"{context} 'show_if'", errors);

        if (!string.IsNullOrWhiteSpace(opt.LockedIf))
            TryValidatePowerFx(opt.LockedIf, $"{context} 'locked_if'", errors);
    }

    private static void ValidateGoto(
        string? gotoValue,
        string context,
        HashSet<string> sceneNames,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(gotoValue) || gotoValue == "exit")
            return;

        if (!sceneNames.Contains(gotoValue))
            errors.Add($"{context}: 'goto' references unknown scene '{gotoValue}'.");
    }

    private static void TryValidatePowerFx(string expression, string context, List<string> errors)
    {
        try
        {
            PowerFxHelper.ValidateSyntax(expression, context);
        }
        catch (DialogueException ex)
        {
            errors.Add(ex.Message);
        }
    }

    private static void ThrowIfErrors(List<string> errors, string? title)
    {
        if (errors.Count == 0) return;

        var header = string.IsNullOrWhiteSpace(title)
            ? "Dialogue validation failed:"
            : $"Dialogue '{title}' validation failed:";

        throw new DialogueException(
            header + "\n" + string.Join("\n", errors.Select(e => $"  - {e}")));
    }
}
