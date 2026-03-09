// Copyright 2026 jx
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using Compose.NET.Interpolation;

namespace Compose.NET.DotEnv
{
    /// <summary>
    /// Delegate for parsing environment file content into key-value pairs.
    /// Mirrors compose-go's <c>dotenv.Parser</c> function signature.
    /// </summary>
    /// <param name="content">The raw file content to parse.</param>
    /// <param name="filename">The filename (for error messages).</param>
    /// <param name="currentVars">Variables already parsed from previous files.</param>
    /// <param name="lookup">External variable lookup (e.g., host environment).</param>
    /// <returns>Dictionary of parsed key-value pairs.</returns>
    public delegate Dictionary<string, string> EnvFormatParser(
        string content,
        string filename,
        IDictionary<string, string> currentVars,
        VariableLookup? lookup);

    /// <summary>
    /// A pluggable registry for environment file format parsers.
    /// Mirrors compose-go's <c>dotenv/format.go</c> plugin registry.
    /// <para>
    /// By default, the standard <c>.env</c> format is registered (handled by <see cref="EnvFileParser"/>).
    /// Custom formats can be registered via <see cref="Register"/> and will be selected
    /// when an <c>env_file</c> entry specifies a <c>format</c> field.
    /// </para>
    /// </summary>
    public static class EnvFormatRegistry
    {
        private static readonly Dictionary<string, EnvFormatParser> Formats
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Static constructor registers the default ".env" format.
        /// </summary>
        static EnvFormatRegistry()
        {
            RegisterDefaults();
        }

        /// <summary>
        /// Registers the default format parsers.
        /// </summary>
        private static void RegisterDefaults()
        {
            Formats[""] = DefaultEnvParser;
        }

        /// <summary>
        /// Registers a custom format parser.
        /// </summary>
        /// <param name="format">The format name (case-insensitive). Use empty string for the default format.</param>
        /// <param name="parser">The parser implementation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="parser"/> is null.</exception>
        public static void Register(string format, EnvFormatParser parser)
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));
            Formats[format ?? ""] = parser;
        }

        /// <summary>
        /// Unregisters a custom format parser.
        /// </summary>
        /// <param name="format">The format name to unregister.</param>
        /// <returns><c>true</c> if the format was found and removed; otherwise <c>false</c>.</returns>
        public static bool Unregister(string format)
        {
            return Formats.Remove(format ?? "");
        }

        /// <summary>
        /// Gets the names of all registered format parsers.
        /// </summary>
        /// <returns>An enumerable of registered format names.</returns>
        public static IEnumerable<string> GetRegisteredFormats()
        {
            return Formats.Keys;
        }

        /// <summary>
        /// Determines whether a parser is registered for the specified format.
        /// </summary>
        /// <param name="format">The format name (case-insensitive). <c>null</c> maps to default.</param>
        /// <returns><c>true</c> if a parser is registered; otherwise <c>false</c>.</returns>
        public static bool IsRegistered(string? format)
        {
            return Formats.ContainsKey(format ?? "");
        }

        /// <summary>
        /// Parses an environment file using the appropriate registered format parser.
        /// </summary>
        /// <param name="filePath">Absolute path to the environment file.</param>
        /// <param name="format">The format name, or <c>null</c>/empty for the default format.</param>
        /// <param name="currentVars">Variables already parsed from previous files.</param>
        /// <param name="lookup">External variable lookup.</param>
        /// <returns>Dictionary of parsed key-value pairs.</returns>
        /// <exception cref="InvalidOperationException">No parser registered for the specified format.</exception>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        public static Dictionary<string, string> ParseFile(
            string filePath,
            string? format,
            IDictionary<string, string>? currentVars = null,
            VariableLookup? lookup = null)
        {
            var effectiveFormat = format ?? "";

            if (!Formats.TryGetValue(effectiveFormat, out var parser))
            {
                throw new InvalidOperationException(
                    $"No parser registered for env_file format '{effectiveFormat}'. " +
                    $"Use EnvFormatRegistry.Register() to add one.");
            }

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"env_file '{filePath}' not found.", filePath);

            var content = ReadFileContent(filePath);
            var vars = currentVars ?? new Dictionary<string, string>(StringComparer.Ordinal);

            return parser(content, filePath, vars, lookup);
        }

        /// <summary>
        /// Parses raw content using the appropriate registered format parser.
        /// </summary>
        /// <param name="content">The raw content to parse.</param>
        /// <param name="format">The format name, or <c>null</c>/empty for the default format.</param>
        /// <param name="filename">Filename for error messages.</param>
        /// <param name="currentVars">Variables already parsed from previous files.</param>
        /// <param name="lookup">External variable lookup.</param>
        /// <returns>Dictionary of parsed key-value pairs.</returns>
        /// <exception cref="InvalidOperationException">No parser registered for the specified format.</exception>
        public static Dictionary<string, string> ParseContent(
            string content,
            string? format,
            string filename = "<inline>",
            IDictionary<string, string>? currentVars = null,
            VariableLookup? lookup = null)
        {
            var effectiveFormat = format ?? "";

            if (!Formats.TryGetValue(effectiveFormat, out var parser))
            {
                throw new InvalidOperationException(
                    $"No parser registered for env_file format '{effectiveFormat}'. " +
                    $"Use EnvFormatRegistry.Register() to add one.");
            }

            var vars = currentVars ?? new Dictionary<string, string>(StringComparer.Ordinal);
            return parser(content, filename, vars, lookup);
        }

        /// <summary>
        /// Resets the registry to only contain the default format parser.
        /// Useful for testing.
        /// </summary>
        public static void Reset()
        {
            Formats.Clear();
            RegisterDefaults();
        }

        /// <summary>
        /// The default .env format parser that delegates to <see cref="EnvFileParser"/>.
        /// </summary>
        private static Dictionary<string, string> DefaultEnvParser(
            string content,
            string filename,
            IDictionary<string, string> currentVars,
            VariableLookup? lookup)
        {
            // Build a combined lookup: external lookup + already-parsed vars
            VariableLookup combinedLookup = key =>
            {
                if (lookup != null)
                {
                    var (val, found) = lookup(key);
                    if (found) return (val, true);
                }

                if (currentVars.TryGetValue(key, out var mapVal))
                    return (mapVal, true);

                return (string.Empty, false);
            };

            return EnvFileParser.ParseWithLookup(content, combinedLookup);
        }

        /// <summary>
        /// Reads file content, stripping UTF-8 BOM if present.
        /// </summary>
        private static string ReadFileContent(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            // Strip UTF-8 BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
}
