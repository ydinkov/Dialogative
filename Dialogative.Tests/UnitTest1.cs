using FluentAssertions;
using Xunit;

namespace Dialogative.Tests;

/// <summary>
/// Tests covering the v2 dialogue format using v2example.yml and
/// inline YAML snippets that exercise conditions, options, and state updates.
/// </summary>
public class DialogueTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly Dictionary<string, object> EmptyState = [];

    private static DialogueHandler HandlerFromFile(string path) =>
        new DialogueBuilder().FromFile(path).BuildHandler();

    private static DialogueHandler HandlerFromYaml(string yaml) =>
        new DialogueBuilder().WithYaml(yaml).BuildHandler();

    // -----------------------------------------------------------------------
    // v2example.yml — linear progression
    // -----------------------------------------------------------------------

    [Fact]
    public void V2Example_Loads_WithCorrectTitle()
    {
        var handler = HandlerFromFile("v2example.yml");
        handler.Title.Should().Be("Opening Scene - First Notification");
    }

    [Fact]
    public void V2Example_FirstLine_HasCorrectTextAndSpeaker()
    {
        var handler = HandlerFromFile("v2example.yml");
        var result  = handler.Continue(EmptyState);

        result.HasError.Should().BeFalse(because: result.Error);
        result.Text.Should().Be("Voss.");
        result.Speaker.Should().Be("Mara");
        result.Sound.Should().Be("PhoneRing");
        result.Event.Should().Be("SetBackground_MarasApartmentNight");
    }

    [Fact]
    public void V2Example_WalksThrough_DefaultScene_ToFirstOption()
    {
        var handler = HandlerFromFile("v2example.yml");

        DialogueResult? last = null;
        while (true)
        {
            last = handler.Continue(EmptyState);
            last.HasError.Should().BeFalse(because: last.Error);
            last.IsFinished.Should().BeFalse();
            if (last.HasOptions) break;
        }

        last.Should().NotBeNull();
        last!.HasOptions.Should().BeTrue();
        last.Options.Should().HaveCount(3);
        last.Options.Select(o => o.Text).Should().Contain(
            "A dead man is still a victim.",
            "It sounds like someone finally blamed him.",
            "Who is already trying to bury this?");
    }

    [Fact]
    public void V2Example_ChooseOption_Integrity_ReturnsCorrectLine()
    {
        var handler = HandlerFromFile("v2example.yml");
        AdvanceToOptions(handler);

        var result = handler.ChooseOption("A dead man is still a victim.", EmptyState);
        result.HasError.Should().BeFalse(because: result.Error);
        result.Speaker.Should().Be("Mara");
        result.Text.Should().Be(
            "Whatever Wren did, someone killed him. The dead don't lose their rights because the living hate them.");
    }

    [Fact]
    public void V2Example_ChooseOption_Blame_ReturnsCorrectLine()
    {
        var handler = HandlerFromFile("v2example.yml");
        AdvanceToOptions(handler);

        var result = handler.ChooseOption("It sounds like someone finally blamed him.", EmptyState);
        result.HasError.Should().BeFalse(because: result.Error);
        result.Speaker.Should().Be("Mara");
    }

    [Fact]
    public void V2Example_ChooseOption_Burial_ReturnsCorrectLine()
    {
        var handler = HandlerFromFile("v2example.yml");
        AdvanceToOptions(handler);

        var result = handler.ChooseOption("Who is already trying to bury this?", EmptyState);
        result.HasError.Should().BeFalse(because: result.Error);
        result.Speaker.Should().Be("Mara");
        result.Text.Should().Be("Who is already trying to bury this?");
    }

    [Fact]
    public void V2Example_AfterChoice_ContinuesToAfterChoiceScene()
    {
        var handler = HandlerFromFile("v2example.yml");
        AdvanceToOptions(handler);
        handler.ChooseOption("A dead man is still a victim.", EmptyState);

        var result = handler.Continue(EmptyState);
        result.HasError.Should().BeFalse(because: result.Error);
        result.Text.Should().Be(
            "Good. Hold on to that when everyone else starts arguing about whether he deserved it.");
    }

    [Fact]
    public void V2Example_GotoExit_FinishesDialogue()
    {
        var handler = HandlerFromFile("v2example.yml");

        DialogueResult result;
        do
        {
            if (handler.AwaitingChoice)
                result = handler.ChooseOption("A dead man is still a victim.", EmptyState);
            else
                result = handler.Continue(EmptyState);

            result.HasError.Should().BeFalse(because: result.Error);
        } while (!result.IsFinished);

        result.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void V2Example_InvalidOption_ReturnsError()
    {
        var handler = HandlerFromFile("v2example.yml");
        AdvanceToOptions(handler);

        var result = handler.ChooseOption("This option does not exist", EmptyState);
        result.HasError.Should().BeTrue();
        result.Error.Should().Contain("not a valid option");
    }

    [Fact]
    public void V2Example_ContinueWhileAwaitingChoice_ReturnsError()
    {
        var handler = HandlerFromFile("v2example.yml");
        AdvanceToOptions(handler);

        var result = handler.Continue(EmptyState);
        result.HasError.Should().BeTrue();
        result.Error.Should().Contain("ChooseOption");
    }

    // -----------------------------------------------------------------------
    // Conditional lines (if / else)
    // -----------------------------------------------------------------------

    private const string ConditionalYaml = """
        title: Conditional Test

        scenes:
          default:
            - line: ["Always shown"]
              speaker: "A"

            - line: ["Only when flag is true"]
              speaker: "B"
              if: "flag = true"

            - line: ["After conditional"]
              speaker: "C"
        """;

    [Fact]
    public void ConditionalLine_ConditionMet_ShowsLine()
    {
        var state = new Dictionary<string, object> { ["flag"] = true };
        var handler = HandlerFromYaml(ConditionalYaml);

        handler.Continue(state).Text.Should().Be("Always shown");
        handler.Continue(state).Text.Should().Be("Only when flag is true");
        handler.Continue(state).Text.Should().Be("After conditional");
    }

    [Fact]
    public void ConditionalLine_ConditionNotMet_SkipsLine()
    {
        var state = new Dictionary<string, object> { ["flag"] = false };
        var handler = HandlerFromYaml(ConditionalYaml);

        handler.Continue(state).Text.Should().Be("Always shown");
        handler.Continue(state).Text.Should().Be("After conditional");
    }

    private const string ElseYaml = """
        title: Else Test

        scenes:
          default:
            - line: ["Conditional line"]
              speaker: "A"
              if: "flag = true"
              else:
                line: ["Fallback line"]
                speaker: "B"

            - line: ["After"]
              speaker: "C"
        """;

    [Fact]
    public void ConditionalLine_ConditionNotMet_ShowsElseLine()
    {
        var state = new Dictionary<string, object> { ["flag"] = false };
        var handler = HandlerFromYaml(ElseYaml);

        var result = handler.Continue(state);
        result.Text.Should().Be("Fallback line");
        result.Speaker.Should().Be("B");
    }

    [Fact]
    public void ConditionalLine_ConditionMet_ShowsMainLine_NotElse()
    {
        var state = new Dictionary<string, object> { ["flag"] = true };
        var handler = HandlerFromYaml(ElseYaml);

        var result = handler.Continue(state);
        result.Text.Should().Be("Conditional line");
        result.Speaker.Should().Be("A");
    }

    // -----------------------------------------------------------------------
    // Options with show_if / locked_if
    // -----------------------------------------------------------------------

    private const string OptionsYaml = """
        title: Options Test

        scenes:
          default:
            - line: ["Choose wisely"]
              options:
                - text: "Always visible"
                  goto: end
                - text: "Only when unlocked"
                  show_if: "unlocked = true"
                  goto: end
                - text: "Locked option"
                  locked_if: "locked = true"
                  goto: end

          end:
            - line: ["End"]
        """;

    [Fact]
    public void Options_ShowIf_HidesOptionWhenConditionFalse()
    {
        var state = new Dictionary<string, object> { ["unlocked"] = false, ["locked"] = false };
        var handler = HandlerFromYaml(OptionsYaml);

        var result = handler.Continue(state);
        result.HasOptions.Should().BeTrue();
        result.Options.Should().HaveCount(2);
        result.Options.Should().NotContain(o => o.Text == "Only when unlocked");
    }

    [Fact]
    public void Options_ShowIf_ShowsOptionWhenConditionTrue()
    {
        var state = new Dictionary<string, object> { ["unlocked"] = true, ["locked"] = false };
        var handler = HandlerFromYaml(OptionsYaml);

        var result = handler.Continue(state);
        result.Options.Should().Contain(o => o.Text == "Only when unlocked");
    }

    [Fact]
    public void Options_LockedIf_MarksOptionLocked()
    {
        var state = new Dictionary<string, object> { ["unlocked"] = true, ["locked"] = true };
        var handler = HandlerFromYaml(OptionsYaml);

        var result = handler.Continue(state);
        result.Options.Should().Contain(o => o.Text == "Locked option" && o.IsLocked);
    }

    // -----------------------------------------------------------------------
    // State update
    // -----------------------------------------------------------------------

    private const string StateUpdateYaml = """
        title: State Update Test

        scenes:
          default:
            - line: ["Hello"]
              update:
                set: "metPlayer"
                to: true
        """;

    [Fact]
    public void StateUpdate_ReturnedInResult()
    {
        var handler = HandlerFromYaml(StateUpdateYaml);
        var result  = handler.Continue(EmptyState);

        result.StateChange.Should().NotBeNull();
        result.StateChange!.Key.Should().Be("metPlayer");
        result.StateChange.Value.Should().Be(true);
    }

    [Fact]
    public void StateUpdate_EventFired()
    {
        string? capturedKey   = null;
        object? capturedValue = null;

        var handler = HandlerFromYaml(StateUpdateYaml);
        handler.StateUpdateRequested += (k, v) => { capturedKey = k; capturedValue = v; };

        handler.Continue(EmptyState);

        capturedKey.Should().Be("metPlayer");
        capturedValue.Should().Be(true);
    }

    // -----------------------------------------------------------------------
    // goto / scene jumping
    // -----------------------------------------------------------------------

    private const string GotoYaml = """
        title: Goto Test

        scenes:
          default:
            - line: ["Start"]
              goto: second

          second:
            - line: ["Jumped here"]
        """;

    [Fact]
    public void Goto_JumpsToTargetScene()
    {
        var handler = HandlerFromYaml(GotoYaml);

        var first  = handler.Continue(EmptyState);
        first.Text.Should().Be("Start");

        var second = handler.Continue(EmptyState);
        second.Text.Should().Be("Jumped here");
    }

    // -----------------------------------------------------------------------
    // Builder validation
    // -----------------------------------------------------------------------

    [Fact]
    public void Builder_InvalidPowerFx_ThrowsDialogueException()
    {
        const string badYaml = """
            title: Bad FX Test
            scenes:
              default:
                - line: ["line"]
                  if: "((( broken expression"
            """;

        var act = () => HandlerFromYaml(badYaml);
        act.Should().Throw<DialogueException>().WithMessage("*PowerFX*");
    }

    [Fact]
    public void Builder_UnknownGoto_ThrowsDialogueException()
    {
        const string badYaml = """
            title: Bad Goto
            scenes:
              default:
                - line: ["line"]
                  goto: nonexistent_scene
            """;

        var act = () => HandlerFromYaml(badYaml);
        act.Should().Throw<DialogueException>().WithMessage("*nonexistent_scene*");
    }

    [Fact]
    public void Builder_MissingTitle_ThrowsDialogueException()
    {
        const string badYaml = """
            scenes:
              default:
                - line: ["hello"]
            """;

        var act = () => HandlerFromYaml(badYaml);
        act.Should().Throw<DialogueException>().WithMessage("*title*");
    }

    [Fact]
    public void Builder_FileNotFound_ThrowsFileNotFoundException()
    {
        var act = () => new DialogueBuilder().FromFile("no_such_file.yml").Build();
        act.Should().Throw<FileNotFoundException>();
    }

    // -----------------------------------------------------------------------
    // Reset
    // -----------------------------------------------------------------------

    [Fact]
    public void Reset_RestartsDialogueFromBeginning()
    {
        var handler = HandlerFromFile("v2example.yml");

        var first = handler.Continue(EmptyState);
        first.Text.Should().Be("Voss.");

        handler.Continue(EmptyState);

        handler.Reset();

        var afterReset = handler.Continue(EmptyState);
        afterReset.Text.Should().Be("Voss.");
    }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    [Fact]
    public void Events_AreAllFiredFromFirstLine()
    {
        var handler = HandlerFromFile("v2example.yml");

        string? subject = null;
        string? sound   = null;
        string? evt     = null;

        handler.SubjectChanged += s => subject = s;
        handler.SoundTriggered += s => sound   = s;
        handler.EventTriggered += e => evt     = e;

        handler.Continue(EmptyState);

        subject.Should().Be("Mara");
        sound.Should().Be("PhoneRing");
        evt.Should().Be("SetBackground_MarasApartmentNight");
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static void AdvanceToOptions(DialogueHandler handler)
    {
        while (!handler.AwaitingChoice)
        {
            var r = handler.Continue(EmptyState);
            r.HasError.Should().BeFalse(because: r.Error);
            r.IsFinished.Should().BeFalse();
        }
    }
}
