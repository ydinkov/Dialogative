using Dialogative;

// ── Load from file ──────────────────────────────────────────────────────────
var handler = new DialogueBuilder()
    .FromFile("v2example.yml")
    .BuildHandler();

Console.WriteLine($"Loaded: {handler.Title}");
Console.WriteLine("Press any key to advance. When options appear, press 1/2/3 to choose.");
Console.WriteLine(new string('─', 60));

// ── Game state (would normally come from your game engine) ──────────────────
var gameState = new Dictionary<string, object>();

// ── Event listeners (optional — the result also contains all of this info) ──
handler.SubjectChanged      += speaker => Console.ForegroundColor = ConsoleColor.Cyan;
handler.MoodChanged         += mood    => Console.Title = $"Mood: {mood}";
handler.SoundTriggered      += sfx     => Console.WriteLine($"  ♪ SFX: {sfx}");
handler.MusicChanged        += music   => Console.WriteLine($"  ♫ Music: {music}");
handler.EventTriggered      += evt     => Console.WriteLine($"  ► Event: {evt}");
handler.StateUpdateRequested += (k, v) => gameState[k] = v!;

// ── Main loop ───────────────────────────────────────────────────────────────
DialogueResult result = handler.Continue(gameState);

while (!result.IsFinished)
{
    Console.ResetColor();

    if (result.HasError)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Error] {result.Error}");
        Console.ResetColor();
        break;
    }

    // Print the line
    var prefix = result.Speaker is not null ? $"{result.Speaker}: " : string.Empty;
    Console.WriteLine($"{prefix}{result.Text}");

    if (result.StateChange is not null)
        Console.WriteLine($"  [state] {result.StateChange.Key} = {result.StateChange.Value}");

    // Present options if any
    if (result.HasOptions)
    {
        Console.WriteLine();
        for (var i = 0; i < result.Options.Count; i++)
        {
            var opt    = result.Options[i];
            var locked = opt.IsLocked ? " [locked]" : string.Empty;
            Console.WriteLine($"  [{i + 1}] {opt.Text}{locked}");
        }
        Console.WriteLine();
        Console.Write("Your choice: ");

        while (true)
        {
            Console.ReadKey(intercept: true);
            var key = Console.ReadKey(intercept: true);
            if (int.TryParse(key.KeyChar.ToString(), out var idx)
                && idx >= 1 && idx <= result.Options.Count)
            {
                var chosen = result.Options[idx - 1];
                Console.WriteLine(chosen.Text);
                result = handler.ChooseOption(chosen.Text, gameState);
                break;
            }
        }
    }
    else
    {
        Console.ReadKey(intercept: true);
        result = handler.Continue(gameState);
    }
}

Console.ResetColor();
Console.WriteLine();
Console.WriteLine(new string('─', 60));
Console.WriteLine("[ Dialogue finished ]");
