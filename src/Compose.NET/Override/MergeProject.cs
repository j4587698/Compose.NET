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
using System.Linq;

namespace Compose.NET.Override
{
    /// <summary>
    /// Implements multi-file merge logic for Docker Compose configuration files.
    /// Mirrors the merge/override behavior from compose-go/override.
    /// <para>
    /// All operations work on raw <see cref="Dictionary{TKey,TValue}"/> trees
    /// (untyped object graphs) rather than strongly-typed models, allowing merges
    /// to be applied before or after type mapping.
    /// </para>
    /// </summary>
    internal static class MergeProject
    {
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // MergeSpecials â€” path-based special merge rules
        // Mirrors compose-go/override/merge.go mergeSpecials init()
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Delegate for special merge functions. Receives the current path for nested path-aware merging.
        /// </summary>
        private delegate object? Merger(object? baseValue, object? overrideValue, string[] path);

        /// <summary>
        /// Path-pattern â†’ merger mappings. Patterns use <c>*</c> as a single-segment wildcard.
        /// Populated once by <see cref="BuildMergeSpecials"/>.
        /// </summary>
        private static readonly List<(string[] segments, Merger merger)> MergeSpecialsTable = BuildMergeSpecials();

        private static List<(string[] segments, Merger merger)> BuildMergeSpecials()
        {
            var table = new List<(string[] segments, Merger merger)>();

            void Register(string pattern, Merger merger)
            {
                table.Add((pattern.Split('.'), merger));
            }

            // â”€â”€ override (full replacement) â”€â”€
            Register("services.*.command",                Override);
            Register("services.*.entrypoint",             Override);
            Register("services.*.healthcheck.test",       Override);

            // â”€â”€ mergeToSequence (convert both sides to sequences, then append) â”€â”€
            Register("networks.*.labels",                           MergeToSequence);
            Register("volumes.*.labels",                            MergeToSequence);
            Register("services.*.annotations",                      MergeToSequence);
            Register("services.*.build.args",                       MergeToSequence);
            Register("services.*.build.additional_contexts",        MergeToSequence);
            Register("services.*.build.extra_hosts",                MergeExtraHosts);
            Register("services.*.build.labels",                     MergeToSequence);
            Register("services.*.depends_on",                       MergeDependsOn);
            Register("services.*.deploy.labels",                    MergeToSequence);
            Register("services.*.dns",                              MergeToSequence);
            Register("services.*.dns_opt",                          MergeToSequence);
            Register("services.*.dns_search",                       MergeToSequence);
            Register("services.*.env_file",                         MergeToSequence);
            Register("services.*.label_file",                       MergeToSequence);
            Register("services.*.environment",                      MergeToSequence);
            Register("services.*.extra_hosts",                      MergeExtraHosts);
            Register("services.*.labels",                           MergeToSequence);
            Register("services.*.volumes.*.volume.labels",          MergeToSequence);
            Register("services.*.sysctls",                          MergeToSequence);
            Register("services.*.tmpfs",                            MergeToSequence);

            // â”€â”€ special mergers â”€â”€
            Register("services.*.build",                  MergeBuild);
            Register("services.*.logging",                MergeLogging);
            Register("services.*.models",                 MergeModels);
            Register("services.*.networks",               MergeNetworks);
            Register("services.*.ulimits.*",              MergeUlimit);
            Register("networks.*.ipam.config",            MergeIPAMConfig);

            return table;
        }

        /// <summary>
        /// Service-level keys that should NOT be inherited from the base service
        /// during an <c>extends</c> operation.
        /// </summary>
        private static readonly HashSet<string> NonInheritedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "depends_on"
        };

        /// <summary>
        /// Merges two raw configuration dictionaries. The <paramref name="overrideConfig"/>
        /// values take precedence over <paramref name="baseConfig"/> values.
        /// <para>
        /// Uses path-based merge specials (30 rules) matching compose-go's merge behavior:
        /// <c>override</c> for command/entrypoint/healthcheck.test;
        /// <c>mergeToSequence</c> for labels/environment/dns/etc;
        /// specialized mergers for build/logging/networks/depends_on/ulimits/IPAM.
        /// </para>
        /// </summary>
        /// <param name="baseConfig">The base configuration dictionary.</param>
        /// <param name="overrideConfig">The override configuration dictionary whose values take precedence.</param>
        /// <returns>A new merged configuration dictionary.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="baseConfig"/> or <paramref name="overrideConfig"/> is <see langword="null"/>.
        /// </exception>
        public static Dictionary<string, object?> MergeYaml(
            Dictionary<string, object?> baseConfig,
            Dictionary<string, object?> overrideConfig)
        {
            if (baseConfig == null) throw new ArgumentNullException(nameof(baseConfig));
            if (overrideConfig == null) throw new ArgumentNullException(nameof(overrideConfig));

            return MergeMappingsAtPath(baseConfig, overrideConfig, Array.Empty<string>());
        }

        /// <summary>
        /// Merges the <c>services</c> sections from two configuration files.
        /// Same-name services have their properties merged; new services are added.
        /// </summary>
        /// <param name="baseServices">The base services dictionary (service name -> service config).</param>
        /// <param name="overrideServices">The override services dictionary.</param>
        /// <returns>A new merged services dictionary.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="baseServices"/> or <paramref name="overrideServices"/> is <see langword="null"/>.
        /// </exception>
        public static Dictionary<string, object?> MergeServices(
            Dictionary<string, object?> baseServices,
            Dictionary<string, object?> overrideServices)
        {
            if (baseServices == null) throw new ArgumentNullException(nameof(baseServices));
            if (overrideServices == null) throw new ArgumentNullException(nameof(overrideServices));

            return MergeMappingsAtPath(baseServices, overrideServices, new[] { "services" });
        }

        /// <summary>
        /// Implements the <c>extends</c> directive: merges a base (source) service with
        /// a child (target) service. The child's values override the base's values.
        /// <list type="bullet">
        ///   <item>Keys in <see cref="NonInheritedKeys"/> (e.g. <c>depends_on</c>) are NOT inherited from the base.</item>
        ///   <item>All other keys use path-based merge specials (same as multi-file merge).</item>
        /// </list>
        /// </summary>
        /// <param name="source">The base service configuration to inherit from.</param>
        /// <param name="target">The child service configuration whose values take precedence.</param>
        /// <returns>A new merged service configuration dictionary.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="source"/> or <paramref name="target"/> is <see langword="null"/>.
        /// </exception>
        public static Dictionary<string, object?> ExtendService(
            Dictionary<string, object?> source,
            Dictionary<string, object?> target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            // Filter out non-inherited keys from source
            var filteredSource = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in source)
            {
                if (!NonInheritedKeys.Contains(kvp.Key))
                {
                    filteredSource[kvp.Key] = kvp.Value;
                }
            }

            // Use path-based merge with "services.*" context
            return MergeMappingsAtPath(filteredSource, target, new[] { "services", "_" });
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Path-aware recursive merge engine
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Merges two mappings at a given path context, consulting MergeSpecials
        /// for each child key.
        /// </summary>
        private static Dictionary<string, object?> MergeMappingsAtPath(
            Dictionary<string, object?> baseDict,
            Dictionary<string, object?> overrideDict,
            string[] currentPath)
        {
            var result = (Dictionary<string, object?>)DeepClone(baseDict)!;

            foreach (var kvp in overrideDict)
            {
                if (!result.TryGetValue(kvp.Key, out var baseValue))
                {
                    result[kvp.Key] = DeepClone(kvp.Value);
                    continue;
                }

                var childPath = AppendPath(currentPath, kvp.Key);
                result[kvp.Key] = MergeValueAtPath(baseValue, kvp.Value, childPath);
            }

            return result;
        }

        /// <summary>
        /// Merges two values at a specific path. First checks MergeSpecials for a
        /// matching merger; otherwise falls back to default merge behavior.
        /// </summary>
        private static object? MergeValueAtPath(object? baseValue, object? overrideValue, string[] path)
        {
            // Check for a matching special merger
            var merger = FindMerger(path);
            if (merger != null)
            {
                return merger(baseValue, overrideValue, path);
            }

            // Default merge behavior
            if (baseValue is Dictionary<string, object?> baseDict
                && overrideValue is Dictionary<string, object?> overrideDict)
            {
                return MergeMappingsAtPath(baseDict, overrideDict, path);
            }

            if (baseValue is List<object?> baseList && overrideValue is List<object?> overrideList)
            {
                // Default for lists: append
                var appended = new List<object?>(baseList.Count + overrideList.Count);
                foreach (var item in baseList) appended.Add(DeepClone(item));
                foreach (var item in overrideList) appended.Add(DeepClone(item));
                return appended;
            }

            // Scalar: override wins
            return DeepClone(overrideValue);
        }

        /// <summary>
        /// Finds the first matching merger for the given path.
        /// </summary>
        private static Merger? FindMerger(string[] path)
        {
            foreach (var (segments, merger) in MergeSpecialsTable)
            {
                if (PathMatches(path, segments))
                {
                    return merger;
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if an actual path matches a pattern (with <c>*</c> wildcards).
        /// </summary>
        private static bool PathMatches(string[] actual, string[] pattern)
        {
            if (actual.Length != pattern.Length) return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == "*") continue;
                if (!string.Equals(actual[i], pattern[i], StringComparison.Ordinal)) return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a new path array by appending a segment.
        /// </summary>
        private static string[] AppendPath(string[] path, string segment)
        {
            var result = new string[path.Length + 1];
            Array.Copy(path, result, path.Length);
            result[path.Length] = segment;
            return result;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Special merger functions â€” mirror compose-go/override/merge.go
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Full replacement: override completely replaces base.
        /// Used for: command, entrypoint, healthcheck.test.
        /// </summary>
        private static object? Override(object? baseValue, object? overrideValue, string[] path)
        {
            return DeepClone(overrideValue);
        }

        /// <summary>
        /// Converts both sides to sequences (expanding maps to KEY=VALUE lists),
        /// then appends override to base.
        /// Used for: labels, environment, dns, dns_search, env_file, build.args, etc.
        /// </summary>
        private static object? MergeToSequence(object? baseValue, object? overrideValue, string[] path)
        {
            var baseSeq = ConvertIntoSequence(baseValue);
            var overrideSeq = ConvertIntoSequence(overrideValue);

            var result = new List<object?>(baseSeq.Count + overrideSeq.Count);
            result.AddRange(baseSeq);
            result.AddRange(overrideSeq);
            return result;
        }

        /// <summary>
        /// Normalizes build to a map (string â†’ {context: string}), then merges as mappings.
        /// </summary>
        private static object? MergeBuild(object? baseValue, object? overrideValue, string[] path)
        {
            var baseDict = NormalizeBuildToMap(baseValue);
            var overrideDict = NormalizeBuildToMap(overrideValue);
            return MergeMappingsAtPath(baseDict, overrideDict, path);
        }

        /// <summary>
        /// Normalizes depends_on to a map (list â†’ {svc: {condition: service_started, required: true}}),
        /// then merges as mappings.
        /// </summary>
        private static object? MergeDependsOn(object? baseValue, object? overrideValue, string[] path)
        {
            var baseDict = NormalizeDependsOnToMap(baseValue);
            var overrideDict = NormalizeDependsOnToMap(overrideValue);
            return MergeMappingsAtPath(baseDict, overrideDict, path);
        }

        /// <summary>
        /// Normalizes models to a map (list â†’ {name: null}), then merges as mappings.
        /// </summary>
        private static object? MergeModels(object? baseValue, object? overrideValue, string[] path)
        {
            var baseDict = NormalizeSequenceToMap(baseValue);
            var overrideDict = NormalizeSequenceToMap(overrideValue);
            return MergeMappingsAtPath(baseDict, overrideDict, path);
        }

        /// <summary>
        /// Normalizes networks to a map (list â†’ {name: null}), then merges as mappings.
        /// </summary>
        private static object? MergeNetworks(object? baseValue, object? overrideValue, string[] path)
        {
            var baseDict = NormalizeSequenceToMap(baseValue);
            var overrideDict = NormalizeSequenceToMap(overrideValue);
            return MergeMappingsAtPath(baseDict, overrideDict, path);
        }

        /// <summary>
        /// Merges extra_hosts: converts both to sequences, then deduplicates by hostname
        /// (the key part of the HOST=IP entry). Base entries whose hostname also appears
        /// in override are replaced; remaining override entries are appended.
        /// </summary>
        private static object? MergeExtraHosts(object? baseValue, object? overrideValue, string[] path)
        {
            var baseSeq = ConvertIntoSequence(baseValue);
            var overrideSeq = ConvertIntoSequence(overrideValue);

            // Build set of override hostnames
            var overrideKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in overrideSeq)
            {
                if (item is string s)
                {
                    var eqIdx = s.IndexOf('=');
                    var colonIdx = s.IndexOf(':');
                    var sep = eqIdx >= 0 ? eqIdx : colonIdx;
                    overrideKeys.Add(sep >= 0 ? s.Substring(0, sep) : s);
                }
            }

            // Keep base entries whose hostname is NOT in override
            var result = new List<object?>();
            foreach (var item in baseSeq)
            {
                if (item is string s)
                {
                    var eqIdx = s.IndexOf('=');
                    var colonIdx = s.IndexOf(':');
                    var sep = eqIdx >= 0 ? eqIdx : colonIdx;
                    var hostname = sep >= 0 ? s.Substring(0, sep) : s;
                    if (!overrideKeys.Contains(hostname))
                    {
                        result.Add(item);
                    }
                }
                else
                {
                    result.Add(item);
                }
            }

            result.AddRange(overrideSeq);
            return result;
        }

        /// <summary>
        /// Merges logging config: if both have a "driver" field and they differ,
        /// override completely replaces base. Otherwise, merge as mappings.
        /// </summary>
        private static object? MergeLogging(object? baseValue, object? overrideValue, string[] path)
        {
            if (baseValue is Dictionary<string, object?> baseDict
                && overrideValue is Dictionary<string, object?> overrideDict)
            {
                var baseDriver = GetStringValue(baseDict, "driver");
                var overrideDriver = GetStringValue(overrideDict, "driver");

                // If both have drivers and they differ, override wins completely
                if (baseDriver != null && overrideDriver != null
                    && !string.Equals(baseDriver, overrideDriver, StringComparison.Ordinal))
                {
                    return DeepClone(overrideValue);
                }

                // Same driver or one is missing â€” deep merge
                return MergeMappingsAtPath(baseDict, overrideDict, path);
            }

            return DeepClone(overrideValue);
        }

        /// <summary>
        /// Merges ulimit: if override is a map, merge as mapping; otherwise override replaces.
        /// Handles the case where ulimit can be a single int or a {soft, hard} map.
        /// </summary>
        private static object? MergeUlimit(object? baseValue, object? overrideValue, string[] path)
        {
            if (overrideValue is Dictionary<string, object?> overrideDict)
            {
                if (baseValue is Dictionary<string, object?> baseDict)
                {
                    return MergeMappingsAtPath(baseDict, overrideDict, path);
                }
                // base is a scalar, override is a map â€” override wins
                return DeepClone(overrideValue);
            }

            // Override is a scalar (single int value) â€” replaces base entirely
            return DeepClone(overrideValue);
        }

        /// <summary>
        /// Merges IPAM config arrays by subnet. Entries with the same subnet are
        /// deep-merged; entries with different subnets are appended.
        /// </summary>
        private static object? MergeIPAMConfig(object? baseValue, object? overrideValue, string[] path)
        {
            var baseList = baseValue as List<object?> ?? new List<object?>();
            var overrideList = overrideValue as List<object?> ?? new List<object?>();

            // Index base entries by subnet
            var result = new List<object?>();
            var subnetIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var item in baseList)
            {
                var subnet = GetSubnet(item);
                if (subnet != null)
                {
                    subnetIndex[subnet] = result.Count;
                }
                result.Add(DeepClone(item));
            }

            foreach (var item in overrideList)
            {
                var subnet = GetSubnet(item);
                if (subnet != null && subnetIndex.TryGetValue(subnet, out var idx))
                {
                    // Merge with existing base entry
                    if (result[idx] is Dictionary<string, object?> baseEntry
                        && item is Dictionary<string, object?> overrideEntry)
                    {
                        result[idx] = MergeMappingsAtPath(baseEntry, overrideEntry, path);
                    }
                    else
                    {
                        result[idx] = DeepClone(item);
                    }
                }
                else
                {
                    if (subnet != null)
                    {
                        subnetIndex[subnet] = result.Count;
                    }
                    result.Add(DeepClone(item));
                }
            }

            return result;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Normalization helpers for special mergers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Converts a value into a sequence for mergeToSequence.
        /// Maps (KEYâ†’VALUE) are expanded to ["KEY=VALUE", ...].
        /// </summary>
        private static List<object?> ConvertIntoSequence(object? value)
        {
            if (value is List<object?> list)
            {
                return list;
            }

            if (value is Dictionary<string, object?> dict)
            {
                var result = new List<object?>(dict.Count);
                foreach (var kvp in dict)
                {
                    if (kvp.Value is List<object?> multiValues)
                    {
                        foreach (var v in multiValues)
                        {
                            result.Add($"{kvp.Key}={v}");
                        }
                    }
                    else if (kvp.Value != null)
                    {
                        result.Add($"{kvp.Key}={kvp.Value}");
                    }
                    else
                    {
                        result.Add(kvp.Key);
                    }
                }
                return result;
            }

            if (value == null) return new List<object?>();

            return new List<object?> { value };
        }

        /// <summary>
        /// Normalizes a build value: string â†’ {"context": string}, already map â†’ as-is.
        /// </summary>
        private static Dictionary<string, object?> NormalizeBuildToMap(object? value)
        {
            if (value is Dictionary<string, object?> dict) return dict;

            if (value is string s)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["context"] = s };
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Normalizes depends_on: list ["db","redis"] â†’ {"db": {condition: "service_started", required: true}, ...}.
        /// Already a map â†’ as-is.
        /// </summary>
        private static Dictionary<string, object?> NormalizeDependsOnToMap(object? value)
        {
            if (value is Dictionary<string, object?> dict) return dict;

            if (value is List<object?> list)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var item in list)
                {
                    if (item is string name)
                    {
                        result[name] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["condition"] = "service_started",
                            ["required"] = true
                        };
                    }
                }
                return result;
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Normalizes a sequence to a map: ["a","b"] â†’ {"a": null, "b": null}.
        /// Already a map â†’ as-is. Used for networks and models.
        /// </summary>
        private static Dictionary<string, object?> NormalizeSequenceToMap(object? value)
        {
            if (value is Dictionary<string, object?> dict) return dict;

            if (value is List<object?> list)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var item in list)
                {
                    if (item is string name)
                    {
                        result[name] = null;
                    }
                }
                return result;
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Extracts the subnet from an IPAM config entry.
        /// </summary>
        private static string? GetSubnet(object? item)
        {
            if (item is Dictionary<string, object?> dict)
            {
                return GetStringValue(dict, "subnet");
            }
            return null;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // EnforceUnicity â€” path-based deduplication
        // Mirrors compose-go/override/uncity.go
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Delegate that extracts a unique key from a list element for deduplication.
        /// Returns <see langword="null"/> when the element cannot be indexed (it will be kept as-is).
        /// </summary>
        private delegate string? Indexer(object? element);

        /// <summary>
        /// Path-pattern â†’ indexer mappings. Patterns use <c>*</c> as a single-segment wildcard.
        /// Populated once by <see cref="BuildIndexerTable"/>.
        /// </summary>
        private static readonly List<(string[] segments, Indexer indexer)> IndexerTable = BuildIndexerTable();

        private static List<(string[] segments, Indexer indexer)> BuildIndexerTable()
        {
            var table = new List<(string[] segments, Indexer indexer)>();

            void Register(string pattern, Indexer indexer)
            {
                table.Add((pattern.Split('.'), indexer));
            }

            // â”€â”€ keyValueIndexer paths â”€â”€
            Register("networks.*.labels",                   KeyValueIndexer);
            Register("networks.*.ipam.options",             KeyValueIndexer);
            Register("services.*.annotations",              KeyValueIndexer);
            Register("services.*.build.args",               KeyValueIndexer);
            Register("services.*.build.additional_contexts", KeyValueIndexer);
            Register("services.*.build.platform",           KeyValueIndexer);
            Register("services.*.build.tags",               KeyValueIndexer);
            Register("services.*.build.labels",             KeyValueIndexer);
            Register("services.*.cap_add",                  KeyValueIndexer);
            Register("services.*.cap_drop",                 KeyValueIndexer);
            Register("services.*.configs",                  MountIndexer(""));
            Register("services.*.deploy.labels",            KeyValueIndexer);
            Register("services.*.dns",                      KeyValueIndexer);
            Register("services.*.dns_opt",                  KeyValueIndexer);
            Register("services.*.dns_search",               KeyValueIndexer);
            Register("services.*.environment",              KeyValueIndexer);
            Register("services.*.env_file",                 EnvFileIndexer);
            Register("services.*.expose",                   ExposeIndexer);
            Register("services.*.labels",                   KeyValueIndexer);
            Register("services.*.links",                    KeyValueIndexer);
            Register("services.*.networks.*.aliases",       KeyValueIndexer);
            Register("services.*.networks.*.link_local_ips", KeyValueIndexer);
            Register("services.*.ports",                    PortIndexer);
            Register("services.*.profiles",                 KeyValueIndexer);
            Register("services.*.secrets",                  MountIndexer("/run/secrets"));
            Register("services.*.sysctls",                  KeyValueIndexer);
            Register("services.*.tmpfs",                    KeyValueIndexer);
            Register("services.*.volumes",                  VolumeIndexer);
            Register("services.*.devices",                  DeviceMappingIndexer);

            return table;
        }

        /// <summary>
        /// Enforces uniqueness on all lists in the configuration tree that have a
        /// registered indexer. Later entries take precedence over earlier ones.
        /// <para>
        /// This mirrors compose-go's <c>EnforceUnicity</c> in <c>override/uncity.go</c>,
        /// which deduplicates sequences using ~29 path-specific indexers covering ports,
        /// volumes, devices, configs, secrets, environment, labels, DNS, and more.
        /// </para>
        /// </summary>
        /// <param name="config">The full raw configuration dictionary (top-level).</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="config"/> is <see langword="null"/>.
        /// </exception>
        public static void EnforceUnicity(Dictionary<string, object?> config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            foreach (var (segments, indexer) in IndexerTable)
            {
                ApplyIndexer(config, segments, 0, indexer);
            }
        }

        /// <summary>
        /// Recursively walks the config tree following the path pattern segments.
        /// When the full path is matched, applies the indexer to deduplicate the list at that location.
        /// </summary>
        private static void ApplyIndexer(Dictionary<string, object?> current, string[] segments, int depth, Indexer indexer)
        {
            if (depth >= segments.Length) return;

            var segment = segments[depth];
            bool isLast = depth == segments.Length - 1;

            if (segment == "*")
            {
                // Wildcard â€” iterate all keys
                foreach (var kvp in current)
                {
                    if (isLast)
                    {
                        if (kvp.Value is List<object?> list)
                        {
                            current[kvp.Key] = DeduplicateByIndexer(list, indexer);
                        }
                    }
                    else if (kvp.Value is Dictionary<string, object?> child)
                    {
                        ApplyIndexer(child, segments, depth + 1, indexer);
                    }
                }
            }
            else
            {
                if (!current.TryGetValue(segment, out var value)) return;

                if (isLast)
                {
                    if (value is List<object?> list)
                    {
                        current[segment] = DeduplicateByIndexer(list, indexer);
                    }
                }
                else if (value is Dictionary<string, object?> child)
                {
                    ApplyIndexer(child, segments, depth + 1, indexer);
                }
            }
        }

        /// <summary>
        /// Deduplicates a list using the provided indexer. Later entries with the same key
        /// replace earlier entries.
        /// </summary>
        private static List<object?> DeduplicateByIndexer(List<object?> items, Indexer indexer)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var result = new List<object?>();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var key = indexer(item);

                if (key != null && seen.TryGetValue(key, out var existingIndex))
                {
                    // Replace the existing entry (later wins)
                    result[existingIndex] = item;
                }
                else
                {
                    if (key != null)
                    {
                        seen[key] = result.Count;
                    }
                    result.Add(item);
                }
            }

            return result;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Indexer functions â€” mirror compose-go/override/uncity.go
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Extracts the key from a <c>KEY=VALUE</c> string or plain string.
        /// </summary>
        private static string? KeyValueIndexer(object? element)
        {
            if (element is string s)
            {
                var eqIndex = s.IndexOf('=');
                return eqIndex >= 0 ? s.Substring(0, eqIndex) : s;
            }
            return element?.ToString();
        }

        /// <summary>
        /// Extracts the target path from a volume mount (map or short syntax string).
        /// </summary>
        private static string? VolumeIndexer(object? element)
        {
            if (element is Dictionary<string, object?> dict)
            {
                return GetStringValue(dict, "target");
            }

            if (element is string s)
            {
                // Short syntax: source:target:opts or just target
                var parts = s.Split(':');
                return parts.Length switch
                {
                    1 => parts[0],
                    _ => parts[1]
                };
            }

            return null;
        }

        /// <summary>
        /// Extracts the target device path from a device mapping (map or short syntax string).
        /// </summary>
        private static string? DeviceMappingIndexer(object? element)
        {
            if (element is Dictionary<string, object?> dict)
            {
                return GetStringValue(dict, "target");
            }

            if (element is string s)
            {
                // Short syntax: host:container or just device
                var parts = s.Split(':');
                return parts.Length >= 2 ? parts[1] : parts[0];
            }

            return null;
        }

        /// <summary>
        /// Returns a mount indexer that uses the target path as key.
        /// For string elements, the target is <c>defaultPath/name</c>.
        /// </summary>
        private static Indexer MountIndexer(string defaultPath)
        {
            return element =>
            {
                if (element is string s)
                {
                    return string.IsNullOrEmpty(defaultPath) ? "/" + s : defaultPath + "/" + s;
                }

                if (element is Dictionary<string, object?> dict)
                {
                    if (dict.TryGetValue("target", out var target) && target != null)
                    {
                        return target.ToString();
                    }
                    // Fall back to source
                    if (dict.TryGetValue("source", out var source) && source != null)
                    {
                        return string.IsNullOrEmpty(defaultPath)
                            ? "/" + source
                            : defaultPath + "/" + source;
                    }
                }

                return null;
            };
        }

        /// <summary>
        /// Extracts the port value from an expose entry (string or int).
        /// </summary>
        private static string? ExposeIndexer(object? element)
        {
            if (element is string s) return s;
            if (element is int i) return i.ToString();
            if (element is long l) return l.ToString();
            return element?.ToString();
        }

        /// <summary>
        /// Builds a composite key for a port entry: <c>{host_ip}:{published}:{target}/{protocol}</c>.
        /// Handles int, string, and dict forms.
        /// </summary>
        private static string? PortIndexer(object? element)
        {
            if (element is int i) return i.ToString();
            if (element is long l) return l.ToString();
            if (element is string s) return s;

            if (element is Dictionary<string, object?> dict)
            {
                var hostIp = GetStringValue(dict, "host_ip") ?? "0.0.0.0";
                var published = GetStringValue(dict, "published") ?? "";
                var target = GetStringValue(dict, "target") ?? "";
                var protocol = GetStringValue(dict, "protocol") ?? "tcp";
                return $"{hostIp}:{published}:{target}/{protocol}";
            }

            return null;
        }

        /// <summary>
        /// Extracts the file path from an env_file entry (string or dict with "path" key).
        /// </summary>
        private static string? EnvFileIndexer(object? element)
        {
            if (element is string s) return s;

            if (element is Dictionary<string, object?> dict)
            {
                return GetStringValue(dict, "path");
            }

            return null;
        }

        /// <summary>
        /// Merges two dictionaries generically. Values from <paramref name="overrideDict"/>
        /// take precedence. Nested dictionaries are merged recursively.
        /// </summary>
        /// <param name="baseDict">The base dictionary.</param>
        /// <param name="overrideDict">The override dictionary whose values take precedence.</param>
        /// <returns>A new merged dictionary.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="baseDict"/> or <paramref name="overrideDict"/> is <see langword="null"/>.
        /// </exception>
        public static Dictionary<string, object?> MergeMapping(
            Dictionary<string, object?> baseDict,
            Dictionary<string, object?> overrideDict)
        {
            if (baseDict == null) throw new ArgumentNullException(nameof(baseDict));
            if (overrideDict == null) throw new ArgumentNullException(nameof(overrideDict));

            var result = (Dictionary<string, object?>)DeepClone(baseDict)!;

            foreach (var kvp in overrideDict)
            {
                if (result.TryGetValue(kvp.Key, out var baseValue)
                    && baseValue is Dictionary<string, object?> nestedBase
                    && kvp.Value is Dictionary<string, object?> nestedOverride)
                {
                    result[kvp.Key] = MergeMapping(nestedBase, nestedOverride);
                }
                else
                {
                    result[kvp.Key] = DeepClone(kvp.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a deep clone of an untyped object tree (dictionaries, lists, and scalars).
        /// </summary>
        /// <param name="value">The value to clone. May be <see langword="null"/>.</param>
        /// <returns>A deep copy of the value, or <see langword="null"/> if the input is <see langword="null"/>.</returns>
        internal static object? DeepClone(object? value)
        {
            switch (value)
            {
                case null:
                    return null;

                case Dictionary<string, object?> dict:
                {
                    var clone = new Dictionary<string, object?>(dict.Count, StringComparer.Ordinal);
                    foreach (var kvp in dict)
                    {
                        clone[kvp.Key] = DeepClone(kvp.Value);
                    }
                    return clone;
                }

                case List<object?> list:
                {
                    var clone = new List<object?>(list.Count);
                    for (int i = 0; i < list.Count; i++)
                    {
                        clone.Add(DeepClone(list[i]));
                    }
                    return clone;
                }

                // Scalars (string, int, long, bool, double, etc.) are immutable â€” return as-is
                default:
                    return value;
            }
        }

        /// <summary>
        /// Safely retrieves a string value from a dictionary, converting the value to string
        /// if it is not <see langword="null"/>.
        /// </summary>
        private static string? GetStringValue(Dictionary<string, object?> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString();
            }

            return null;
        }
    }
}
