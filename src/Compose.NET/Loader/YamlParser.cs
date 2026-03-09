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
using System.Text;
using YamlDotNet.Serialization;

namespace Compose.NET.Loader
{
    /// <summary>
    /// Handles raw YAML deserialization and key type conversion.
    /// Converts YamlDotNet's untyped object tree (Dictionary&lt;object, object&gt;)
    /// into a normalized Dictionary&lt;string, object?&gt; tree suitable for further processing.
    /// </summary>
    internal static class YamlParser
    {
        /// <summary>
        /// UTF-8 BOM bytes.
        /// </summary>
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        /// <summary>
        /// Top-level keys that should be empty dictionaries rather than null.
        /// </summary>
        private static readonly HashSet<string> TopLevelCollectionFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "services",
            "networks",
            "volumes",
            "configs",
            "secrets"
        };

        /// <summary>
        /// Service-level keys that should be empty lists rather than null.
        /// </summary>
        private static readonly HashSet<string> ServiceListFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "ports",
            "volumes",
            "environment",
            "env_file",
            "expose",
            "dns",
            "dns_search",
            "extra_hosts",
            "cap_add",
            "cap_drop",
            "devices",
            "depends_on",
            "links",
            "security_opt",
            "tmpfs",
            "sysctls",
            "profiles",
            "configs",
            "secrets",
            "entrypoint",
            "command",
            "labels",
            "annotations"
        };

        /// <summary>
        /// Deserializes a YAML string into a normalized <see cref="Dictionary{TKey,TValue}"/>
        /// with string keys throughout the tree.
        /// </summary>
        /// <param name="yamlContent">The raw YAML content string.</param>
        /// <returns>A dictionary representing the parsed YAML document.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the top-level YAML value is not a mapping.
        /// </exception>
        public static Dictionary<string, object?> ParseYaml(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                return new Dictionary<string, object?>();
            }

            var deserializer = new DeserializerBuilder().Build();
            var raw = deserializer.Deserialize<object>(yamlContent);

            if (raw == null)
            {
                // YAML contained only comments or empty documents
                return new Dictionary<string, object?>();
            }

            if (raw is Dictionary<object, object> dict)
            {
                return (Dictionary<string, object?>)ConvertToStringKeys(dict)!;
            }

            throw new InvalidOperationException(
                $"Expected top-level YAML value to be a mapping, but got {raw.GetType().Name}.");
        }

        /// <summary>
        /// Deserializes a YAML byte array into a normalized dictionary.
        /// Handles UTF-8 BOM stripping before parsing.
        /// </summary>
        /// <param name="content">The raw YAML content as a byte array.</param>
        /// <returns>A dictionary representing the parsed YAML document.</returns>
        public static Dictionary<string, object?> ParseYaml(byte[] content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            string yamlContent = StripBom(content);
            return ParseYaml(yamlContent);
        }

        /// <summary>
        /// Recursively converts an untyped object tree from YamlDotNet into one
        /// where all mapping keys are strings.
        /// <list type="bullet">
        ///   <item><description><see cref="Dictionary{TKey,TValue}"/> with object keys are converted to string keys.</description></item>
        ///   <item><description><see cref="List{T}"/> items are converted recursively.</description></item>
        ///   <item><description>Scalars (string, int, bool, double, null) are left unchanged.</description></item>
        /// </list>
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted value with all mapping keys as strings.</returns>
        internal static object? ConvertToStringKeys(object? value)
        {
            switch (value)
            {
                case null:
                    return null;

                case Dictionary<object, object> objDict:
                {
                    var result = new Dictionary<string, object?>(objDict.Count);
                    foreach (var kvp in objDict)
                    {
                        string key = kvp.Key?.ToString() ?? string.Empty;
                        result[key] = ConvertToStringKeys(kvp.Value);
                    }
                    return result;
                }

                case Dictionary<string, object?> strDict:
                {
                    // Already has string keys, but values may need conversion
                    var result = new Dictionary<string, object?>(strDict.Count);
                    foreach (var kvp in strDict)
                    {
                        result[kvp.Key] = ConvertToStringKeys(kvp.Value);
                    }
                    return result;
                }

                case List<object> list:
                {
                    var result = new List<object?>(list.Count);
                    for (int i = 0; i < list.Count; i++)
                    {
                        result.Add(ConvertToStringKeys(list[i]));
                    }
                    return result;
                }

                // Scalars: string, int, long, bool, double, etc. â€” pass through
                default:
                    return value;
            }
        }

        /// <summary>
        /// Ensures null values for known collection fields become empty dictionaries or lists.
        /// <para>
        /// Top-level keys such as <c>services</c>, <c>networks</c>, <c>volumes</c>,
        /// <c>configs</c>, and <c>secrets</c> are converted from null to empty dictionaries.
        /// </para>
        /// <para>
        /// Within each service definition, known list fields (e.g. <c>ports</c>, <c>volumes</c>,
        /// <c>environment</c>) are converted from null to empty lists.
        /// </para>
        /// </summary>
        /// <param name="dict">The parsed configuration dictionary to fix up.</param>
        /// <returns>The same dictionary reference, modified in place.</returns>
        public static Dictionary<string, object?> FixEmptyNotNull(Dictionary<string, object?> dict)
        {
            if (dict == null)
                throw new ArgumentNullException(nameof(dict));

            // Fix top-level collection fields
            foreach (var field in TopLevelCollectionFields)
            {
                if (dict.ContainsKey(field) && dict[field] == null)
                {
                    dict[field] = new Dictionary<string, object?>();
                }
            }

            // Fix service-level list fields
            if (dict.TryGetValue("services", out var servicesObj) &&
                servicesObj is Dictionary<string, object?> services)
            {
                foreach (var kvp in services)
                {
                    if (kvp.Value is Dictionary<string, object?> serviceDict)
                    {
                        FixServiceListFields(serviceDict);
                    }
                }
            }

            return dict;
        }

        /// <summary>
        /// Extracts the top-level <c>name</c> field from a parsed configuration dictionary.
        /// </summary>
        /// <param name="config">The parsed YAML configuration.</param>
        /// <returns>The project name, or null if not specified.</returns>
        public static string? GetProjectName(Dictionary<string, object?> config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.TryGetValue("name", out var nameObj) && nameObj is string name)
            {
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }

            return null;
        }

        /// <summary>
        /// Fixes known list fields within a single service configuration dictionary,
        /// converting null values to empty lists.
        /// </summary>
        private static void FixServiceListFields(Dictionary<string, object?> serviceDict)
        {
            foreach (var field in ServiceListFields)
            {
                if (serviceDict.ContainsKey(field) && serviceDict[field] == null)
                {
                    serviceDict[field] = new List<object?>();
                }
            }
        }

        /// <summary>
        /// Strips UTF-8 BOM from a byte array and returns the content as a string.
        /// </summary>
        private static string StripBom(byte[] bytes)
        {
            if (bytes.Length >= 3 &&
                bytes[0] == Utf8Bom[0] &&
                bytes[1] == Utf8Bom[1] &&
                bytes[2] == Utf8Bom[2])
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
