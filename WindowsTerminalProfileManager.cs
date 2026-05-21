using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClaudeCodexTerminal
{
    internal static class WindowsTerminalProfileManager
    {
        private const string CodexGuid = "{141c6045-99f8-46e5-9645-7fa6595d2697}";
        private const string ClaudeGuid = "{d93ab165-f0b6-4b5b-b2e6-890c268a8d06}";

        private static readonly object SyncRoot = new object();
        private static bool ensured;

        public static void EnsureDefaultProfiles()
        {
            lock (SyncRoot)
            {
                if (ensured)
                {
                    return;
                }

                string settingsPath = FindPreviewSettingsPath();
                if (string.IsNullOrWhiteSpace(settingsPath))
                {
                    throw new FileNotFoundException("Could not find Windows Terminal Preview settings.json under the current user's AppData\\Local\\Packages\\Microsoft.WindowsTerminalPreview* folder.");
                }

                EnsureDefaultProfiles(settingsPath);
                ensured = true;
            }
        }

        private static void EnsureDefaultProfiles(string settingsPath)
        {
            JObject root;
            if (File.Exists(settingsPath) && new FileInfo(settingsPath).Length > 0)
            {
                using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(settingsPath)))
                {
                    DateParseHandling = DateParseHandling.None
                })
                {
                    root = JObject.Load(reader);
                }
            }
            else
            {
                root = new JObject
                {
                    ["$schema"] = "https://aka.ms/terminal-profiles-schema-preview"
                };
            }

            JArray profiles = GetProfileList(root);
            bool changed = false;
            changed |= EnsureProfile(profiles, CreateCodexProfile());
            changed |= EnsureProfile(profiles, CreateClaudeProfile());

            if (!changed)
            {
                return;
            }

            string directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(settingsPath, root.ToString(Formatting.Indented));
        }

        private static JArray GetProfileList(JObject root)
        {
            JObject profilesObject = GetOrCreateObject(root, "profiles");

            if (profilesObject["list"] is JArray existingList)
            {
                return existingList;
            }

            var newList = new JArray();
            profilesObject["list"] = newList;
            return newList;
        }

        private static JObject GetOrCreateObject(JObject parent, string key)
        {
            if (parent[key] is JObject existing)
            {
                return existing;
            }

            var created = new JObject();
            parent[key] = created;
            return created;
        }

        private static bool EnsureProfile(JArray profiles, JObject desired)
        {
            string desiredName = GetString(desired, "name");
            string desiredGuid = GetString(desired, "guid");
            JObject existing = profiles
                .OfType<JObject>()
                .FirstOrDefault(profile =>
                    StringEquals(GetString(profile, "guid"), desiredGuid) ||
                    StringEquals(GetString(profile, "name"), desiredName));

            if (existing == null)
            {
                profiles.Add(desired);
                return true;
            }

            bool changed = false;
            changed |= SetValue(existing, "name", desiredName);
            changed |= SetValue(existing, "guid", desiredGuid);
            changed |= SetValue(existing, "commandline", GetString(desired, "commandline"));
            changed |= SetValue(existing, "hidden", false);

            foreach (JProperty property in desired.Properties())
            {
                if (existing[property.Name] == null)
                {
                    existing[property.Name] = property.Value.DeepClone();
                    changed = true;
                }
            }

            return changed;
        }

        private static JObject CreateCodexProfile()
        {
            return CreateProfile(
                "Codex",
                CodexGuid,
                "cmd.exe /k codex",
                ResolveIconPath("chatgpt.ico"),
                "#254EBD");
        }

        private static JObject CreateClaudeProfile()
        {
            return CreateProfile(
                "Claude",
                ClaudeGuid,
                "cmd.exe /k claude",
                ResolveIconPath("claude.ico"),
                "#00E1F0");
        }

        private static JObject CreateProfile(string name, string guid, string commandline, string iconPath, string tabColor)
        {
            var profile = new JObject
            {
                ["altGrAliasing"] = true,
                ["antialiasingMode"] = "grayscale",
                ["closeOnExit"] = "automatic",
                ["colorScheme"] = "Ottosson",
                ["commandline"] = commandline,
                ["cursorShape"] = "underscore",
                ["elevate"] = true,
                ["experimental.rainbowSuggestions"] = true,
                ["experimental.retroTerminalEffect"] = false,
                ["font"] = new JObject
                {
                    ["face"] = "Cascadia Mono",
                    ["size"] = 14
                },
                ["guid"] = guid,
                ["hidden"] = false,
                ["historySize"] = 9001,
                ["name"] = name,
                ["padding"] = "8, 8, 8, 8",
                ["snapOnInput"] = true,
                ["startingDirectory"] = string.Empty,
                ["tabColor"] = tabColor,
                ["useAcrylic"] = false
            };

            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                profile["icon"] = iconPath;
            }

            return profile;
        }

        private static string FindPreviewSettingsPath()
        {
            string packagesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "Local",
                "Packages");

            if (!Directory.Exists(packagesRoot))
            {
                return null;
            }

            string[] packageDirectories;
            try
            {
                packageDirectories = Directory.GetDirectories(packagesRoot, "Microsoft.WindowsTerminalPreview*");
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            foreach (string packageDirectory in packageDirectories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string localStateSettings = Path.Combine(packageDirectory, "LocalState", "settings.json");
                if (File.Exists(localStateSettings))
                {
                    return localStateSettings;
                }

                string discovered = EnumerateFilesSafely(packageDirectory, "settings.json")
                    .OrderBy(path => path.IndexOf("\\LocalState\\", StringComparison.OrdinalIgnoreCase) < 0)
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(discovered))
                {
                    return discovered;
                }
            }

            string firstPackage = packageDirectories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            return string.IsNullOrWhiteSpace(firstPackage)
                ? null
                : Path.Combine(firstPackage, "LocalState", "settings.json");
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

        private static string ResolveIconPath(string fileName)
        {
            string extensionIcon = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "resources",
                fileName);

            return File.Exists(extensionIcon) ? extensionIcon : fileName;
        }

        private static bool SetValue(JObject profile, string key, object value)
        {
            JToken existing = profile[key];
            JToken desired = value == null ? JValue.CreateNull() : JToken.FromObject(value);
            if (existing != null && JToken.DeepEquals(existing, desired))
            {
                return false;
            }

            profile[key] = desired;
            return true;
        }

        private static string GetString(JObject profile, string key)
        {
            return profile[key]?.Value<string>();
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
