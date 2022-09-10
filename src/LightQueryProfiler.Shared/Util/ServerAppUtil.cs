/*
 Code adapted from:
https://github.com/chromelyapps/demo-projects/tree/master/blazor
September 2022
 */

using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.NetworkInformation;

namespace LightQueryProfiler.Shared.Util
{
    public static class ServerAppUtil
    {
        private const int DefaultPort = 5001;
        private const int StartScan = 5050;
        private const int EndScan = 6000;
        private const string ArgumentType = "--type";

        public static Task? BlazorTask;
        public static CancellationTokenSource? BlazorTaskTokenSource;

        public static int AvailablePort
        {
            get
            {
                for (int i = StartScan; i < EndScan; i++)
                {
                    if (IsPortAvailable(i))
                    {
                        return i;
                    }
                }

                return DefaultPort;
            }
        }

        public static bool IsMainProcess(IEnumerable<string> args)
        {
            if (args == null || !args.Any())
            {
                return true;
            }

            if (!HasArgument(args, ArgumentType))
            {
                return true;
            }

            return false;
        }

        public static bool IsPortAvailable(int port)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port == port)
                {
                    return false;
                }
            }

            return true;
        }

        public static void ProcessExit(object? sender, EventArgs e)
        {
            // Clean up kestrel process if not taken down by OS. This can
            // occur when the app is closed from WindowController (frameless).
            Task.Run(() =>
            {
                if (BlazorTaskTokenSource != null)
                {
                    WaitHandle.WaitAny(new[] { BlazorTaskTokenSource.Token.WaitHandle });
                }

                BlazorTask?.Dispose();
            });
            BlazorTaskTokenSource?.Cancel();
        }

        private static bool HasArgument(IEnumerable<string> args, string arg)
        {
            return args.Any(a => a.StartsWith(arg));
        }

        public static string CreateTempFile(string path)
        {
            string fileName = "lqp.data";
            string fullPath = Path.Combine(path, fileName);
            if (!File.Exists(fullPath))
            {
                File.WriteAllBytes(fullPath, new byte[128]);
            }

            return fullPath;
        }

        public static void SavePort(string path, int port)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fs, null, fs.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
            using (MemoryMappedViewAccessor acc = mmf.CreateViewAccessor())
            {
                acc.Write(0, port);
            }
        }

        public static int GetSavedPort(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fs, null, fs.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
            using (MemoryMappedViewAccessor acc = mmf.CreateViewAccessor())
            {
                return acc.ReadInt32(0);
            }
        }
    }
}