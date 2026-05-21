using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCodexTerminal
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class ClaudeCodexTerminalPackage : AsyncPackage
    {
        public const string PackageGuidString = "d693fa16-4b2f-46af-a9f4-f857c6997603";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            _ = Task.Run(
                () =>
                {
                    WindowsTerminalLocator.GetWindowsTerminalPath();
                    try
                    {
                        WindowsTerminalProfileManager.EnsureDefaultProfiles();
                    }
                    catch
                    {
                    }
                },
                cancellationToken);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await OpenTerminalCommand.InitializeAsync(this);
        }
    }
}
