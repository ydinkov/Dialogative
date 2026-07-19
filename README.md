[![NuGet](https://img.shields.io/nuget/v/Dialogative?label=NuGet&logo=nuget)](https://www.nuget.org/packages/Dialogative/)

<img src="https://raw.githubusercontent.com/ydinkov/Dialogative/master/Dialogative/icon.png" alt="Dialogative icon" width="180" />

Dialogative is a YAML-driven dialogue engine for games, with PowerFx conditions, branching options, and explicit state updates.

## Install

```bash
dotnet add package Dialogative
```

## Minimal YAML Example

```yaml
title: Tavern Intro

scenes:
  default:
    - line: ["Welcome back, traveler."]
      speaker: "Innkeeper"

    - line: ["What do you need?"]
      options:
        - text: "A room"
          goto: room
        - text: "Just passing by"
          goto: exit

  room:
    - line: ["One room. Fresh sheets. No questions."]
      speaker: "Innkeeper"
      update:
        set: "hasRoom"
        to: true
      goto: exit
```

## Example 1: Dialogue Object (event-driven)

Use Dialogue when you want direct control and optional event subscriptions.

```csharp
using Dialogative;

var gameState = new Dictionary<string, object>
{
    ["reputation"] = 5,
    ["hasRoom"] = false
};

var dialogue = new DialogueBuilder()
    .FromFile("tavern.yml")
    .WithGameStateProvider(() => gameState)
    .Build();

dialogue.SubjectChanged += speaker => Console.WriteLine($"[speaker] {speaker}");
dialogue.SoundTriggered += sfx => Console.WriteLine($"[sfx] {sfx}");
dialogue.EventTriggered += evt => Console.WriteLine($"[event] {evt}");
dialogue.StateUpdateRequested += (key, value) => gameState[key] = value!;

while (!dialogue.IsFinished)
{
    var result = dialogue.Continue();

    if (result.HasError)
    {
        Console.WriteLine($"Error: {result.Error}");
        break;
    }

    if (result.IsFinished)
        break;

    Console.WriteLine(result.Text);

    if (result.HasOptions)
    {
        var selected = result.Options[0].Text; // replace with player choice
        result = dialogue.ChooseOption(selected);

        if (result.HasError)
        {
            Console.WriteLine($"Error: {result.Error}");
            break;
        }

        if (!result.IsFinished)
            Console.WriteLine(result.Text);
    }
}
```

## Example 2: DialogueHandler (imperative wrapper)

Use DialogueHandler for a straightforward Continue/ChooseOption loop that always takes an explicit game state.

```csharp
using Dialogative;

var gameState = new Dictionary<string, object>();

var handler = new DialogueBuilder()
    .FromFile("tavern.yml")
    .BuildHandler();

DialogueResult result = handler.Continue(gameState);

while (!result.IsFinished && !result.HasError)
{
    Console.WriteLine(result.Text);

    if (result.HasOptions)
    {
        for (var i = 0; i < result.Options.Count; i++)
            Console.WriteLine($"[{i + 1}] {result.Options[i].Text}");

        var chosenText = result.Options[0].Text; // replace with player input
        result = handler.ChooseOption(chosenText, gameState);
    }
    else
    {
        result = handler.Continue(gameState);
    }
}

if (result.HasError)
    Console.WriteLine($"Error: {result.Error}");
```

## Result Model Quick Reference

Each Continue/ChooseOption call returns DialogueResult, including:

- Text and Speaker
- Mood, Sound, Music, Event
- Options (with lock state)
- StateChange
- IsFinished and HasError flags

## NuGet README Publishing

This repository is configured so README.md is included in the package and displayed on the NuGet package page via:

- PackageReadmeFile metadata in the project
- Packing README.md into the .nupkg root
