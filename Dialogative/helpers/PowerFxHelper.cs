using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Dialogative;

/// <summary>
/// Thin wrapper around the Microsoft PowerFX engine.
/// Thread-safe: <see cref="RecalcEngine"/> is safe to reuse.
/// </summary>
internal static class PowerFxHelper
{
    // RecalcEngine is heavyweight — create once and share.
    private static readonly RecalcEngine _engine = new();

    // -----------------------------------------------------------------------
    // Parse-time validation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Validates that <paramref name="expression"/> is syntactically valid PowerFX.
    /// Throws <see cref="DialogueException"/> if the expression has parse errors.
    /// </summary>
    /// <param name="expression">The PowerFX formula to validate.</param>
    /// <param name="context">Human-readable description used in error messages (e.g. "scene 'intro' line 3 'if'").</param>
    internal static void ValidateSyntax(string expression, string context)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return;

        var parseResult = _engine.Parse(expression);
        if (!parseResult.IsSuccess)
        {
            var errors = parseResult.Errors is not null
                ? string.Join("; ", parseResult.Errors.Select(e => e.Message))
                : "unknown parse error";
            throw new DialogueException(
                $"PowerFX syntax error in {context}: {errors}  |  Expression: '{expression}'");
        }
    }

    // -----------------------------------------------------------------------
    // Runtime evaluation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Evaluates <paramref name="expression"/> as a boolean against the
    /// supplied <paramref name="gameState"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the expression evaluates to true.
    /// <c>false</c> on evaluation errors (details via <paramref name="error"/>).
    /// </returns>
    /// <exception cref="DialogueException">
    /// Thrown only for fatal engine faults (not for expected false results or
    /// for expressions that reference undefined game-state variables).
    /// </exception>
    internal static bool EvaluateBool(
        string expression,
        Dictionary<string, object> gameState,
        out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
            return true;

        try
        {
            var record = BuildRecord(gameState);
            var result = _engine.Eval(expression, record);

            return result switch
            {
                BooleanValue b => b.Value,
                ErrorValue e =>
                    SetErrorAndReturn(
                        ref error,
                        $"PowerFX evaluation error in '{expression}': " +
                        string.Join("; ", e.Errors.Select(x => x.Message)),
                        false),
                _ =>
                    SetErrorAndReturn(
                        ref error,
                        $"PowerFX expression '{expression}' returned a non-boolean result ({result?.GetType().Name})",
                        false)
            };
        }
        catch (InvalidOperationException ex)
        {
            // PowerFX throws InvalidOperationException when the expression
            // references variables that are not present in the game-state
            // record. We surface this as a non-fatal evaluation error so
            // the game can continue rather than crashing.
            error = $"PowerFX could not evaluate '{expression}': {ex.Message}";
            return false;
        }
        catch (Exception ex) when (ex is not DialogueException)
        {
            throw new DialogueException(
                $"Fatal error evaluating PowerFX expression '{expression}': {ex.Message}", ex);
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static RecordValue BuildRecord(Dictionary<string, object> gameState)
    {
        var fields = gameState
            .Where(kvp => kvp.Key is not null)
            .Select(kvp => new NamedValue(kvp.Key, ToFormulaValue(kvp.Value)));
        return RecordValue.NewRecordFromFields(fields);
    }

    private static FormulaValue ToFormulaValue(object? value) => value switch
    {
        bool b     => FormulaValue.New(b),
        int i      => FormulaValue.New((double)i),
        long l     => FormulaValue.New((double)l),
        double d   => FormulaValue.New(d),
        float f    => FormulaValue.New((double)f),
        decimal dc => FormulaValue.New((double)dc),
        string s   => FormulaValue.New(s),
        null       => FormulaValue.NewBlank(FormulaType.Boolean),
        _          => FormulaValue.New(value.ToString() ?? string.Empty)
    };

    private static bool SetErrorAndReturn(ref string? error, string message, bool returnValue)
    {
        error = message;
        return returnValue;
    }
}
