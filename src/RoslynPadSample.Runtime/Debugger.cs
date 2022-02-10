namespace RoslynPadSample.Runtime
{
    using System;

    public static class Debugger
    {
        public static event Action<int, int, Var[]> Notified;

        public static void Notify(int spanStart, int spanLength, params Var[] variables)
        {
            Notified?.Invoke(spanStart, spanLength, variables);
        }
    }
}