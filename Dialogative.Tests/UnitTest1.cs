using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Dialogative.Tests;

public class UnitTest1
{
    [Fact]
    public async Task TestForLongIntroduction()
    {
        var facts = new string[]{};
        var eventQueue = new Queue<string>();
        var yamlText = await File.ReadAllTextAsync("ruby_rick.yaml");
        var rick = new DialogueTree(yamlText, ()=>facts, eventQueue.Enqueue,collection => collection.First());
        rick.Should().NotBeNull();
        rick.Name.Should().Be("Ruby Rick");
        (await rick.TalkAsync())?.Text.Should().Be("Step closer stranger.");
        (await rick.TalkAsync())?.Text.Should().Be("Man, Listen to this...");
        (await rick.TalkAsync())?.Text.Should().Be("Listen to how prettily the birds chirp");
        (await rick.TalkAsync())?.Text.Should().Be("Take a moment to listen with me..");
        (await rick.TalkAsync())?.Text.Should().Be("... <breathe in> ...");
        (await rick.TalkAsync())?.Text.Should().Be("Sure is nice, to slow down a bit");
        (await rick.TalkAsync())?.Text.Should().Be("Life goes by so fast these days");
        (await rick.TalkAsync())?.Text.Should().Be("If you don't stop once in a while to look listen and smell..");
        (await rick.TalkAsync())?.Text.Should().Be("You just might miss it..");
        eventQueue.Should().Contain("HasMetRuby=true");
    }
    
    
    [Fact]
    public async Task TestForProvidedFacts()
    {
        var facts = new string[]{"HasMetRuby=true"};
        var eventQueue = new Queue<string>();
        var yamlText = await File.ReadAllTextAsync("ruby_rick.yaml");
        var rick = new DialogueTree(yamlText, ()=>facts, eventQueue.Enqueue,collection => collection.First());
        (await rick.TalkAsync())?.Text.Should().Be("Hey Buddy!");
    }
    
    
    [Fact]
    public async Task TestFoAcceptingQuest()
    {
        var facts = new string[]{"HasMetRuby=true"};
        var eventQueue = new Queue<string>();
        var yamlText = await File.ReadAllTextAsync("ruby_rick.yaml");
        var rick = new DialogueTree(yamlText, ()=>facts, eventQueue.Enqueue,collection => collection.First());
        (await rick.TalkAsync())?.Text.Should().Be("Hey Buddy!");
        var line = await rick.TalkAsync();
        
        line?.Text.Should().Be("Anything else I do you for?");
        line?.Options.Should().Contain(x => x.Text == "Find the lute");
        
        (await rick.TalkAsync(line?.Options[2]))?.Text.Should().Be("Oh jeez. These birds are really rocking out!");
        (await rick.TalkAsync())?.Text.Should().Be("I wish i had my lute with me to help me rock out");
        var line2 = await rick.TalkAsync();
        line2?.Text.Should().Be("Could you help me look? It's somewhere around the house");
        (await rick.TalkAsync(line2?.Options[0]))?.Text.Should().Be("Aww thank you so much!");
        (await rick.TalkAsync())?.Text.Should().Be("Anything else I do you for?");
        (await rick.TalkAsync())?.Should().BeNull();
        eventQueue.Should().Contain("AcceptedFindTheLute");
    }
    
    [Fact]
    public async Task TestForFilteringOptions()
    {
        var facts = new string[]{"HasMetRuby=true","FoundTheLute=true"};
        var eventQueue = new Queue<string>();
        var yamlText = await File.ReadAllTextAsync("ruby_rick.yaml");
        var rick = new DialogueTree(yamlText, ()=>facts, eventQueue.Enqueue,collection => collection.First());
        (await rick.TalkAsync())?.Text.Should().Be("Hey Buddy!");
        var line = await rick.TalkAsync();
        
        line?.Text.Should().Be("Anything else I do you for?");
        line?.Options.Should().NotContain(x => x.Text == "Find the lute");
    }
}