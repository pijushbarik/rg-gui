using CliWrap;
using CliWrap.EventStream;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace rg_gui
{
    public class RipGrepWrapper
    {
        public enum FileEncoding
        {
            Auto,
            GBK
        }

        public enum MaxFileSizeUnit
        {
            None,
            B,
            K,
            M,
            G
        }

        public class SearchParameters
        {
            public string StartPath { get; set; } = string.Empty;

            public IEnumerable<string> SearchStrings { get; set; } = Enumerable.Empty<string>();

            public string IncludePatterns { get; set; } = string.Empty;

            public string ExcludePatterns { get; set; } = string.Empty;

            public bool IncludeHiddenFiles { get; set; } = true;

            public bool IgnoreCase { get; set; } = true;

            public bool Recursive { get; set; } = true;

            public bool RegularExpression { get; set; } = true;

            public FileEncoding Encoding { get; set; } = FileEncoding.Auto;

            public int MaxFileSize { get; set; }

            public MaxFileSizeUnit MaxFileSizeUnit { get; set; } = MaxFileSizeUnit.None;
        }

        public class LineResult
        {
            public LineResult(string lineContent)
            {
                LineContent = lineContent;
                TermResults = new();
            }

            public string LineContent { get; }
            public ConcurrentBag<TermResult> TermResults { get; }
        }

        private class FileEntry
        {
            public readonly ConcurrentDictionary<int, LineResult> Lines = new();
            
            // Guarded by lock (this)
            private readonly HashSet<int> m_termsMatched = new();
            private bool m_raised;
            
            // Returns true exactly once, when every search term has matched in this file.
            public bool MarkTermMatched(int termIndex, int searchTermCount)
            {
                lock (this)
                {
                    m_termsMatched.Add(termIndex);
                    if (!m_raised && m_termsMatched.Count == searchTermCount)
                    {
                        m_raised = true;
                        return true;
                    }
                    return false;
                }
            }
        }
        
        private readonly ConcurrentDictionary<(string path, string filename), FileEntry> m_files = new();

        private int m_searchTermCount;

        public event EventHandler<(string path, string filename)>? FileFound;
        protected void RaiseFileFound(string path, string filename)
        {
            FileFound?.Invoke(this, (path, filename));
        }
        
        private const string RipGrepExecutable = "rg.exe";
        
        // Looks for rg.exe in PATH first, then in installation folder. Returns null if not found.
        public static string? FindRipGrep()
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in pathVariable.Split(Path.PathSeparator,  StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim().Trim('"'), RipGrepExecutable);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Ignore exceptions and continue searching
                }
            }

            var installDirectory = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrWhiteSpace(installDirectory))
            {
                var candidate = Path.Combine(installDirectory, RipGrepExecutable);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            
            return null;
        }

        private readonly string m_ripGrepPath;

        public RipGrepWrapper(string ripGrepPath)
        {
            m_ripGrepPath = ripGrepPath;
        }

        public void Clear()
        {
            m_files.Clear();
        }
        
        // Returns the line results for a file; empty if the file has none.
        public IEnumerable<KeyValuePair<int, LineResult>> GetLineResults(string path, string filename)
        {
            return m_files.TryGetValue((path, filename), out var entry)
                ? entry.Lines
                : Enumerable.Empty<KeyValuePair<int, LineResult>>();
        }

        public async Task Search(SearchParameters searchParameters, CancellationToken cancellationToken)
        {
            var searchTasks = new List<Task>();

            m_searchTermCount = searchParameters.SearchStrings.Count();

            for (var i = 0; i < m_searchTermCount; i++)
            {
                searchTasks.Add(Search(searchParameters, cancellationToken, i));
            }

            await Task.WhenAll(searchTasks);
        }

        private async Task Search(SearchParameters searchParameters, CancellationToken cancellationToken, int termIndex)
        {
            const string fieldMatchSeparator = "\t";

            if (string.IsNullOrWhiteSpace(searchParameters.StartPath))
            {
                return;
            }

            var argsBuilder = new StringBuilder();
            argsBuilder.Append("-uu ");
            argsBuilder.Append("--no-heading ");
            argsBuilder.Append("--line-number ");
            argsBuilder.Append($"--field-match-separator=\"{fieldMatchSeparator}\" ");

            if (searchParameters.IgnoreCase)
            {
                argsBuilder.Append("-i ");
            }

            if (searchParameters.IncludeHiddenFiles)
            {
                argsBuilder.Append("--hidden ");
            }

            if (!searchParameters.Recursive)
            {
                argsBuilder.Append("--max-depth=1 ");
            }

            if (!searchParameters.RegularExpression)
            {
                argsBuilder.Append("--fixed-strings ");
            }

            if (!string.IsNullOrWhiteSpace(searchParameters.IncludePatterns))
            {
                argsBuilder.Append("--iglob={");
                argsBuilder.AppendJoin(",", GetSearchPatterns(searchParameters.IncludePatterns));
                argsBuilder.Append("} ");
            }

            if (searchParameters.ExcludePatterns.Any())
            {
                argsBuilder.Append("--iglob=!{");
                argsBuilder.AppendJoin(",", GetSearchPatterns(searchParameters.ExcludePatterns));
                argsBuilder.Append("} ");
            }

            argsBuilder.Append("--color always ");

            if (searchParameters.Encoding != FileEncoding.Auto)
            {
                argsBuilder.Append($"-E {EncodingTypes[searchParameters.Encoding]} ");
            }

            if (searchParameters.MaxFileSizeUnit != MaxFileSizeUnit.None)
            {
                argsBuilder.Append($"--max-filesize {searchParameters.MaxFileSize}{(searchParameters.MaxFileSizeUnit != MaxFileSizeUnit.B ? searchParameters.MaxFileSizeUnit : string.Empty)} ");
            }

            // Signal no more flags will be set.
            argsBuilder.Append("-- ");

            var searchString = searchParameters.SearchStrings.ElementAt(termIndex);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                argsBuilder.Append(searchString);
                argsBuilder.Append(' ');
            }

            argsBuilder.Append($"\"{searchParameters.StartPath}\"");

            var cmd = Cli.Wrap(m_ripGrepPath)
                .WithArguments(argsBuilder.ToString())
                .WithValidation(CommandResultValidation.None);

            try
            {
                await foreach (var cmdEvent in cmd.ListenAsync(Encoding.UTF8, cancellationToken))
                {
                    switch (cmdEvent)
                    {
                        case StandardOutputCommandEvent stdOut:
                            {
                                var result = stdOut.Text.Split(fieldMatchSeparator, 3);

                                if (result.Length == 3 &&
                                    !string.IsNullOrWhiteSpace(result[0]) &&
                                    !string.IsNullOrWhiteSpace(result[1]) &&
                                    !string.IsNullOrWhiteSpace(result[2]) &&
                                    int.TryParse(RemoveAnsiColors(result[1]), out int lineNumber)
                                    )
                                {
                                    var fullPath = RemoveAnsiColors(result[0]);
                                    var path = Path.GetDirectoryName(fullPath);
                                    var filename = Path.GetFileName(fullPath);

                                    if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(filename))
                                    {
                                        var entry = m_files.GetOrAdd((path, filename), _ => new FileEntry());
                                        
                                        if (entry.MarkTermMatched(termIndex, m_searchTermCount))
                                        {
                                            RaiseFileFound(path, filename);
                                        }
                                        
                                        var lineResult = entry.Lines.GetOrAdd(lineNumber, _ => new LineResult(RemoveAnsiColors(result[2])));
                                        
                                        foreach (var termMatch in GetTermMatches(result[2], termIndex))
                                        {
                                            lineResult.TermResults.Add(termMatch);
                                        }
                                    }
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static readonly char[] PatternDelimiters = { ' ', ':', ';', ',' };

        private static readonly Dictionary<FileEncoding, string> EncodingTypes = new()
        {
            { FileEncoding.Auto, string.Empty },
            { FileEncoding.GBK, "GBK" },
        };

        private static IEnumerable<string> GetSearchPatterns(string patternString)
        {
            var searchPatterns = new List<string>();
            var splitPatternString = patternString.Split(PatternDelimiters, StringSplitOptions.RemoveEmptyEntries);

            var invalidChars = Path.GetInvalidFileNameChars().Where(x => x != Path.DirectorySeparatorChar && x != '*').ToList();
            invalidChars.Add('{');
            invalidChars.Add('}');

            foreach (var token in splitPatternString)
            {
                var pattern = token;

                // Remove any invalid characters from patterns.
                foreach (var c in invalidChars)
                {
                    pattern = pattern.Replace(c.ToString(), string.Empty);
                }

                // Remove any whitespace from patterns.
                pattern = Regex.Replace(pattern, @"\s+", "");

                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    searchPatterns.Add(pattern);
                }
            }

            return searchPatterns;
        }

        private static string RemoveAnsiColors(string source)
        {
            return Regex.Replace(source, @"\x1B\[[^@-~]*[@-~]", string.Empty);
        }

        private static IList<TermResult> GetTermMatches(string source, int termIndex)
        {
            var ripGrepMatches = Regex.Matches(source, @"\x1B\[0m\x1B\[1m\x1B\[31m(.+?)\x1B\[0m");

            var termMatches = new List<TermResult>();

            var processIndex = 0;
            var originalStringIndex = 0;
            for (var i = 0; i < ripGrepMatches.Count; i++)
            {
                if (processIndex != ripGrepMatches[i].Groups[0].Index)
                {
                    originalStringIndex += (ripGrepMatches[i].Groups[0].Index - processIndex);
                }

                var start = originalStringIndex;
                originalStringIndex += ripGrepMatches[i].Groups[1].Value.Length;
                termMatches.Add(new TermResult(start, originalStringIndex - 1, termIndex));
                processIndex = ripGrepMatches[i].Index + ripGrepMatches[i].Length;
            }

            return termMatches;
        }
    }
}
