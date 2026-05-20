using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ClaudeCodexTerminal
{
    internal sealed class SelectionTerminalContext
    {
        public SelectionTerminalContext(string initialDirectory, string terminalDirectory)
        {
            InitialDirectory = initialDirectory;
            TerminalDirectory = terminalDirectory;
        }

        public string InitialDirectory { get; }

        public string TerminalDirectory { get; }
    }

    internal static class SelectionPathResolver
    {
        public static SelectionTerminalContext GetSelectionContext(IAsyncServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTE80.DTE2 dte = ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                return await serviceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            });

            if (dte == null || dte.SelectedItems == null || dte.SelectedItems.Count == 0)
            {
                return null;
            }

            foreach (EnvDTE.SelectedItem selectedItem in dte.SelectedItems)
            {
                string selectedDirectory = GetSelectedDirectory(selectedItem);
                if (string.IsNullOrWhiteSpace(selectedDirectory) || !Directory.Exists(selectedDirectory))
                {
                    continue;
                }

                string solutionDirectory = GetSolutionDirectory(dte);
                string terminalDirectory = string.IsNullOrWhiteSpace(solutionDirectory)
                    ? selectedDirectory
                    : solutionDirectory;

                if (selectedItem.Project != null && selectedItem.ProjectItem == null)
                {
                    terminalDirectory = selectedDirectory;
                }

                return new SelectionTerminalContext(selectedDirectory, EnsureTrailingSlash(terminalDirectory));
            }

            return null;
        }

        private static string GetSelectedDirectory(EnvDTE.SelectedItem selectedItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (selectedItem.ProjectItem != null)
            {
                return GetProjectItemDirectory(selectedItem.ProjectItem);
            }

            if (selectedItem.Project != null)
            {
                return GetProjectDirectory(selectedItem.Project);
            }

            return null;
        }

        private static string GetProjectItemDirectory(EnvDTE.ProjectItem projectItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string path = GetProjectItemPath(projectItem);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (Directory.Exists(path))
            {
                return path;
            }

            return File.Exists(path) ? Path.GetDirectoryName(path) : null;
        }

        private static string GetProjectItemPath(EnvDTE.ProjectItem projectItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                string fileName = projectItem.FileNames[1];
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return fileName;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (COMException)
            {
            }

            return GetPropertyValue(projectItem.Properties, "FullPath");
        }

        private static string GetProjectDirectory(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fullPath = GetPropertyValue(project.Properties, "FullPath");
            if (!string.IsNullOrWhiteSpace(fullPath) && Directory.Exists(fullPath))
            {
                return fullPath;
            }

            if (!string.IsNullOrWhiteSpace(project.FullName))
            {
                string projectFileDirectory = Path.GetDirectoryName(project.FullName);
                if (!string.IsNullOrWhiteSpace(projectFileDirectory) && Directory.Exists(projectFileDirectory))
                {
                    return projectFileDirectory;
                }
            }

            return null;
        }

        private static string GetSolutionDirectory(EnvDTE80.DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionPath = dte.Solution?.FullName;
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                return null;
            }

            string solutionDirectory = Path.GetDirectoryName(solutionPath);
            return Directory.Exists(solutionDirectory) ? solutionDirectory : null;
        }

        private static string GetPropertyValue(EnvDTE.Properties properties, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return properties?.Item(name)?.Value?.ToString();
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
