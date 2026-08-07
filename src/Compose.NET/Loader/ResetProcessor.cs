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
using System.Linq;
using System.Text;
using Compose.NET.Interpolation;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Compose.NET.Loader
{
    /// <summary>
    /// Processes <c>!reset</c> and <c>!override</c> YAML tags during parsing.
    /// <para>
    /// In Docker Compose override files, <c>!reset</c> removes a value from the
    /// base configuration, while <c>!override</c> replaces a value entirely
    /// (preventing merge with the base). Both are custom YAML tags defined by
    /// the Compose Specification.
    /// </para>
    /// <para>
    /// Uses YamlDotNet's <see cref="YamlStream"/> (representation model) which
    /// automatically resolves anchors/aliases while preserving tag information.
    /// This ensures correct handling of YAML features like <c>&amp;anchor</c> / <c>*alias</c>
    /// and <c>&lt;&lt;</c> merge keys alongside <c>!reset</c> / <c>!override</c> tags.
    /// </para>
    /// <para>
    /// Mirrors compose-go's <c>ResetProcessor</c> in <c>loader/reset.go</c>.
    /// </para>
    /// </summary>
    internal sealed class ResetProcessor
    {
        private const string ResetTag = "!reset";
        private const string OverrideTag = "!override";

        /// <summary>
        /// Paths that have been marked with <c>!reset</c> or <c>!override</c>.
        /// After merging, <see cref="ApplyNullOverrides"/> uses these to remove
        /// matching keys from the merged base dictionary.
        /// </summary>
        private readonly List<TreePath> _resetPaths = new List<TreePath>();

        /// <summary>
        /// Gets the recorded reset/override paths.
        /// </summary>
        public IReadOnlyList<TreePath> ResetPaths => _resetPaths;

        /// <summary>
        /// Whether any <c>!reset</c> or <c>!override</c> tags were detected.
        /// </summary>
        public bool HasResets => _resetPaths.Count > 0;

        /// <summary>
        /// Parses YAML content with <c>!reset</c> and <c>!override</c> tag detection.
        /// Uses <see cref="YamlStream"/> to correctly handle anchors, aliases, and
        /// merge keys while detecting custom tags.
        /// <para>
        /// Nodes tagged with <c>!reset</c> are removed from the output dictionary.
        /// Nodes tagged with <c>!override</c> are included in the output but their
        /// paths are recorded so the base can be cleared before merge.
        /// </para>
        /// </summary>
        /// <param name="yamlContent">The YAML string to parse.</param>
        /// <returns>
        /// A dictionary representing the YAML document with <c>!reset</c> nodes removed
        /// and <c>!override</c> tags stripped. Returns an empty dictionary if the
        /// document is empty.
        /// </returns>
        public Dictionary<string, object?> ParseWithResetDetection(string yamlContent)
        {
            _resetPaths.Clear();

            if (string.IsNullOrWhiteSpace(yamlContent))
                return new Dictionary<string, object?>();

            var yaml = new YamlStream();
            using (var reader = new StringReader(yamlContent))
            {
                yaml.Load(reader);
            }

            if (yaml.Documents.Count == 0)
                return new Dictionary<string, object?>();

            var root = yaml.Documents[0].RootNode;

            if (root is YamlMappingNode mappingRoot)
            {
                var result = ConvertMapping(mappingRoot, TreePath.New());
                return result ?? new Dictionary<string, object?>();
            }

            // Non-mapping root â€” return empty
            return new Dictionary<string, object?>();
        }

        /// <summary>
        /// Parses YAML content from a byte array with tag detection.
        /// Handles UTF-8 BOM stripping.
        /// </summary>
        public Dictionary<string, object?> ParseWithResetDetection(byte[] content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            string yamlContent = StripBom(content);
            return ParseWithResetDetection(yamlContent);
        }

        /// <summary>
        /// Applies null overrides to the merged dictionary by removing entries
        /// whose paths match any recorded <c>!reset</c> or <c>!override</c> path.
        /// This should be called on the base/merged dictionary BEFORE the override
        /// file's values are merged in.
        /// </summary>
        /// <param name="target">The merged configuration dictionary to modify.</param>
        public void ApplyNullOverrides(Dictionary<string, object?> target)
        {
            if (target == null || _resetPaths.Count == 0)
                return;

            ApplyNullOverridesRecursive(target, TreePath.New());
        }

        /// <summary>
        /// Strips <c>!reset</c> entries from a dictionary that was parsed by the
        /// standard <see cref="YamlParser"/>. This is used when the YAML was already
        /// parsed but needs reset node removal based on previously scanned paths.
        /// </summary>
        /// <param name="dict">The parsed dictionary.</param>
        /// <param name="resetPaths">The paths recorded from <c>!reset</c> tags.</param>
        /// <returns>The dictionary with reset entries removed.</returns>
        public static Dictionary<string, object?> StripResetEntries(
            Dictionary<string, object?> dict,
            IReadOnlyList<TreePath> resetPaths)
        {
            if (dict == null || resetPaths == null || resetPaths.Count == 0)
                return dict ?? new Dictionary<string, object?>();

            StripResetEntriesRecursive(dict, TreePath.New(), resetPaths, isReset: true);
            return dict;
        }

        #region YAML Node Conversion

        /// <summary>
        /// Converts a <see cref="YamlMappingNode"/> to a <see cref="Dictionary{TKey,TValue}"/>,
        /// detecting <c>!reset</c> and <c>!override</c> tags.
        /// Returns <c>null</c> if the mapping itself is tagged with <c>!reset</c>.
        /// </summary>
        private Dictionary<string, object?>? ConvertMapping(YamlMappingNode node, TreePath path)
        {
            // Check tags on the mapping itself
            if (IsResetTag(node.Tag))
            {
                _resetPaths.Add(path);
                return null; // Entire mapping is reset
            }

            if (IsOverrideTag(node.Tag))
            {
                _resetPaths.Add(path);
            }

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

                // Handle merge key (<<) â€” YamlStream resolves anchors but we still
                // see the << key. The merged values should already be present.
                // We skip the << key itself.
                if (key == "<<")
                {
                    // Merge key: process the value(s) as base mappings
                    MergeMappingFromMergeKey(dict, child.Value, path);
                    continue;
                }

                var valuePath = path.Next(key);
                var value = ConvertNode(child.Value, valuePath);

                if (value is ResetMarker)
                {
                    // Node was tagged !reset â€” don't include in dict
                    continue;
                }

                dict[key] = value;
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
        private void MergeMappingFromMergeKey(Dictionary<string, object?> target, YamlNode value, TreePath path)
        {
            switch (value)
            {
                case YamlMappingNode mappingNode:
                {
                    var merged = ConvertMapping(mappingNode, path);
                    if (merged != null)
                    {
                        foreach (var kvp in merged)
                        {
                            // Merge key: base values don't overwrite existing
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
                            var merged = ConvertMapping(mappingItem, path);
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
        /// Converts a <see cref="YamlSequenceNode"/> to a <see cref="List{T}"/>,
        /// detecting tags. Returns <c>null</c> (wrapped as <see cref="ResetMarker"/>)
        /// if the sequence is tagged with <c>!reset</c>.
        /// </summary>
        private object? ConvertSequence(YamlSequenceNode node, TreePath path)
        {
            if (IsResetTag(node.Tag))
            {
                _resetPaths.Add(path);
                return ResetMarker.Instance;
            }

            if (IsOverrideTag(node.Tag))
            {
                _resetPaths.Add(path);
            }

            var list = new List<object?>();
            for (int i = 0; i < node.Children.Count; i++)
            {
                var itemPath = path.Next(i.ToString());
                var item = ConvertNode(node.Children[i], itemPath);

                if (item is not ResetMarker)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        /// <summary>
        /// Converts a <see cref="YamlScalarNode"/> to a .NET scalar value (string, bool, int, etc.),
        /// detecting tags. Returns <see cref="ResetMarker"/> if tagged with <c>!reset</c>.
        /// </summary>
        private object? ConvertScalar(YamlScalarNode node, TreePath path)
        {
            if (IsResetTag(node.Tag))
            {
                _resetPaths.Add(path);
                return ResetMarker.Instance;
            }

            if (IsOverrideTag(node.Tag))
            {
                _resetPaths.Add(path);
            }

            return ConvertScalarValue(node);
        }

        /// <summary>
        /// Dispatches conversion based on the node type.
        /// </summary>
        private object? ConvertNode(YamlNode node, TreePath path)
        {
            switch (node)
            {
                case YamlMappingNode mapping:
                {
                    var dict = ConvertMapping(mapping, path);
                    if (dict == null) return ResetMarker.Instance;
                    return dict;
                }

                case YamlSequenceNode sequence:
                    return ConvertSequence(sequence, path);

                case YamlScalarNode scalar:
                    return ConvertScalar(scalar, path);

                default:
                    return null;
            }
        }

        /// <summary>
        /// Converts a YAML scalar node's value to the appropriate .NET type.
        /// Matches YamlDotNet's Deserializer default behavior: bool, int, long, double, null, or string.
        /// </summary>
        private static object? ConvertScalarValue(YamlScalarNode node)
        {
            return YamlParser.ConvertScalarValue(node);
        }

        #endregion

        #region Apply / Strip

        /// <summary>
        /// Recursively traverses the target dictionary and removes entries at
        /// paths matching any recorded reset/override path.
        /// </summary>
        private void ApplyNullOverridesRecursive(object? target, TreePath path)
        {
            switch (target)
            {
                case Dictionary<string, object?> dict:
                {
                    var keysToRemove = new List<string>();

                    foreach (var kvp in dict)
                    {
                        var next = path.Next(kvp.Key);

                        bool shouldRemove = false;
                        foreach (var pattern in _resetPaths)
                        {
                            if (next.Matches(pattern))
                            {
                                shouldRemove = true;
                                break;
                            }
                        }

                        if (shouldRemove)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                        else
                        {
                            ApplyNullOverridesRecursive(kvp.Value, next);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        dict.Remove(key);
                    }

                    break;
                }

                case List<object?> list:
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var next = path.Next($"[{i}]");

                        bool shouldRemove = false;
                        foreach (var pattern in _resetPaths)
                        {
                            if (next.Matches(pattern))
                            {
                                shouldRemove = true;
                                break;
                            }
                        }

                        if (!shouldRemove)
                        {
                            ApplyNullOverridesRecursive(list[i], next);
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Recursively strips entries whose paths match reset paths.
        /// When <paramref name="isReset"/> is true, only removes nodes matching
        /// paths flagged as !reset (not !override, which keeps its value).
        /// </summary>
        private static void StripResetEntriesRecursive(
            object? target,
            TreePath path,
            IReadOnlyList<TreePath> resetPaths,
            bool isReset)
        {
            switch (target)
            {
                case Dictionary<string, object?> dict:
                {
                    var keysToRemove = new List<string>();

                    foreach (var kvp in dict)
                    {
                        var next = path.Next(kvp.Key);

                        bool matches = false;
                        foreach (var pattern in resetPaths)
                        {
                            if (next.Matches(pattern))
                            {
                                matches = true;
                                break;
                            }
                        }

                        if (matches)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                        else
                        {
                            StripResetEntriesRecursive(kvp.Value, next, resetPaths, isReset);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        dict.Remove(key);
                    }

                    break;
                }

                case List<object?> list:
                {
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var next = path.Next(i.ToString());

                        bool matches = false;
                        foreach (var pattern in resetPaths)
                        {
                            if (next.Matches(pattern))
                            {
                                matches = true;
                                break;
                            }
                        }

                        if (matches)
                        {
                            list.RemoveAt(i);
                        }
                        else
                        {
                            StripResetEntriesRecursive(list[i], next, resetPaths, isReset);
                        }
                    }

                    break;
                }
            }
        }

        #endregion

        #region Tag helpers

        private static bool IsResetTag(TagName tag)
        {
            return !tag.IsEmpty && tag.Value == ResetTag;
        }

        private static bool IsOverrideTag(TagName tag)
        {
            return !tag.IsEmpty && tag.Value == OverrideTag;
        }

        #endregion

        #region BOM

        private static string StripBom(byte[] bytes)
        {
            if (bytes.Length >= 3 &&
                bytes[0] == 0xEF &&
                bytes[1] == 0xBB &&
                bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            return Encoding.UTF8.GetString(bytes);
        }

        #endregion

        /// <summary>
        /// Sentinel object indicating a <c>!reset</c> tag was encountered.
        /// Used internally during parsing to filter out reset nodes.
        /// </summary>
        private sealed class ResetMarker
        {
            public static readonly ResetMarker Instance = new ResetMarker();
            private ResetMarker() { }
        }
    }
}
