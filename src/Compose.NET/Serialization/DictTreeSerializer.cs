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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace Compose.NET.Serialization
{
    /// <summary>
    /// Serializes a <c>Dictionary&lt;string, object?&gt;</c> tree to YAML or JSON
    /// without using reflection. Renders YAML through YamlDotNet's representation
    /// model (core emitter) and JSON through <see cref="JsonNode"/> / <see cref="Utf8JsonWriter"/>,
    /// making this fully compatible with ahead-of-time compilation and trimming.
    /// <para>
    /// Null values are omitted from mappings; sequence items are preserved.
    /// </para>
    /// </summary>
    internal static class DictTreeSerializer
    {
        /// <summary>
        /// Renders a dictionary tree as a YAML document.
        /// </summary>
        public static string ToYaml(Dictionary<string, object?> dict)
        {
            var mapping = new YamlMappingNode();
            foreach (var kvp in dict)
            {
                var node = ToYamlNode(kvp.Value);
                if (node != null)
                {
                    mapping.Add(new YamlScalarNode(kvp.Key), node);
                }
            }

            var stream = new YamlStream();
            stream.Documents.Add(new YamlDocument(mapping));

            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                stream.Save(writer, assignAnchors: false);
                return writer.ToString();
            }
        }

        /// <summary>
        /// Renders a dictionary tree as a JSON document.
        /// </summary>
        /// <param name="dict">The dictionary tree to serialize.</param>
        /// <param name="indented">Whether to format the output with indentation.</param>
        public static string ToJson(Dictionary<string, object?> dict, bool indented)
        {
            var root = ToJsonNode(dict);
            var options = new JsonWriterOptions { Indented = indented };

            using (var buffer = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(buffer, options))
                {
                    root!.WriteTo(writer);
                }

                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        /// <summary>
        /// Converts a value to a <see cref="YamlNode"/>, or <c>null</c> if the
        /// value should be omitted.
        /// </summary>
        private static YamlNode? ToYamlNode(object? value)
        {
            switch (value)
            {
                case null:
                    return null;

                case string s:
                    return new YamlScalarNode(s);

                case bool b:
                    return new YamlScalarNode(b ? "true" : "false");

                case int i:
                    return new YamlScalarNode(i.ToString(CultureInfo.InvariantCulture));

                case long l:
                    return new YamlScalarNode(l.ToString(CultureInfo.InvariantCulture));

                case float f:
                    return new YamlScalarNode(f.ToString("R", CultureInfo.InvariantCulture));

                case double d:
                    return new YamlScalarNode(d.ToString("R", CultureInfo.InvariantCulture));

                case decimal m:
                    return new YamlScalarNode(m.ToString(CultureInfo.InvariantCulture));

                case Dictionary<string, object?> dict:
                {
                    var mapping = new YamlMappingNode();
                    foreach (var kvp in dict)
                    {
                        var node = ToYamlNode(kvp.Value);
                        if (node != null)
                        {
                            mapping.Add(new YamlScalarNode(kvp.Key), node);
                        }
                    }
                    return mapping;
                }

                case IDictionary<string, object> strDict:
                {
                    var mapping = new YamlMappingNode();
                    foreach (var kvp in strDict)
                    {
                        var node = ToYamlNode(kvp.Value);
                        if (node != null)
                        {
                            mapping.Add(new YamlScalarNode(kvp.Key), node);
                        }
                    }
                    return mapping;
                }

                case IDictionary untypedDict:
                {
                    var mapping = new YamlMappingNode();
                    foreach (DictionaryEntry entry in untypedDict)
                    {
                        var node = ToYamlNode(entry.Value);
                        if (node != null)
                        {
                            var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture)
                                      ?? string.Empty;
                            mapping.Add(new YamlScalarNode(key), node);
                        }
                    }
                    return mapping;
                }

                case IEnumerable<object?> objectSeq:
                {
                    var sequence = new YamlSequenceNode();
                    foreach (var item in objectSeq)
                    {
                        var node = ToYamlNode(item);
                        if (node != null)
                        {
                            sequence.Add(node);
                        }
                    }
                    return sequence;
                }

                default:
                    return new YamlScalarNode(
                        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        /// <summary>
        /// Converts a value to a <see cref="JsonNode"/>, or <c>null</c> if the
        /// value should be omitted from a mapping.
        /// </summary>
        private static JsonNode? ToJsonNode(object? value)
        {
            switch (value)
            {
                case null:
                    return null;

                case string s:
                    return JsonValue.Create(s);

                case bool b:
                    return JsonValue.Create(b);

                case int i:
                    return JsonValue.Create(i);

                case long l:
                    return JsonValue.Create(l);

                case float f:
                    return JsonValue.Create(f);

                case double d:
                    return JsonValue.Create(d);

                case decimal m:
                    return JsonValue.Create(m);

                case Dictionary<string, object?> dict:
                {
                    var obj = new JsonObject();
                    foreach (var kvp in dict)
                    {
                        var node = ToJsonNode(kvp.Value);
                        if (node != null)
                        {
                            obj[ToCamelCase(kvp.Key)] = node;
                        }
                    }
                    return obj;
                }

                case IDictionary<string, object> strDict:
                {
                    var obj = new JsonObject();
                    foreach (var kvp in strDict)
                    {
                        var node = ToJsonNode(kvp.Value);
                        if (node != null)
                        {
                            obj[ToCamelCase(kvp.Key)] = node;
                        }
                    }
                    return obj;
                }

                case IDictionary untypedDict:
                {
                    var obj = new JsonObject();
                    foreach (DictionaryEntry entry in untypedDict)
                    {
                        var node = ToJsonNode(entry.Value);
                        if (node != null)
                        {
                            var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture)
                                      ?? string.Empty;
                            obj[ToCamelCase(key)] = node;
                        }
                    }
                    return obj;
                }

                case IEnumerable<object?> objectSeq:
                {
                    var array = new JsonArray();
                    foreach (var item in objectSeq)
                    {
                        array.Add(ToJsonNode(item));
                    }
                    return array;
                }

                default:
                    return JsonValue.Create(
                        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        /// <summary>
        /// Applies the same transformation as <see cref="JsonNamingPolicy.CamelCase"/>:
        /// lowercases the first character of the key.
        /// </summary>
        private static string ToCamelCase(string key)
        {
            if (string.IsNullOrEmpty(key) || char.IsLower(key[0]))
                return key;

            return char.ToLowerInvariant(key[0]) + key.Substring(1);
        }
    }
}
