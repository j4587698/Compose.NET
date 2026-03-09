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
using System.Text;
using Compose.NET.Interpolation;

namespace Compose.NET.DotEnv
{
    /// <summary>
    /// Exception thrown when a .env file contains invalid syntax.
    /// </summary>
    public class EnvParseException : Exception
    {
        /// <summary>
        /// The line number where the error occurred.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Initializes a new exception for an invalid .env line.
        /// </summary>
        /// <param name="line">The 1-based line number that failed to parse.</param>
        /// <param name="message">The parsing error details.</param>
        public EnvParseException(int line, string message)
            : base($"line {line}: {message}")
        {
            Line = line;
        }
    }

    /// <summary>
    /// Parser for Docker Compose .env files.
    /// Mirrors compose-go/dotenv/parser.go.
    ///
    /// Features:
    /// - UTF-8 BOM handling
    /// - Comment lines (#)
    /// - export prefix support
    /// - Single-quoted values (no interpolation, no escape expansion)
    /// - Double-quoted values (escape expansion + interpolation)
    /// - Unquoted values (interpolation, inline comments)
    /// - Multi-line quoted values
    /// - Variable expansion with bash-style operators
    /// - Inherited variables (key without =)
    /// - YAML-style colon separator
    /// </summary>
    public static class EnvFileParser
    {
        /// <summary>
        /// UTF-8 BOM bytes.
        /// </summary>
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        /// <summary>
        /// Reads a .env file and returns the parsed key-value pairs.
        /// Variables are expanded using the provided lookup function and previously parsed values.
        /// </summary>
        /// <param name="filePath">Path to the .env file.</param>
        /// <param name="lookup">Optional function to look up external variables (e.g., environment variables).
        /// If null, only variables defined in the file itself are available for expansion.</param>
        /// <returns>Dictionary of parsed key-value pairs.</returns>
        public static Dictionary<string, string> ReadFile(string filePath, VariableLookup? lookup = null)
        {
            var bytes = File.ReadAllBytes(filePath);
            var content = StripBom(bytes);
            return ParseWithLookup(content, lookup);
        }

        /// <summary>
        /// Parses .env content string and returns the key-value pairs.
        /// Variables are expanded using the provided lookup function and previously parsed values.
        /// </summary>
        /// <param name="content">The .env file content.</param>
        /// <param name="lookup">Optional external variable lookup function.</param>
        /// <returns>Dictionary of parsed key-value pairs.</returns>
        public static Dictionary<string, string> ParseWithLookup(string content, VariableLookup? lookup = null)
        {
            var result = Parse(content, lookup);

            // Filter out keys that start with a digit
            var filtered = new Dictionary<string, string>();
            foreach (var kvp in result)
            {
                if (kvp.Key.Length > 0 && char.IsDigit(kvp.Key[0]))
                    continue;
                filtered[kvp.Key] = kvp.Value;
            }

            return filtered;
        }

        /// <summary>
        /// Low-level parse that does not filter digit-prefixed keys.
        /// </summary>
        public static Dictionary<string, string> Parse(string content, VariableLookup? lookup = null)
        {
            var result = new Dictionary<string, string>();
            var parser = new Parser(content, lookup, result);
            parser.Parse();
            return result;
        }

        /// <summary>
        /// Strips UTF-8 BOM from byte array and returns the content as a string.
        /// </summary>
        private static string StripBom(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == Utf8Bom[0] && bytes[1] == Utf8Bom[1] && bytes[2] == Utf8Bom[2])
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Reads multiple .env files and merges the results.
        /// Later files override values from earlier files.
        /// </summary>
        /// <param name="filePaths">Paths to .env files.</param>
        /// <param name="currentEnv">Optional current environment variables for lookup.</param>
        /// <returns>Merged dictionary of all key-value pairs.</returns>
        public static Dictionary<string, string> ReadFiles(
            IEnumerable<string> filePaths,
            IDictionary<string, string>? currentEnv = null)
        {
            var envMap = new Dictionary<string, string>();

            VariableLookup lookup = key =>
            {
                if (currentEnv != null && currentEnv.TryGetValue(key, out var envVal))
                    return (envVal, true);
                if (envMap.TryGetValue(key, out var mapVal))
                    return (mapVal, true);
                return (string.Empty, false);
            };

            foreach (var filePath in filePaths)
            {
                var fileVars = ReadFile(filePath, lookup);
                foreach (var kvp in fileVars)
                {
                    envMap[kvp.Key] = kvp.Value;
                }
            }

            return envMap;
        }

        /// <summary>
        /// Internal parser state machine.
        /// </summary>
        private class Parser
        {
            private readonly string _input;
            private readonly VariableLookup? _externalLookup;
            private readonly Dictionary<string, string> _result;
            private int _pos;
            private int _line;

            public Parser(string input, VariableLookup? externalLookup, Dictionary<string, string> result)
            {
                _input = input;
                _externalLookup = externalLookup;
                _result = result;
                _pos = 0;
                _line = 1;
            }

            public void Parse()
            {
                while (_pos < _input.Length)
                {
                    SkipWhitespaceAndComments();

                    if (_pos >= _input.Length)
                        break;

                    ParseStatement();
                }
            }

            /// <summary>
            /// Skips whitespace characters and comment lines.
            /// </summary>
            private void SkipWhitespaceAndComments()
            {
                while (_pos < _input.Length)
                {
                    // Skip whitespace (including newlines)
                    while (_pos < _input.Length && IsWhitespaceOrNewline(_input[_pos]))
                    {
                        if (_input[_pos] == '\n')
                            _line++;
                        _pos++;
                    }

                    if (_pos >= _input.Length)
                        break;

                    // Check for comment line
                    if (_input[_pos] == '#')
                    {
                        // Skip to end of line
                        while (_pos < _input.Length && _input[_pos] != '\n')
                            _pos++;
                        // Continue to skip more whitespace/comments
                        continue;
                    }

                    // Non-whitespace, non-comment character found
                    break;
                }
            }

            /// <summary>
            /// Parses a single KEY=VALUE statement.
            /// </summary>
            private void ParseStatement()
            {
                int statementLine = _line;

                // Check for 'export' prefix
                StripExportPrefix();

                // Parse key name
                var (key, separator) = ParseKeyName();

                if (separator == '\n' || separator == '\0')
                {
                    // Inherited variable (no = or :)
                    // Try to look up from external environment
                    var trimmedKey = key.Trim();
                    if (!string.IsNullOrEmpty(trimmedKey))
                    {
                        if (_externalLookup != null)
                        {
                            var (val, found) = _externalLookup(trimmedKey);
                            if (found)
                                _result[trimmedKey] = val;
                        }
                    }
                    return;
                }

                // Validate key
                key = key.Trim();
                if (key.Contains(" "))
                {
                    throw new EnvParseException(statementLine, "key cannot contain a space");
                }

                // Skip whitespace before value (but not newlines)
                SkipSpaces();

                // Parse value
                string value;
                if (_pos >= _input.Length || _input[_pos] == '\n')
                {
                    // Empty value
                    value = string.Empty;
                    if (_pos < _input.Length && _input[_pos] == '\n')
                    {
                        _line++;
                        _pos++;
                    }
                }
                else
                {
                    value = ParseValue(statementLine);
                }

                _result[key] = value;
            }

            /// <summary>
            /// Strips the 'export' prefix if present.
            /// </summary>
            private void StripExportPrefix()
            {
                if (_pos + 6 < _input.Length &&
                    _input.Substring(_pos, 6) == "export" &&
                    IsSpaceNotNewline(_input[_pos + 6]))
                {
                    _pos += 6;
                    SkipSpaces();
                }
            }

            /// <summary>
            /// Parses the key name. Returns the key and the separator character ('=', ':', '\n', or '\0' for EOF).
            /// </summary>
            private (string Key, char Separator) ParseKeyName()
            {
                int start = _pos;

                while (_pos < _input.Length)
                {
                    char c = _input[_pos];

                    if (c == '=')
                    {
                        string key = _input.Substring(start, _pos - start);
                        _pos++; // skip '='
                        return (key, '=');
                    }

                    if (c == ':')
                    {
                        string key = _input.Substring(start, _pos - start);
                        _pos++; // skip ':'
                        return (key, ':');
                    }

                    if (c == '\n')
                    {
                        string key = _input.Substring(start, _pos - start);
                        _line++;
                        _pos++; // skip '\n'
                        return (key, '\n');
                    }

                    // Validate key character
                    if (!IsValidKeyChar(c))
                    {
                        string partialKey = _input.Substring(start, _pos - start + 1);
                        throw new EnvParseException(_line,
                            $"unexpected character \"{c}\" in variable name \"{partialKey}\"");
                    }

                    _pos++;
                }

                // EOF reached
                string finalKey = _input.Substring(start, _pos - start);
                return (finalKey, '\0');
            }

            /// <summary>
            /// Parses the value portion of a KEY=VALUE statement.
            /// </summary>
            private string ParseValue(int statementLine)
            {
                char c = _input[_pos];

                if (c == '\'')
                    return ParseSingleQuotedValue(statementLine);
                if (c == '"')
                    return ParseDoubleQuotedValue(statementLine);
                return ParseUnquotedValue();
            }

            /// <summary>
            /// Parses a single-quoted value. No interpolation, no escape expansion.
            /// Only \' is treated as an escaped quote.
            /// </summary>
            private string ParseSingleQuotedValue(int statementLine)
            {
                _pos++; // skip opening '
                var sb = new StringBuilder();

                while (_pos < _input.Length)
                {
                    char c = _input[_pos];

                    if (c == '\\' && _pos + 1 < _input.Length && _input[_pos + 1] == '\'')
                    {
                        // Escaped single quote
                        sb.Append('\'');
                        _pos += 2;
                        continue;
                    }

                    if (c == '\'')
                    {
                        // Closing quote
                        _pos++; // skip closing '
                        // Skip to end of line (rest goes back to main loop)
                        return sb.ToString();
                    }

                    if (c == '\n')
                        _line++;

                    sb.Append(c);
                    _pos++;
                }

                throw new EnvParseException(statementLine, "unterminated quoted value");
            }

            /// <summary>
            /// Parses a double-quoted value. Supports escape sequences and variable interpolation.
            /// </summary>
            private string ParseDoubleQuotedValue(int statementLine)
            {
                _pos++; // skip opening "
                var sb = new StringBuilder();

                while (_pos < _input.Length)
                {
                    char c = _input[_pos];

                    if (c == '\\' && _pos + 1 < _input.Length && _input[_pos + 1] == '"')
                    {
                        // Escaped double quote
                        sb.Append('"');
                        _pos += 2;
                        continue;
                    }

                    if (c == '\\' && _pos + 1 < _input.Length)
                    {
                        // Other backslash sequence â€” keep both characters for later escape expansion
                        sb.Append(c);
                        sb.Append(_input[_pos + 1]);
                        if (_input[_pos + 1] == '\n')
                            _line++;
                        _pos += 2;
                        continue;
                    }

                    if (c == '"')
                    {
                        // Closing quote
                        _pos++; // skip closing "

                        // Expand escape sequences, then expand variables
                        string raw = sb.ToString();
                        string escaped = ExpandEscapes(raw);
                        string expanded = ExpandVariables(escaped);
                        return expanded;
                    }

                    if (c == '\n')
                        _line++;

                    sb.Append(c);
                    _pos++;
                }

                throw new EnvParseException(statementLine, "unterminated quoted value");
            }

            /// <summary>
            /// Parses an unquoted value. Supports inline comments and variable interpolation.
            /// </summary>
            private string ParseUnquotedValue()
            {
                int start = _pos;

                // Find end of line
                int end = _input.IndexOf('\n', _pos);
                string line;
                if (end < 0)
                {
                    line = _input.Substring(_pos);
                    _pos = _input.Length;
                }
                else
                {
                    line = _input.Substring(_pos, end - _pos);
                    _pos = end + 1;
                    _line++;
                }

                // Remove inline comment: look for " #" (space + hash)
                int commentIdx = line.IndexOf(" #", StringComparison.Ordinal);
                if (commentIdx >= 0)
                {
                    line = line.Substring(0, commentIdx);
                }

                // Trim trailing whitespace (including \r for Windows line endings)
                line = line.TrimEnd();

                // Expand variables
                return ExpandVariables(line);
            }

            /// <summary>
            /// Expands escape sequences in double-quoted values.
            /// </summary>
            private static string ExpandEscapes(string value)
            {
                if (value.IndexOf('\\') < 0)
                    return value;

                var sb = new StringBuilder(value.Length);
                int i = 0;

                while (i < value.Length)
                {
                    if (value[i] != '\\' || i + 1 >= value.Length)
                    {
                        sb.Append(value[i]);
                        i++;
                        continue;
                    }

                    char next = value[i + 1];

                    switch (next)
                    {
                        case 'a':
                            sb.Append('\a');
                            i += 2;
                            break;
                        case 'b':
                            sb.Append('\b');
                            i += 2;
                            break;
                        case 'f':
                            sb.Append('\f');
                            i += 2;
                            break;
                        case 'n':
                            sb.Append('\n');
                            i += 2;
                            break;
                        case 'r':
                            sb.Append('\r');
                            i += 2;
                            break;
                        case 't':
                            sb.Append('\t');
                            i += 2;
                            break;
                        case 'v':
                            sb.Append('\v');
                            i += 2;
                            break;
                        case '\\':
                            sb.Append('\\');
                            i += 2;
                            break;
                        case '"':
                            sb.Append('"');
                            i += 2;
                            break;
                        case '$':
                            // \$ -> $$ so that variable expansion treats it as literal $
                            sb.Append("$$");
                            i += 2;
                            break;
                        case '0':
                            // Octal: \0 followed by up to 3 digits
                            int octalEnd = i + 2;
                            while (octalEnd < value.Length && octalEnd < i + 5 &&
                                   value[octalEnd] >= '0' && value[octalEnd] <= '7')
                            {
                                octalEnd++;
                            }
                            if (octalEnd > i + 2)
                            {
                                string octalStr = value.Substring(i + 2, octalEnd - (i + 2));
                                int octalVal = Convert.ToInt32(octalStr, 8);
                                sb.Append((char)octalVal);
                            }
                            else
                            {
                                sb.Append('\0'); // Just \0 with no digits
                            }
                            i = octalEnd;
                            break;
                        default:
                            // Unknown escape â€” keep both characters
                            sb.Append(value[i]);
                            sb.Append(value[i + 1]);
                            i += 2;
                            break;
                    }
                }

                return sb.ToString();
            }

            /// <summary>
            /// Expands variables in the value using the external lookup and current result map.
            /// </summary>
            private string ExpandVariables(string value)
            {
                if (value.IndexOf('$') < 0)
                    return value;

                VariableLookup combinedLookup = key =>
                {
                    // External lookup takes priority
                    if (_externalLookup != null)
                    {
                        var (val, found) = _externalLookup(key);
                        if (found)
                            return (val, true);
                    }

                    // Then check current result map
                    if (_result.TryGetValue(key, out var mapVal))
                        return (mapVal, true);

                    return (string.Empty, false);
                };

                return Template.Substitute(value, combinedLookup);
            }

            /// <summary>
            /// Skips space characters that are not newlines.
            /// </summary>
            private void SkipSpaces()
            {
                while (_pos < _input.Length && IsSpaceNotNewline(_input[_pos]))
                    _pos++;
            }

            /// <summary>
            /// Checks if a character is a whitespace character (not including newline).
            /// Matches compose-go's isSpace which treats \r as whitespace but not \n.
            /// </summary>
            private static bool IsSpaceNotNewline(char c)
            {
                return c == ' ' || c == '\t' || c == '\r' || c == '\v' || c == '\f'
                    || c == '\u0085' || c == '\u00A0';
            }

            /// <summary>
            /// Checks if a character is whitespace including newline.
            /// </summary>
            private static bool IsWhitespaceOrNewline(char c)
            {
                return c == '\n' || IsSpaceNotNewline(c);
            }

            /// <summary>
            /// Checks if a character is valid in a key name.
            /// Allowed: letters, digits, _, ., -, [, ], and Unicode letters/numbers.
            /// </summary>
            private static bool IsValidKeyChar(char c)
            {
                if (c == '_' || c == '.' || c == '-' || c == '[' || c == ']')
                    return true;
                if (char.IsLetter(c) || char.IsDigit(c))
                    return true;
                if (IsSpaceNotNewline(c))
                    return true; // Spaces are allowed during scanning, validated later
                return false;
            }
        }
    }
}
