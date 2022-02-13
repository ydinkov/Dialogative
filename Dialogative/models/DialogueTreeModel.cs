using System.Collections.Generic;

namespace Dialogative.Models
{
    public class DialogueTreeModel{
        public string Name { get; set; } = null!;
        //internal string BaseSpritePath { get; set; } = null!;
        
        public Dictionary<string, string> Moods { get; set; } = null!;
        public Dictionary<string, string> Sounds { get; set; } = null!;
        public string[] Variables { get; set; } = null!;
        
        public Dictionary<string, SceneModel> Scenes = new();


    }
}