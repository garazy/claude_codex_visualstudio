using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCodexTerminal
{
    internal sealed class OpenTerminalCommand
    {
        public const int OpenCodexCommandId = 0x0100;
        public const int OpenClaudeCommandId = 0x0101;

        public static readonly Guid CommandSet = new Guid("91565bab-c358-4427-bfc6-c179a104cfb7");

        private readonly AsyncPackage package;

        private OpenTerminalCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            AddCommand(commandService, OpenCodexCommandId, "Codex");
            AddCommand(commandService, OpenClaudeCommandId, "Claude");
        }

        public static OpenTerminalCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new OpenTerminalCommand(package, commandService);
        }

        private void AddCommand(OleMenuCommandService commandService, int commandId, string profileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var command = new OleMenuCommand(
                (sender, args) =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    Execute(profileName);
                },
                new CommandID(CommandSet, commandId));
            command.BeforeQueryStatus += BeforeQueryStatus;
            commandService.AddCommand(command);
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var command = (OleMenuCommand)sender;
            SelectionTerminalContext context = SelectionPathResolver.GetSelectionContext(package);
            command.Visible = context != null;
            command.Enabled = context != null;
        }

        private void Execute(string profileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SelectionTerminalContext context = SelectionPathResolver.GetSelectionContext(package);
            if (context == null)
            {
                return;
            }

            try
            {
                string wtExe = WindowsTerminalLocator.GetWindowsTerminalPath();
                if (string.IsNullOrWhiteSpace(wtExe) || !File.Exists(wtExe))
                {
                    throw new FileNotFoundException("Could not find wt.exe in the current user's WindowsApps folder or under C:\\Program Files\\WindowsApps.");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = wtExe,
                    Arguments = "-p " + QuoteArgument(profileName) + " -d " + QuoteArgument(context.TerminalDirectory),
                    WorkingDirectory = context.InitialDirectory,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                    package,
                    ex.Message,
                    "Open " + profileName + " Terminal",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var result = new StringBuilder();
            result.Append('"');

            int backslashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', backslashCount * 2 + 1);
                    result.Append('"');
                    backslashCount = 0;
                    continue;
                }

                result.Append('\\', backslashCount);
                backslashCount = 0;
                result.Append(character);
            }

            result.Append('\\', backslashCount * 2);
            result.Append('"');
            return result.ToString();
        }
    }
}
