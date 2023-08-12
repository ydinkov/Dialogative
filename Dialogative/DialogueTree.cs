using System.Collections.Concurrent;
using Dialogative.helpers;
using Dialogative.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dialogative
{
    public class DialogueTree
    {
        private readonly Func<ICollection<string>> Mutations;
        private readonly Action<string> _onEventTrigger;
        private readonly Func<ICollection<string>, string>? _randomBarkChooser;
        private DialogueTreeModel Model { get; } = null!;

        private BeatModel? CurrentBeat { get; set; }
        private bool _reset;

        public string Name => Model.Name;
        public IList<string> Declarations => Model.Variables;


        public DialogueTree(string sourceString, Func<ICollection<string>> mutations, Action<string> onEventTrigger,
            Func<ICollection<string>, string>? randomBarkChooser = null)
        {
            Mutations = mutations;
            _onEventTrigger = onEventTrigger;
            _randomBarkChooser = randomBarkChooser;
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance) // see height_in_inches in sample yml 
                .Build();
            Model = deserializer.Deserialize<DialogueTreeModel>(sourceString);
            //A little initialisation here
            foreach (var scene in Model.Scenes.Values)
            foreach (var beats in scene.Beats)
                beats.ParentScene = scene;
            Reset();
        }

        public async Task<Line?> TalkAsync(Option? option = null)
        {
            if (_reset)
            {
                _reset = false;
                return await GetCurrentLineAsync();
            }

            var shouldHaveChosenAnOption = await CurrentBeat!.Predicate.BoolAsync(() => Declarations, Mutations)
                ? CurrentBeat.Success.Options?.Any() ?? false
                : CurrentBeat.Failure.Options?.Any() ?? false;

            if (shouldHaveChosenAnOption && option is null)
            {
                Reset();
                //Exit
                return null;
            }
            else
            {
                if (option != null)
                {
                    if (string.IsNullOrWhiteSpace(option.Next)) return null; //if option doesnt have next, then break
                    CurrentBeat = Model.Scenes[option.Next]?.Beats.First(); // else change beat to next
                }
                else
                {
                    CurrentBeat =
                        await CurrentBeat?.GetNextAsync(() => Declarations, Mutations,
                            Model.Scenes)!; //otherwise keep getting next line
                }
            }

            //If you have something to say, say it
            var somethingToSay = await GetCurrentLineAsync();
            if (somethingToSay != null) return somethingToSay;
            //otherwise reset and end the conversation
            Reset();
            return null;
        }

        private async Task<Line?> GetCurrentLineAsync()
        {
            if (CurrentBeat is null)return null;
            var lineModel = await CurrentBeat?.GetLineAsync(() => Declarations, Mutations)!;
            var q = new ConcurrentQueue<Option>();
            var options = lineModel.Options?.Select(async x =>
            {
                var predicate = x?.Predicate ?? string.Empty;
                var shouldShowOption = await predicate.BoolAsync(() => Declarations, Mutations); 
                if (x != null && (shouldShowOption || string.IsNullOrWhiteSpace(predicate) ) )
                    q.Enqueue(x);
            }) ?? new Task[] { };
            await Task.WhenAll(options);
            _onEventTrigger(lineModel.Trigger);
            return new Line
            {
                Text = lineModel.Text.Choice(_randomBarkChooser),
                Mood = lineModel.Mood,
                Sound = lineModel.Sound,
                Options = q.ToArray()
            };
        }
        
        //private Line? GetCurrentLine()
        //{
        //    if (CurrentBeat is null)return null;
        //    var lineModel = CurrentBeat?.GetLine(() => Declarations, Mutations)!;
        //    var q = new ConcurrentQueue<Option>();
//
        //    foreach (var x in lineModel.Options)
        //    {
        //        var predicate = x?.Predicate ?? string.Empty;
        //        var shouldShowOption = predicate.Bool(() => Declarations, Mutations); 
        //        if (x != null && (shouldShowOption || string.IsNullOrWhiteSpace(predicate) ) )
        //            q.Enqueue(x);
        //    }
        //    _onEventTrigger(lineModel.Trigger);
        //    return new Line
        //    {
        //        Text = lineModel.Text.Choice(_randomBarkChooser),
        //        Mood = lineModel.Mood,
        //        Sound = lineModel.Sound,
        //        Options = q.ToArray()
        //    };
        //}

        private void Reset()
        {
            CurrentBeat = Model.Scenes.First().Value.Beats.First();
            _reset = true;
        }
    }
}