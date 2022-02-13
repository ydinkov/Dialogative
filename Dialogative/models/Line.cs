using System.Collections.Generic;

namespace Dialogative.Models
{
    public class LineModel
    {
        public string[] Text { get; set; } = null!;
        public string Mood { get; set; } = null!;
        public string Next { get; set; } = null!;
        public string Sound { get; set; } = null!;
        public Option[] Options { get; set; } = null!;
        public string Trigger { get; set; } = null!;
    }
    
    public class Line
    {
        public string Text { get; set; } = null!;
        public string Mood { get; set; } = null!;
        public string Sound { get; set; } = null!;
        public Option[] Options { get; set; } = null!;
    }
}