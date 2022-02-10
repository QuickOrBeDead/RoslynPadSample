namespace RoslynPadSample.Runtime
{
    using System;

    public sealed class DebugInfo
    {
        private Var[] _variables;

        public int SpanStart { get; set; }

        public int SpanLength { get; set; }

        public Var[] Variables
        {
            get => _variables ??= Array.Empty<Var>();
            set => _variables = value;
        }
    }
}