namespace RoslynPadSample.Models
{
    public sealed class VariableModel
    {
        public string Name { get; set; }

        public string Value { get; set; }
    }

    public enum OutputTextType
    {
        Output = 0,
        Console = 1
    }
}
