namespace RoslynPadSample.Runtime
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// This class initializes the RoslynPad standalone host.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class RuntimeInitializer
    {
        private static bool _initialized;

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            TryAttachToParentProcess();
            AttachConsole();
        }

        private static void AttachConsole()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            var consoleDumper = new JsonConsoleDumper();

            Console.SetOut(consoleDumper.CreateWriter(ConsoleMessageType.Out));
            Console.SetError(consoleDumper.CreateWriter(ConsoleMessageType.Error));

            AppDomain.CurrentDomain.UnhandledException += (o, e) =>
                {
                    // TODO: Dump exception
                    Environment.Exit(1);
                };

            Debugger.Notified += (s, l, v) => consoleDumper.Dump(new DebugInfo { SpanStart = s, SpanLength = l, Variables = v });
            AppDomain.CurrentDomain.ProcessExit += (o, e) => consoleDumper.Flush();
        }

        private static bool TryAttachToParentProcess()
        {
            if (ParseCommandLine("pid", @"\d+", out var parentProcessId))
            {
                AttachToParentProcess(int.Parse(parentProcessId));

                return true;
            }

            return false;
        }

        internal static void AttachToParentProcess(int parentProcessId)
        {
            Process clientProcess;
            try
            {
                clientProcess = Process.GetProcessById(parentProcessId);
            }
            catch (ArgumentException)
            {
                Environment.Exit(1);
                return;
            }

            clientProcess.EnableRaisingEvents = true;
            clientProcess.Exited += (o, e) =>
            {
                Environment.Exit(1);
            };

            if (!IsAlive(clientProcess))
            {
                Environment.Exit(1);
            }
        }

        private static bool ParseCommandLine(string name, string pattern, out string value)
        {
            var match = Regex.Match(Environment.CommandLine, @$"--{name}\s+""?({pattern})");
            value = match.Success ? match.Groups[1].Value : string.Empty;
            return match.Success;
        }

        private static bool IsAlive(Process process)
        {
            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}
