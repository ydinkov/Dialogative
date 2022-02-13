using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dialogative.helpers;

namespace Dialogative.Models
{
    public class BeatModel
    {
        public string Predicate { get; set; }  = "true"!;
        public LineModel Success { get; set; }  = null!;
        public LineModel Failure { get; set; }  = null!;
        public SceneModel ParentScene { get; set; }  = null!;
        
        
        internal async Task<LineModel> GetLine(Func<ICollection<string>> delarations,Func<ICollection<string>> mutations) =>
            await Predicate.Bool(delarations,mutations) ? Success : Failure;
        
        internal async Task<BeatModel?> GetNext(Func<ICollection<string>> delarations,Func<ICollection<string>> mutations, Dictionary<string, SceneModel> scenes)
        {
            var nextInLine = (await GetLine(delarations,mutations)).Next;
            if (nextInLine == "exit") return null;
            //If no next line, then get next in scene
            return nextInLine == null ? GetNextInScene() : scenes[nextInLine].Beats.First();
        }
        
        private BeatModel? GetNextInScene()
        {
            var thisSceneIndex = ParentScene?.Beats?.ToList()?.IndexOf(this) ?? -1;
            var targetSceneIndex = thisSceneIndex + 1;
            var length = ParentScene?.Beats?.Length ?? 0;
            return length > targetSceneIndex ? ParentScene?.Beats?[targetSceneIndex] : null;
          
        }
    }
}