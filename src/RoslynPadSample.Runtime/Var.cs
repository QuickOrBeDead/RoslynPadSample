namespace RoslynPadSample.Runtime
{
    public sealed class Var
    {
        public string Name { get; set; }

        public object Value { get; set; }

        public Var()
        {
        }

        public Var(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }
}
