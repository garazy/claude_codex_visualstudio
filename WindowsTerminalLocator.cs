using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClaudeCodexTerminal
{
    internal static class WindowsTerminalLocator
    {
        private static readonly object SyncRoot = new object();
        private static string cachedPath;
        private static bool searched;

        public static string GetWindowsTerminalPath()
        {
            lock (SyncRoot)
            {
                if (searched)
                {
                    return cachedPath;
                }

                searched = true;
                cachedPath = FindWindowsTerminalPath();
                return cachedPath;
            }
        }

        private static string FindWindowsTerminalPath()
        {
            string localAlias = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsApps",
                "wt.exe");

            if (File.Exists(localAlias))
            {
                return localAlias;
            }

            string windowsApps = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "WindowsApps");

            if (!Directory.Exists(windowsApps))
            {
                return null;
            }

            return EnumerateFilesSafely(windowsApps, "wt.exe")
                .OrderByDescending(path => path.IndexOf("Microsoft.WindowsTerminal", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static IEnumerable<string> EnumerateFilesSafely(string root, string fileName)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string directory = pending.Pop();

                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    yield return candidate;
                }

                string[] children;
                try
                {
                    children = Directory.GetDirectories(directory);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (string child in children)
                {
                    pending.Push(child);
                }
            }
        }
    }
}
