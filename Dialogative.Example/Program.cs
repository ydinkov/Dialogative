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