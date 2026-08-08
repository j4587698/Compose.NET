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
using System.IO;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Compose.NET.Loader
{
    /// <summary>
    /// Handles raw YAML deserialization and key type conversion.
    /// Parses YAML into a normalized <see cref="Dictionary{TKey,TValue}"/>
    /// tree using YamlDotNet's representation model, which is fully
    /// compatible with ahead-of-time compilation and trimming.
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
        /// Thrown when the top-level YAML value is not a mapping, or the stream
        /// contains more than one document.
        /// </exception>
        public static Dictionary<string, object?> ParseYaml(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                return new Dictionary<string, object?>();
            }

            var yaml = new YamlStream();
            using (var reader = new StringReader(yamlContent))
            {
                yaml.Load(reader);
            }

            if (yaml.Documents.Count == 0)
            {
                // YAML contained only comments or empty documents
                return new Dictionary<string, object?>();
            }

            if (yaml.Documents.Count > 1)
            {
                throw new InvalidOperationException(
                    "Expected a single document in the stream, but found more than one.");
            }

            var root = yaml.Documents[0].RootNode;
            if (root is YamlMappingNode mappingRoot)
            {
                return ConvertMapping(mappingRoot) ?? new Dictionary<string, object?>();
            }

            throw new InvalidOperationException(
                $"Expected top-level YAML value to be a mapping, but got {root.GetType().Name}.");
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
        /// Converts a YAML scalar node's value to the appropriate .NET type.
        /// Matches YamlDotNet's Deserializer default behavior: bool, int, long, double, null, or string.
        /// </summary>
        internal static object? ConvertScalarValue(YamlScalarNode node)
        {
            var value = node.Value;

            // Quoted strings are always strings
            if (node.Style == ScalarStyle.SingleQuoted ||
                node.Style == ScalarStyle.DoubleQuoted)
            {
                return value;
            }

            // Plain scalars — try typed parsing
            if (value == null || value == "~" ||
                string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                return null;

            if (string.IsNullOrEmpty(value))
                return null;

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            if (int.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var intVal))
                return intVal;

            if (long.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var longVal))
                return longVal;

            if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture, out var dblVal))
                return dblVal;

            return value;
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
        /// Converts a <see cref="YamlMappingNode"/> to a <see cref="Dictionary{TKey,TValue}"/>
        /// with string keys throughout the tree.
        /// </summary>
        private static Dictionary<string, object?>? ConvertMapping(YamlMappingNode node)
        {
            var dict = new Dictionary<string, object?>();

            foreach (var child in node.Children)
            {
                string key;
                if (child.Key is YamlScalarNode keyScalar)
                {
                    key = keyScalar.Value ?? string.Empty;
                }
                else
                {
                    key = child.Key.ToString();
                }

                // Merge key (<<) — expand the referenced mapping(s) without
                // overwriting explicitly declared keys.
                if (key == "<<")
                {
                    MergeMappingFromMergeKey(dict, child.Value);
                    continue;
                }

                dict[key] = ConvertNode(child.Value);
            }

            return dict;
        }

        /// <summary>
        /// Handles the YAML merge key <c>&lt;&lt;</c>. The value can be:
        /// <list type="bullet">
        ///   <item>A single mapping node (alias to an anchor)</item>
        ///   <item>A sequence of mapping nodes</item>
        /// </list>
        /// Merged values are added to the dictionary without overwriting existing keys.
        /// </summary>
        private static void MergeMappingFromMergeKey(Dictionary<string, object?> target, YamlNode value)
        {
            switch (value)
            {
                case YamlMappingNode mappingNode:
                {
                    var merged = ConvertMapping(mappingNode);
                    if (merged != null)
                    {
                        foreach (var kvp in merged)
                        {
                            if (!target.ContainsKey(kvp.Key))
                            {
                                target[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    break;
                }

                case YamlSequenceNode seqNode:
                {
                    // Multiple merge sources: process in order, later ones have lower priority
                    foreach (var item in seqNode.Children)
                    {
                        if (item is YamlMappingNode mappingItem)
                        {
                            var merged = ConvertMapping(mappingItem);
                            if (merged != null)
                            {
                                foreach (var kvp in merged)
                                {
                                    if (!target.ContainsKey(kvp.Key))
                                    {
                                        target[kvp.Key] = kvp.Value;
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Converts a <see cref="YamlSequenceNode"/> to a <see cref="List{T}"/>.
        /// </summary>
        private static List<object?> ConvertSequence(YamlSequenceNode node)
        {
            var list = new List<object?>(node.Children.Count);
            for (int i = 0; i < node.Children.Count; i++)
            {
                list.Add(ConvertNode(node.Children[i]));
            }
            return list;
        }

        /// <summary>
        /// Dispatches conversion based on the node type.
        /// </summary>
        private static object? ConvertNode(YamlNode node)
        {
            switch (node)
            {
                case YamlMappingNode mapping:
                    return ConvertMapping(mapping);

                case YamlSequenceNode sequence:
                    return ConvertSequence(sequence);

                case YamlScalarNode scalar:
                    return ConvertScalarValue(scalar);

                default:
                    return null;
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
