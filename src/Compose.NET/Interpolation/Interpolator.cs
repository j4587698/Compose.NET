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
using System.Globalization;

namespace Compose.NET.Interpolation
{
    /// <summary>
    /// Delegate for type-casting interpolated string values to their target types.
    /// </summary>
    /// <param name="value">The interpolated string value.</param>
    /// <returns>The cast value (could be int, bool, etc.).</returns>
    internal delegate object TypeCast(string value);

    /// <summary>
    /// Options for configuring the interpolation engine.
    /// </summary>
    internal class InterpolateOptions
    {
        /// <summary>
        /// Function to look up variable values. Returns (value, found).
        /// </summary>
        public VariableLookup? LookupValue { get; set; }

        /// <summary>
        /// Type casting rules keyed by TreePath pattern.
        /// When a config path matches a pattern, the corresponding cast function is applied
        /// to the interpolated string value.
        /// </summary>
        public Dictionary<TreePath, TypeCast>? TypeCastMapping { get; set; }

        /// <summary>
        /// Optional custom substitution function. If null, Template.Substitute is used.
        /// </summary>
        public Func<string, VariableLookup, string>? SubstituteFunc { get; set; }
    }

    /// <summary>
    /// Interpolation engine that traverses a YAML configuration tree and performs
    /// Bash-style variable substitution on string values.
    /// Mirrors compose-go/interpolation.
    ///
    /// Key behaviors:
    /// - Only string values are interpolated; bool, int, float, null are passed through
    /// - Maps are traversed recursively (both keys and values, but only values are interpolated)
    /// - Lists are traversed recursively
    /// - TypeCast rules can convert interpolated strings to int/bool/etc. based on config path
    /// </summary>
    internal static class Interpolator
    {
        /// <summary>
        /// Interpolates all string values in the given configuration dictionary.
        /// </summary>
        /// <param name="config">The raw YAML configuration as a dictionary tree.</param>
        /// <param name="options">Interpolation options.</param>
        /// <returns>A new dictionary with all string values interpolated.</returns>
        public static Dictionary<string, object?> Interpolate(
            Dictionary<string, object?> config,
            InterpolateOptions options)
        {
            var lookup = options.LookupValue ?? DefaultLookup;
            var substitute = options.SubstituteFunc ?? Template.Substitute;

            var result = new Dictionary<string, object?>(config.Count);

            foreach (var kvp in config)
            {
                var path = TreePath.New(kvp.Key);
                result[kvp.Key] = RecursiveInterpolate(kvp.Value, path, lookup, substitute, options);
            }

            return result;
        }

        /// <summary>
        /// Recursively traverses the configuration tree and interpolates string values.
        /// </summary>
        private static object? RecursiveInterpolate(
            object? value,
            TreePath path,
            VariableLookup lookup,
            Func<string, VariableLookup, string> substitute,
            InterpolateOptions options)
        {
            switch (value)
            {
                case string s:
                    return InterpolateString(s, path, lookup, substitute, options);

                case Dictionary<string, object?> dict:
                    return InterpolateDictionary(dict, path, lookup, substitute, options);

                case Dictionary<object, object?> objDict:
                    return InterpolateObjectDictionary(objDict, path, lookup, substitute, options);

                case List<object?> list:
                    return InterpolateList(list, path, lookup, substitute, options);

                case IList<object?> ilist:
                    return InterpolateIList(ilist, path, lookup, substitute, options);

                default:
                    // bool, int, long, double, null, etc. â€” pass through unchanged
                    return value;
            }
        }

        private static object? InterpolateString(
            string s,
            TreePath path,
            VariableLookup lookup,
            Func<string, VariableLookup, string> substitute,
            InterpolateOptions options)
        {
            string interpolated = substitute(s, lookup);

            // Apply type cast if a matching rule exists
            if (options.TypeCastMapping != null)
            {
                foreach (var kvp in options.TypeCastMapping)
                {
                    if (path.Matches(kvp.Key))
                    {
                        return kvp.Value(interpolated);
                    }
                }
            }

            return interpolated;
        }

        private static Dictionary<string, object?> InterpolateDictionary(
            Dictionary<string, object?> dict,
            TreePath path,
            VariableLookup lookup,
            Func<string, VariableLookup, string> substitute,
            InterpolateOptions options)
        {
            var result = new Dictionary<string, object?>(dict.Count);

            foreach (var kvp in dict)
            {
                var childPath = path.Next(kvp.Key);
                result[kvp.Key] = RecursiveInterpolate(kvp.Value, childPath, lookup, substitute, options);
            }

            return result;
        }

        private static Dictionary<string, object?> InterpolateObjectDictionary(
            Dictionary<object, object?> dict,
            TreePath path,
            VariableLookup lookup,
            Func<string, VariableLookup, string> substitute,
            InterpolateOptions options)
        {
            var result = new Dictionary<string, object?>(dict.Count);

            foreach (var kvp in dict)
            {
                string key = kvp.Key?.ToString() ?? string.Empty;
                var childPath = path.Next(key);
                result[key] = RecursiveInterpolate(kvp.Value, childPath, lookup, substitute, options);
            }

            return result;
        }

        private static List<object?> InterpolateList(
            List<object?> list,
            TreePath path,
            VariableLookup lookup,
            Func<string, VariableLookup, string> substitute,
            InterpolateOptions options)
        {
            var result = new List<object?>(list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                var childPath = path.Next(TreePath.MatchList);
                result.Add(RecursiveInterpolate(list[i], childPath, lookup, substitute, options));
            }

            return result;
        }

        private static List<object?> InterpolateIList(
            IList<object?> list,
            TreePath path,
            VariableLookup lookup,
            Func<string, VariableLookup, string> substitute,
            InterpolateOptions options)
        {
            var result = new List<object?>(list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                var childPath = path.Next(TreePath.MatchList);
                result.Add(RecursiveInterpolate(list[i], childPath, lookup, substitute, options));
            }

            return result;
        }

        /// <summary>
        /// Default variable lookup that uses environment variables.
        /// </summary>
        private static (string Value, bool Found) DefaultLookup(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (value != null)
                return (value, true);
            return (string.Empty, false);
        }

        /// <summary>
        /// Creates a VariableLookup from a dictionary, with optional fallback to environment variables.
        /// </summary>
        /// <param name="variables">The variable dictionary.</param>
        /// <param name="fallbackToEnv">If true, falls back to environment variables when not found in dictionary.</param>
        /// <returns>A VariableLookup delegate.</returns>
        public static VariableLookup CreateLookup(
            IDictionary<string, string?>? variables,
            bool fallbackToEnv = false)
        {
            return key =>
            {
                if (variables != null && variables.TryGetValue(key, out var value))
                {
                    return (value ?? string.Empty, true);
                }

                if (fallbackToEnv)
                {
                    var envValue = Environment.GetEnvironmentVariable(key);
                    if (envValue != null)
                        return (envValue, true);
                }

                return (string.Empty, false);
            };
        }

        /// <summary>
        /// Creates a VariableLookup that merges multiple variable sources.
        /// Later sources override earlier ones.
        /// </summary>
        public static VariableLookup CreateLookup(params IDictionary<string, string?>[] sources)
        {
            return key =>
            {
                // Search in reverse order so later sources take precedence
                for (int i = sources.Length - 1; i >= 0; i--)
                {
                    if (sources[i] != null && sources[i].TryGetValue(key, out var value))
                    {
                        return (value ?? string.Empty, true);
                    }
                }

                return (string.Empty, false);
            };
        }

        #region Built-in TypeCast functions

        /// <summary>
        /// Casts a string to an integer. Returns original string if parsing fails.
        /// </summary>
        public static object CastToInt(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
            return value;
        }

        /// <summary>
        /// Casts a string to a long integer. Returns original string if parsing fails.
        /// </summary>
        public static object CastToLong(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
            return value;
        }

        /// <summary>
        /// Casts a string to a boolean. Returns original string if parsing fails.
        /// Supports: true/false, yes/no, 1/0, on/off (case-insensitive).
        /// </summary>
        public static object CastToBool(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            switch (value.ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "1":
                case "on":
                    return true;
                case "false":
                case "no":
                case "0":
                case "off":
                    return false;
                default:
                    return value;
            }
        }

        /// <summary>
        /// Casts a string to a double. Returns original string if parsing fails.
        /// </summary>
        public static object CastToDouble(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;
            return value;
        }

        #endregion
    }
}
