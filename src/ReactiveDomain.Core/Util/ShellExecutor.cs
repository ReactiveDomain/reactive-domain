using System.Diagnostics;

namespace ReactiveDomain.Util
{
    public static class ShellExecutor
    {
        public static string GetOutput(string command, string args = null)
        {
            var info = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = command,
                Arguments = args ?? string.Empty
            };

            using (var process = Process.Start(info))
            {
                // ReSharper disable once PossibleNullReferenceException
                var res = process.StandardOutput.ReadToEnd();
                return res;
            }
        }
    }
}
