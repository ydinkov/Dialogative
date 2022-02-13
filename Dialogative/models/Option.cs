namespace Dialogative.Models
{
    public class Option
    {
        public string Text { get; set; } = null!;
        public string? Predicate { get; set; }
        public string Color { get; set; } = null!;
        public string Next { get; set; } = null!;
    }
}