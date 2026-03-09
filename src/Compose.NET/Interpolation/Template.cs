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
using System.Text;
using System.Text.RegularExpressions;

namespace Compose.NET.Interpolation
{
    /// <summary>
    /// Exception thrown when a template string contains invalid syntax.
    /// </summary>
    internal class InvalidTemplateException : Exception
    {
        public string Template { get; }

        public InvalidTemplateException(string template)
            : base($"Invalid template: \"{template}\". You may need to escape any $ with another $.")
        {
            Template = template;
        }

        public InvalidTemplateException(string template, string message)
            : base(message)
        {
            Template = template;
        }
    }

    /// <summary>
    /// Exception thrown when a required variable is missing or empty.
    /// </summary>
    internal class MissingRequiredVariableException : Exception
    {
        public string Variable { get; }

        public MissingRequiredVariableException(string variable, string errorMessage)
            : base(errorMessage)
        {
            Variable = variable;
        }
    }

    /// <summary>
    /// Delegate for looking up a variable value.
    /// Returns the value and a boolean indicating whether the variable was set.
    /// </summary>
    public delegate (string Value, bool Found) VariableLookup(string key);

    /// <summary>
    /// Bash-style template substitution engine that supports nested variable references.
    /// Mirrors compose-go/template.
    ///
    /// Supported syntax:
    /// - $VAR / ${VAR}       â€” simple substitution
    /// - ${VAR:-default}     â€” default when empty or unset
    /// - ${VAR-default}      â€” default when unset only
    /// - ${VAR:+alternate}   â€” alternate when set and non-empty
    /// - ${VAR+alternate}    â€” alternate when set (even if empty)
    /// - ${VAR:?error}       â€” error when empty or unset
    /// - ${VAR?error}        â€” error when unset only
    /// - $$                  â€” literal $
    /// - Nested: ${FOO:-${BAR:-default}}
    /// </summary>
    internal static class Template
    {
        // Regex to match variable references: $$, ${...}, $NAME
        // We use a simpler regex and handle braces manually for nesting support.
        private static readonly Regex SimpleVarPattern = new Regex(
            @"\$\$|\$\{|\$(?<named>[_a-zA-Z][_a-zA-Z0-9]*)",
            RegexOptions.Compiled);

        /// <summary>
        /// Substitutes variable references in the template string using the given mapping function.
        /// </summary>
        /// <param name="template">The template string to process.</param>
        /// <param name="mapping">Function to look up variable values.</param>
        /// <returns>The template with all variable references resolved.</returns>
        public static string Substitute(string template, VariableLookup mapping)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            var sb = new StringBuilder(template.Length);
            int i = 0;

            while (i < template.Length)
            {
                if (template[i] != '$')
                {
                    sb.Append(template[i]);
                    i++;
                    continue;
                }

                // Found a '$'
                if (i + 1 >= template.Length)
                {
                    // Lone '$' at end of string
                    sb.Append('$');
                    i++;
                    continue;
                }

                char next = template[i + 1];

                if (next == '$')
                {
                    // Escaped: $$ -> $
                    sb.Append('$');
                    i += 2;
                    continue;
                }

                if (next == '{')
                {
                    // Braced expression: ${...}
                    int closingIndex = FindMatchingBrace(template, i + 1);
                    if (closingIndex < 0)
                    {
                        throw new InvalidTemplateException(template,
                            $"Invalid template: \"{template}\". Missing closing brace. You may need to escape any $ with another $.");
                    }

                    // Extract the content between ${ and }
                    string expression = template.Substring(i + 2, closingIndex - (i + 2));

                    if (string.IsNullOrEmpty(expression) || string.IsNullOrWhiteSpace(expression))
                    {
                        throw new InvalidTemplateException(template);
                    }

                    string result = ResolveExpression(expression, mapping, template);
                    sb.Append(result);
                    i = closingIndex + 1;
                    continue;
                }

                if (IsValidVariableStart(next))
                {
                    // Simple $VAR
                    int nameStart = i + 1;
                    int nameEnd = nameStart;
                    while (nameEnd < template.Length && IsValidVariableChar(template[nameEnd]))
                    {
                        nameEnd++;
                    }

                    string varName = template.Substring(nameStart, nameEnd - nameStart);
                    var (value, found) = mapping(varName);
                    sb.Append(found ? value : string.Empty);
                    i = nameEnd;
                    continue;
                }

                // $ followed by something that's not a valid variable start â€” keep literal
                sb.Append('$');
                i++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Finds the matching closing brace for an opening brace at the given position.
        /// Handles nested ${...} expressions.
        /// </summary>
        private static int FindMatchingBrace(string s, int openBraceIndex)
        {
            int depth = 1;
            for (int i = openBraceIndex + 1; i < s.Length; i++)
            {
                if (s[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
                else if (s[i] == '$' && i + 1 < s.Length && s[i + 1] == '{')
                {
                    depth++;
                    i++; // skip the '{' so we don't double count
                }
            }
            return -1; // No matching brace found
        }

        /// <summary>
        /// Resolves a braced expression like "VAR", "VAR:-default", "VAR:+alt", etc.
        /// The expression does NOT include the surrounding ${ and }.
        /// </summary>
        private static string ResolveExpression(string expression, VariableLookup mapping, string originalTemplate)
        {
            // Find which operator appears first in the expression
            var (op, opIndex) = FindOperator(expression);

            if (op == null)
            {
                // Simple ${VAR} â€” no operator
                string varName = expression.Trim();
                if (!IsValidVariableName(varName))
                {
                    throw new InvalidTemplateException(originalTemplate);
                }

                var (value, found) = mapping(varName);
                return found ? value : string.Empty;
            }

            // Split at the operator
            string name = expression.Substring(0, opIndex);
            string operand = expression.Substring(opIndex + op.Length);

            if (!IsValidVariableName(name))
            {
                throw new InvalidTemplateException(originalTemplate);
            }

            var (varValue, varFound) = mapping(name);

            switch (op)
            {
                case ":-":
                    // Default when empty or unset
                    if (!varFound || string.IsNullOrEmpty(varValue))
                        return Substitute(operand, mapping);
                    return varValue;

                case "-":
                    // Default when unset only
                    if (!varFound)
                        return Substitute(operand, mapping);
                    return varValue;

                case ":+":
                    // Alternate when set and non-empty
                    if (varFound && !string.IsNullOrEmpty(varValue))
                        return Substitute(operand, mapping);
                    return string.Empty;

                case "+":
                    // Alternate when set (even if empty)
                    if (varFound)
                        return Substitute(operand, mapping);
                    return string.Empty;

                case ":?":
                    // Error when empty or unset
                    if (!varFound || string.IsNullOrEmpty(varValue))
                    {
                        string errorMsg = Substitute(operand, mapping);
                        throw new MissingRequiredVariableException(name,
                            $"required variable {name} is missing a value: {errorMsg}");
                    }
                    return varValue;

                case "?":
                    // Error when unset only
                    if (!varFound)
                    {
                        string errorMsg = Substitute(operand, mapping);
                        throw new MissingRequiredVariableException(name,
                            $"required variable {name} is missing a value: {errorMsg}");
                    }
                    return varValue;

                default:
                    // Should not happen
                    throw new InvalidTemplateException(originalTemplate);
            }
        }

        /// <summary>
        /// Finds the first operator in the expression, ordered by position.
        /// Two-character operators are checked before single-character ones at the same position.
        /// </summary>
        private static (string? Op, int Index) FindOperator(string expression)
        {
            // Operators and their string representations
            string[] operators = { ":-", ":+", ":?", "-", "+", "?" };

            string? bestOp = null;
            int bestIndex = int.MaxValue;

            foreach (var op in operators)
            {
                // Search for the operator, but it must come after at least one character (variable name)
                int idx = -1;
                int searchStart = 1; // operator can't be at position 0

                while (searchStart < expression.Length)
                {
                    idx = expression.IndexOf(op, searchStart, StringComparison.Ordinal);
                    if (idx < 0) break;

                    // Verify the part before the operator is a valid variable name
                    string candidate = expression.Substring(0, idx);
                    if (IsValidVariableName(candidate))
                    {
                        break; // valid operator position
                    }

                    // Not a valid variable name before this operator, keep searching
                    searchStart = idx + 1;
                    idx = -1;
                }

                if (idx > 0 && idx < bestIndex)
                {
                    bestIndex = idx;
                    bestOp = op;
                }
                else if (idx > 0 && idx == bestIndex && op.Length > bestOp!.Length)
                {
                    // At the same position, prefer longer operator (e.g., ":-" over "-")
                    bestOp = op;
                }
            }

            if (bestOp == null)
                return (null, -1);

            return (bestOp, bestIndex);
        }

        /// <summary>
        /// Checks if a character is a valid start for a variable name.
        /// </summary>
        private static bool IsValidVariableStart(char c)
        {
            return c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        /// <summary>
        /// Checks if a character is valid in a variable name (after the first character).
        /// </summary>
        private static bool IsValidVariableChar(char c)
        {
            return c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        }

        /// <summary>
        /// Validates that a string is a valid variable name: [_a-zA-Z][_a-zA-Z0-9]*
        /// </summary>
        internal static bool IsValidVariableName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!IsValidVariableStart(name[0]))
                return false;

            for (int i = 1; i < name.Length; i++)
            {
                if (!IsValidVariableChar(name[i]))
                    return false;
            }

            return true;
        }
    }
}
