namespace RoslynPadSample.Runtime
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    internal class JsonConsoleDumper
    {
        private static readonly byte[] NewLine = Encoding.UTF8.GetBytes(Environment.NewLine);

        private readonly Stream _stream;

        private readonly object _lock = new();

        public JsonConsoleDumper()
        {
            _stream = Console.OpenStandardOutput();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public void Dump<TData>(TData data)
        {
            try
            {
                DumpMessage(JsonSerializer.Serialize(ConsoleDumpData.Create(data)));
            }
            catch (Exception ex)
            {
                try
                {
                    DumpMessage(JsonSerializer.Serialize(ConsoleDumpData.Create("Error during Dump: " + ex.Message)));
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                _stream.Flush();
            }
        }

        public TextWriter CreateWriter(ConsoleMessageType type)
        {
            return new ConsoleRedirectWriter(this, type);
        }

        private void DumpMessage(string message)
        {
            lock (_lock)
            {
                using (var textWriter = new StreamWriter(_stream, leaveOpen: true))
                {
                    textWriter.Write(message);
                }

                DumpNewLine();
            }
        }

        private void DumpNewLine()
        {
            _stream.Write(NewLine, 0, NewLine.Length);
        }

        /// <summary>
        /// Redirects the console to the Dump method.
        /// </summary>
        private sealed class ConsoleRedirectWriter : TextWriter
        {
            private readonly JsonConsoleDumper _dumper;
            private readonly ConsoleMessageType _type;

            public override Encoding Encoding => Encoding.UTF8;

            public ConsoleRedirectWriter(JsonConsoleDumper dumper, ConsoleMessageType type)
            {
                _dumper = dumper;
                _type = type;
            }

            public override void Write(string value)
            {
                if (string.Equals(Environment.NewLine, value, StringComparison.Ordinal))
                {
                    return;
                }

                Dump(value);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                if (EndsWithNewLine(buffer, index, count))
                {
                    count -= Environment.NewLine.Length;
                }

                if (count > 0)
                {
                    Dump(new string(buffer, index, count));
                }
            }

            public override void Write(char value)
            {
                Dump(value.ToString());
            }

            private static bool EndsWithNewLine(char[] buffer, int index, int count)
            {
                var nl = Environment.NewLine;

                if (count < nl.Length)
                {
                    return false;
                }

                for (int i = nl.Length; i >= 1; --i)
                {
                    if (buffer[index + count - i] != nl[nl.Length - i])
                    {
                        return false;
                    }
                }

                return true;
            }

            private void Dump(string value)
            {
                _dumper.Dump(new ConsoleMessage {Type = _type, Message = value});
            }
        }
    }
}