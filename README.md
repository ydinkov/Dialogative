[![Nuget](https://img.shields.io/nuget/v/Dialogative?label=Nuget&logo=nuget)](https://www.nuget.org/packages/Dialogative/)

<img src="icon.png" alt="drawing" width="200"/>

# Dialogative

Is a helper class that let's you encode a full dialogue tree in a yaml file and query it at runtime.

## Structure

The Yaml File has several components.

### Metadata

The upper part of a dialogue yaml, contains some metadata needed to store and validate the script

- name
- moods
- sounds
- variables

### Scenes

The rest of the dialogue tree consists of SCENES.

### Beats

Each scene consists of a set of BEATS. Dialogue is evaluated beat by beat. Each beat conists of

- A predicate
- A success line
- a failure line

A beat can return one of the two lines, based on how it's predicate is resolved.

### Beat Predicates

If the predicate resolves to true, then the success line is returned If the predicate resolves to false, then the
failure line is returned If the beat does not have a predicate, the success line is returned by default. Predicates can
only be based on variables declared in the variables section above, and can be stacked
with [regular C# boolean logic operators](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/boolean-logical-operators)

### Lines

Each line may contain:

- A **mood** and/or a **sound**, that can be used in-game

- An array of dialogue **text** that may be printed in game.
    - Each item in the array is a dialogue **BARK**
    - Each line that return one **BARK**
    - By default the interpreter will choose a random bark from the list, but this behaviour can be customised

- A **next**: This is a reference to another scene in the tree.
    - After this line is returned, the first line of the referenced scene will be loaded
    - This allows you to string a set of scenes together in a tree

- A **trigger**: returning lines can have in-game triggers. These can be listened to in-game

### Options

Sometimes a line can contain dialogue OPTIONS. These options allow the player to branch out into different scenes. Each
option can forward the player to a different scene Options are returned in game as full objects containing:

- **text**: a text label for the dialogue option
- **predicate**: a predicate that can show this option conditionally; if it resolves to _false_, the option is hidden
- **next**: If this option is chosen, the dialogue is routed to the first branch of this scene

Options allow for more complex branching patterns

## Example

The following dialogue tree consists of some metadata and a set of 5 scenes

```yml

#The name of the character or dialogue tree
name: "Ruby Rick"

# Here we can declare a set of 'moods', a character can take during dialogue
# Later you can attach theme moods to individual lines and use them in-game
moods:
  default: "pensive.jpg"
  excited: "excited.jpg"
  sad: "sad.jpg"


# Here we can declare sound effects that can be triggered in game by individual lines
sounds:
  birds: "birds.wav"
  greeting: "greeting.wav"


# Here we declare a list of VARIABLES, that are used to conditionally 
# offer options, show or hide dialogue lines or enable whole branches
# You can chose to set these variables here, and they will be evaluated BEFORE the game state
# You must declare all variables that are used in the conditions for you dialogue tree
variables:
  - "HasMetRuby"
  - "FoundTheLuteStarted"
  - "FoundTheLute=false"
  - "AcceptedFindTheLute=false"


# This is the root component of the dialogue tree
# A tree consists of a set of "Scenes"
# Each scene has a set of BEATS.
# The dialogue that the player reads is evaluated beat by beat
scenes:
  #Scene1
  introduction:
    beats:
      # Each beat may contain a PREDICATE and two LINES:
      # a Success LINE and a Failure LINE.
      # If the predicate resolves to true, success line is returned,
      # If the predicate resolves to false, failure line is returned,      
      - predicate: "HasMetRuby"
        # Each line may contain:
        # - A mood, that can be used in-game
        # - A sounds, that can be used in-game
        # - An Array of text that may be printed in game. Each item in the array containing a BARK
        #     NOTE: if a line has multiple texts, the interpreter will choose a random BARK from the list to return
        #     you can inject a custom bark selector.
        # - A next: This is a reference to another scene in the tree.
        #     After this line is returned, the first line of the referenced scene will be loaded
        #     This allows you to string a set of scenes together in a tree
        # - A trigger: returning lines can have in-game triggers. These can be listened to in-game
        success:
          mood: excited
          sound: greeting
          text: [ "Hey Buddy!" ]
          next: welcome #Routes the conversation to the 'welcome' scene
        failure:
          mood: default
          text: [ "Step closer stranger." ]

      # If no predicate is defined, the success line is always printed
      - success:
          mood: sad
          text: [ "Man, Listen to this..." ]
      # Then beats will be printed one after another
      - success:
          mood: default
          text: [ "Listen to how prettily the birds chirp" ]

      - success:
          mood: pensive
          text: [ "You just might miss it.." ]
          trigger: "HasMetRuby=true"
          next: welcome

  #Scene2
  welcome:
    beats:
      - success:
          mood: excited
          text: [ "Anything else I do you for?" ]
          # Sometimes a line can contain dialogue OPTIONS
          # These options allow the player to branch out into different scenes
          # Each option can forward the player to a different scene
          options:
            - text: "Buy Instruments"
              next: leave #TODO
            - text: "Sell Music"
              next: leave #TODO
            - text: "Find the lute"
              # NOTE: Options can have predicates,
              # these will only be shown to the player
              # if they resolve to true
              predicate: "!FoundTheLute"
              next: find_the_lute
            - text: "Bye!"
              next: leave
  #Scene3
  find_the_lute:
    beats:
      - success:
          mood: excited
          text: [ "Oh jeez. These birds are really rocking out!" ]

      - success:
          mood: sad
          text: [ "I wish i had my lute with me to help me rock out" ]

      - success:
          mood: sad
          text: [ "Could you help me look? It's somewhere around the house" ]
          options:
            - text: "Sure!"
              next: thanks_for_accepting_FindTheLute
            - text: "Maybe Later..."
              next: leave

  #Scene4    
  thanks_for_accepting_FindTheLute:
    beats:
      - success:
          # The game can listen to these events being thrown during dialogue
          trigger: "AcceptedFindTheLute"
          text: [ "Aww thank you so much!" ]
          next: welcome

  #Scene5          
  leave:
    beats:
      - success:
          text: [ "See ya later!" ]
          next: exit
```

## Usage

```c#

// See https://aka.ms/new-console-template for more information

using Dialogative;
using Dialogative.Models;

//SETUP
//Some facts about the game, related to variables declared int he dialogue
var facts = new List<string>
    { "HasMetRuby=true", "FoundTheLute=true" }; //make sure these variables are assigned a value!

// Something that can capture events coming from the dialogue
var eventQueue = new Queue<string>();

// Load the file string
var yamlText = await File.ReadAllTextAsync("ruby_rick.yaml");

//INIT
// Create the dialogue tree, this one is for an npc, called rick
var rick = new DialogueTree
(
    yamlText, // From you yaml string
    () => facts, // Pass it a Func<ICollection<string>> that can read the state of the game
    eventQueue.Enqueue // Pass it an Action<string> that can listen to events from the dialogue
);


//TALKING
Console.WriteLine($"Loaded: {rick?.Name}"); // Check if we loaded correctly
ConsoleKey key = ConsoleKey.F; // Press F for bugs
Option[]? options = null; // Hold the player's current options


do
{
    key = Console.ReadKey().Key;
    //If there are options for the player, handle their choice
    var choice = options is null ? null : HandlePlayerChoice(options, key);

    //TalkAsync is the main way to interract with the dialogue tree
    //Each call to TalkAsync will advance the dialogue tree one beat
    //Each beat will return a line that can be interpreted by the game
    //You can pass it options, if these are available 
    var line = await rick.TalkAsync(choice);
    if (line is null) break;
    
    
    //Each line will hold one bark in its Text property that can be interpreted by the game
    Console.WriteLine(line.Text);

    //Here we print out some options if there are any
    if (line?.Options?.Any() ?? false)
    {
        options = line.Options;
        var i = 1;
        foreach (var option in line.Options)
        {
            Console.WriteLine($"\t[{i}]\t-\t{option.Text}");
            i++;
        }
    }
    else
    {
        options = null;
    }
     
    
} while (key != ConsoleKey.Escape); // Escape can exit the loop

Console.WriteLine("Done");

Option? HandlePlayerChoice(Option[] options1, ConsoleKey consoleKey)
{
    Option choice = null;
    try
    {
        choice = consoleKey switch
        {
            ConsoleKey.D1 => options1[0],
            ConsoleKey.D2 => options1[1],
            ConsoleKey.D3 => options1[2],
            ConsoleKey.D4 => options1[3],
            _ => options1.First()
        };
    }
    catch
    {
        choice = options1.First();
    }

    return choice;
}

```
