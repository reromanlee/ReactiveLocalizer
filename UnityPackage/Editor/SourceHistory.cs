using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Finds the source text a translation was made from, by the fingerprint the translation carries, in the git history
    /// of the table's source file, following renames. The table window shows it beside the current source text.
    /// </summary>
    /// <remarks>
    /// Best effort, and off the main thread: it finds nothing when the project isn't in a git repository, git isn't on
    /// the PATH, or none of the file's recent versions has that text.
    /// </remarks>
    internal static class SourceHistory
    {
        private const int RevisionLimit = 60;
        private const int TimeoutMilliseconds = 5000;
        private static readonly UTF8Encoding Utf8 = new(false);

        /// <summary>Searches the history of the file at <paramref name="physicalPath"/> on a thread pool thread.</summary>
        /// <returns>The source text, or null with the problem saying why nothing was found.</returns>
        public static Task<(string Text, string Problem)> FindAsync(string physicalPath, string key, uint fingerprint)
        {
            return Task.Run(() =>
            {
                string text = Find(physicalPath, key, fingerprint, out string problem);
                return (text, problem);
            });
        }

        private static string Find(string physicalPath, string key, uint fingerprint, out string problem)
        {
            string folder = Path.GetDirectoryName(physicalPath);
            string file = Path.GetFileName(physicalPath);
            if (!TryRun(folder, $"log --follow --format=%H --name-only -n {RevisionLimit} -- \"{file}\"", out string log, out problem))
            {
                return null;
            }
            List<(string Revision, string Path)> versions = ParseLog(log);
            if (versions.Count == 0)
            {
                problem = "The source file has no history in git yet.";
                return null;
            }
            for (int i = 0; i < versions.Count; i++)
            {
                // Paths in the log are relative to the repository's root, which ':' after a revision means too.
                if (!TryRun(folder, $"show \"{versions[i].Revision}:{versions[i].Path}\"", out string content, out _))
                {
                    continue;
                }
                if (TableDocument.Parse(content).TryGetEntry(key, out TableDocumentEntry entry) && Hashing.ComputeFingerprint(entry.Value) == fingerprint)
                {
                    problem = null;
                    return entry.Value;
                }
            }
            problem = $"None of the last {versions.Count} versions of the source file in git has the text this translation was made from.";
            return null;
        }

        /// <summary>Reads pairs of a revision and the file's path in it, as <c>--format=%H --name-only</c> writes them.</summary>
        private static List<(string Revision, string Path)> ParseLog(string log)
        {
            List<(string, string)> versions = new();
            string revision = null;
            foreach (string rawLine in log.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (revision == null)
                {
                    revision = line;
                }
                else
                {
                    versions.Add((revision, line));
                    revision = null;
                }
            }
            return versions;
        }

        private static bool TryRun(string folder, string arguments, out string output, out string problem)
        {
            output = null;
            try
            {
                ProcessStartInfo start = new("git", arguments)
                {
                    WorkingDirectory = folder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Utf8,
                    StandardErrorEncoding = Utf8
                };
                using Process process = Process.Start(start);
                if (process == null)
                {
                    problem = "git couldn't be started.";
                    return false;
                }
                // Both streams are drained at once, so a full error stream never blocks git.
                Task<string> errors = process.StandardError.ReadToEndAsync();
                Task<string> standard = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(TimeoutMilliseconds))
                {
                    process.Kill();
                    problem = "git took too long to answer.";
                    return false;
                }
                output = standard.Result;
                if (process.ExitCode != 0)
                {
                    problem = errors.Result.Contains("not a git repository")
                        ? "The project isn't in a git repository, so no older source text can be found."
                        : "git couldn't read the source file's history.";
                    return false;
                }
                problem = null;
                return true;
            }
            catch (Exception exception) when (exception is Win32Exception || exception is InvalidOperationException || exception is IOException)
            {
                problem = "Finding older source texts takes git, which isn't installed or on the PATH.";
                return false;
            }
        }
    }
}
