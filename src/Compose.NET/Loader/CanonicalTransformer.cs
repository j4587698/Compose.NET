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

using System.Globalization;
using Compose.NET.Types;

namespace Compose.NET.Loader
{
    /// <summary>
    /// Transforms short-syntax Compose values into their canonical long-syntax form.
    /// Mirrors compose-go's <c>transform.Canonical()</c>.
    /// </summary>
    internal static class CanonicalTransformer
    {
        /// <summary>
        /// Registered path patterns and their associated transformer functions.
        /// Each transformer takes the current value at that path and returns the canonical form.
        /// </summary>
        private static readonly (string Pattern, Func<object?, object?> Transformer)[] Transformers =
        {
            ("services.*.build", TransformBuild),
            ("services.*.depends_on", TransformDependsOn),
            ("services.*.env_file", TransformEnvFile),
            ("services.*.extends", TransformExtends),
            ("services.*.networks", TransformNetworks),
            ("services.*.models", TransformModels),
            ("services.*.ports", TransformPorts),
            ("services.*.volumes.*", TransformVolumeMount),
            ("services.*.secrets.*", TransformFileMount),
            ("services.*.configs.*", TransformFileMount),
            ("services.*.build.secrets.*", TransformFileMount),
            ("services.*.build.provenance", TransformStringOrX),
            ("services.*.build.sbom", TransformStringOrX),
            ("services.*.build.ulimits.*", TransformUlimits),
            ("services.*.dns", TransformDns),
            ("services.*.label_file", TransformLabelFile),
            ("services.*.ulimits.*", TransformUlimits),
            ("services.*.develop.watch.*.ignore", TransformStringOrList),
            ("services.*.develop.watch.*.include", TransformStringOrList),
            ("volumes.*", TransformMaybeExternal),
            ("networks.*", TransformMaybeExternal),
            ("secrets.*", TransformMaybeExternal),
            ("configs.*", TransformMaybeExternal),
            ("include.*", TransformInclude),
            ("services.*.devices.*", TransformDeviceMapping),
            ("services.*.build.ssh", TransformSSH),
            ("services.*.build.additional_contexts", TransformKeyValue),
            ("services.*.gpus", TransformGpus),
        };

        /// <summary>
        /// Transforms a Compose config dictionary from short-syntax to canonical long-syntax form.
        /// </summary>
        /// <param name="dict">The raw config dictionary parsed from YAML.</param>
        /// <returns>A new dictionary with all short-syntax values expanded to their canonical form.</returns>
        public static Dictionary<string, object?> Transform(Dictionary<string, object?> dict)
        {
            var result = TransformRecursive(dict, "") as Dictionary<string, object?>;
            return result ?? new Dictionary<string, object?>();
        }

        /// <summary>
        /// Recursively walks the config structure and applies transformers at matching paths.
        /// </summary>
        private static object? TransformRecursive(object? value, string currentPath)
        {
            // First, apply any transformer that matches this path
            value = ApplyTransformer(value, currentPath);

            switch (value)
            {
                case Dictionary<string, object?> dict:
                {
                    var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var kvp in dict)
                    {
                        var childPath = string.IsNullOrEmpty(currentPath)
                            ? kvp.Key
                            : currentPath + "." + kvp.Key;
                        result[kvp.Key] = TransformRecursive(kvp.Value, childPath);
                    }

                    return result;
                }
                case List<object?> list:
                {
                    var result = new List<object?>(list.Count);
                    for (var i = 0; i < list.Count; i++)
                    {
                        var childPath = currentPath + ".*";
                        result.Add(TransformRecursive(list[i], childPath));
                    }

                    return result;
                }
                default:
                    return value;
            }
        }

        /// <summary>
        /// Applies the first matching transformer for the given path.
        /// </summary>
        private static object? ApplyTransformer(object? value, string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath))
                return value;

            foreach (var (pattern, transformer) in Transformers)
            {
                if (PathMatches(currentPath, pattern))
                {
                    return transformer(value);
                }
            }

            return value;
        }

        /// <summary>
        /// Checks if a dotted path matches a pattern with wildcard <c>*</c> segments.
        /// For example, <c>"services.web.ports"</c> matches <c>"services.*.ports"</c>.
        /// </summary>
        internal static bool PathMatches(string path, string pattern)
        {
            var pathSegments = path.Split('.');
            var patternSegments = pattern.Split('.');

            if (pathSegments.Length != patternSegments.Length)
                return false;

            for (var i = 0; i < pathSegments.Length; i++)
            {
                if (patternSegments[i] == "*")
                    continue;
                if (!string.Equals(pathSegments[i], patternSegments[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        #region Individual Transformers

        /// <summary>
        /// Transforms <c>services.*.build</c>: string -> <c>{"context": string}</c>.
        /// </summary>
        private static object? TransformBuild(object? value)
        {
            if (value is string s)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["context"] = s
                };
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.depends_on</c>:
        /// list -> dict with default condition/required;
        /// dict entries get missing defaults filled.
        /// </summary>
        private static object? TransformDependsOn(object? value)
        {
            if (value is List<object?> list)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var item in list)
                {
                    var name = Convert.ToString(item, CultureInfo.InvariantCulture) ?? "";
                    result[name] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["condition"] = "service_started",
                        ["required"] = true
                    };
                }

                return result;
            }

            if (value is Dictionary<string, object?> dict)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in dict)
                {
                    if (kvp.Value is Dictionary<string, object?> entry)
                    {
                        var newEntry = new Dictionary<string, object?>(entry, StringComparer.Ordinal);
                        if (!newEntry.ContainsKey("condition"))
                            newEntry["condition"] = "service_started";
                        if (!newEntry.ContainsKey("required"))
                            newEntry["required"] = true;
                        result[kvp.Key] = newEntry;
                    }
                    else
                    {
                        result[kvp.Key] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["condition"] = "service_started",
                            ["required"] = true
                        };
                    }
                }

                return result;
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.env_file</c>:
        /// string -> list of objects; list of strings -> list of objects; fills missing <c>required</c>.
        /// </summary>
        private static object? TransformEnvFile(object? value)
        {
            if (value is string s)
            {
                return new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["path"] = s,
                        ["required"] = true
                    }
                };
            }

            if (value is List<object?> list)
            {
                var result = new List<object?>(list.Count);
                foreach (var item in list)
                {
                    if (item is string str)
                    {
                        result.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["path"] = str,
                            ["required"] = true
                        });
                    }
                    else if (item is Dictionary<string, object?> entry)
                    {
                        var newEntry = new Dictionary<string, object?>(entry, StringComparer.Ordinal);
                        if (!newEntry.ContainsKey("required"))
                            newEntry["required"] = true;
                        result.Add(newEntry);
                    }
                    else
                    {
                        result.Add(item);
                    }
                }

                return result;
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.extends</c>: string -> <c>{"service": string}</c>.
        /// </summary>
        private static object? TransformExtends(object? value)
        {
            if (value is string s)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["service"] = s
                };
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.networks</c>:
        /// list <c>["net1", "net2"]</c> -> <c>{"net1": null, "net2": null}</c>.
        /// </summary>
        private static object? TransformNetworks(object? value)
        {
            if (value is List<object?> list)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var item in list)
                {
                    var name = Convert.ToString(item, CultureInfo.InvariantCulture) ?? "";
                    result[name] = null;
                }

                return result;
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.ports</c>:
        /// string/int items are parsed via <see cref="PortParser"/> and converted to dicts.
        /// </summary>
        private static object? TransformPorts(object? value)
        {
            if (value is not List<object?> list)
                return value;

            var result = new List<object?>();
            foreach (var item in list)
            {
                switch (item)
                {
                    case string portStr:
                    {
                        var configs = PortParser.ParsePortConfig(portStr);
                        foreach (var cfg in configs)
                        {
                            result.Add(PortConfigToDict(cfg));
                        }

                        break;
                    }
                    case int intPort:
                    {
                        var configs = PortParser.ParsePortConfig(
                            intPort.ToString(CultureInfo.InvariantCulture));
                        foreach (var cfg in configs)
                        {
                            result.Add(PortConfigToDict(cfg));
                        }

                        break;
                    }
                    case long longPort:
                    {
                        var configs = PortParser.ParsePortConfig(
                            longPort.ToString(CultureInfo.InvariantCulture));
                        foreach (var cfg in configs)
                        {
                            result.Add(PortConfigToDict(cfg));
                        }

                        break;
                    }
                    case Dictionary<string, object?> dict:
                    {
                        // Already long-form, ensure defaults
                        var newDict = new Dictionary<string, object?>(dict, StringComparer.Ordinal);
                        if (!newDict.ContainsKey("protocol"))
                            newDict["protocol"] = "tcp";
                        result.Add(newDict);
                        break;
                    }
                    default:
                        result.Add(item);
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Transforms <c>services.*.volumes.*</c>:
        /// string -> parsed via <see cref="VolumeParser"/> and converted to dict.
        /// </summary>
        private static object? TransformVolumeMount(object? value)
        {
            if (value is string s)
            {
                var cfg = VolumeParser.ParseVolume(s);
                return VolumeConfigToDict(cfg);
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.secrets.*</c>, <c>services.*.configs.*</c>,
        /// and <c>services.*.build.secrets.*</c>: string -> <c>{"source": string}</c>.
        /// </summary>
        private static object? TransformFileMount(object? value)
        {
            if (value is string s)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = s
                };
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.dns</c>: string -> <c>[string]</c>.
        /// </summary>
        private static object? TransformDns(object? value)
        {
            if (value is string s)
            {
                return new List<object?> { s };
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.label_file</c>: string -> <c>[string]</c>.
        /// </summary>
        private static object? TransformLabelFile(object? value)
        {
            if (value is string s)
            {
                return new List<object?> { s };
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.ulimits.*</c>:
        /// int value stays as-is (later binding handles single-value ulimits).
        /// </summary>
        private static object? TransformUlimits(object? value)
        {
            // int/long values pass through unchanged; the binding layer handles them.
            return value;
        }

        /// <summary>
        /// Transforms top-level <c>volumes.*</c>, <c>networks.*</c>, <c>secrets.*</c>, <c>configs.*</c>:
        /// If <c>external</c> is a dict like <c>{"name": "x"}</c>, converts to
        /// <c>external: true</c> + <c>name: "x"</c> at parent level.
        /// </summary>
        private static object? TransformMaybeExternal(object? value)
        {
            if (value is not Dictionary<string, object?> dict)
                return value;

            if (!dict.TryGetValue("external", out var externalValue))
                return value;

            if (externalValue is not Dictionary<string, object?> externalDict)
                return value;

            var result = new Dictionary<string, object?>(dict, StringComparer.Ordinal);
            result["external"] = true;

            if (externalDict.TryGetValue("name", out var nameValue))
            {
                result["name"] = nameValue;
            }

            return result;
        }

        /// <summary>
        /// Transforms <c>include.*</c>: string -> <c>{"path": string}</c>.
        /// </summary>
        private static object? TransformInclude(object? value)
        {
            if (value is string s)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = s
                };
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.devices.*</c>:
        /// string <c>"/dev/sda:/dev/xvdc:rwm"</c> -> <c>{"source": ..., "target": ..., "permissions": ...}</c>.
        /// </summary>
        private static object? TransformDeviceMapping(object? value)
        {
            if (value is not string s)
                return value;

            var parts = s.Split(':');
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);

            switch (parts.Length)
            {
                case 1:
                    // "/dev/sda" -> source=target, default permissions
                    result["source"] = parts[0];
                    result["target"] = parts[0];
                    result["permissions"] = "rwm";
                    break;
                case 2:
                    // "/dev/sda:/dev/xvdc" -> source:target, default permissions
                    result["source"] = parts[0];
                    result["target"] = parts[1];
                    result["permissions"] = "rwm";
                    break;
                case 3:
                    // "/dev/sda:/dev/xvdc:rwm" -> source:target:permissions
                    result["source"] = parts[0];
                    result["target"] = parts[1];
                    result["permissions"] = parts[2];
                    break;
                default:
                    return value;
            }

            return result;
        }

        /// <summary>
        /// Transforms <c>services.*.build.ssh</c>:
        /// list <c>["default", "id=path"]</c> -> <c>{"default": null, "id": "path"}</c>.
        /// </summary>
        private static object? TransformSSH(object? value)
        {
            if (value is not List<object?> list)
                return value;

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                var str = Convert.ToString(item, CultureInfo.InvariantCulture) ?? "";
                var eqIndex = str.IndexOf('=');
                if (eqIndex < 0)
                {
                    result[str] = null;
                }
                else
                {
                    var key = str.Substring(0, eqIndex);
                    var val = str.Substring(eqIndex + 1);
                    result[key] = val;
                }
            }

            return result;
        }

        /// <summary>
        /// Transforms <c>services.*.build.additional_contexts</c>:
        /// list <c>["k=v"]</c> -> <c>{"k": "v"}</c>.
        /// </summary>
        private static object? TransformKeyValue(object? value)
        {
            if (value is not List<object?> list)
                return value;

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                var str = Convert.ToString(item, CultureInfo.InvariantCulture) ?? "";
                var eqIndex = str.IndexOf('=');
                if (eqIndex < 0)
                {
                    result[str] = "";
                }
                else
                {
                    var key = str.Substring(0, eqIndex);
                    var val = str.Substring(eqIndex + 1);
                    result[key] = val;
                }
            }

            return result;
        }

        /// <summary>
        /// Transforms <c>services.*.gpus</c>:
        /// string <c>"all"</c> -> <c>[{"count": "all"}]</c>.
        /// </summary>
        private static object? TransformGpus(object? value)
        {
            if (value is string s)
            {
                return new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["count"] = s
                    }
                };
            }

            return value;
        }

        /// <summary>
        /// Transforms <c>services.*.models</c>:
        /// list <c>["model1", "model2"]</c> -> <c>{"model1": null, "model2": null}</c>.
        /// </summary>
        private static object? TransformModels(object? value)
        {
            if (value is List<object?> list)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var item in list)
                {
                    var name = Convert.ToString(item, CultureInfo.InvariantCulture) ?? "";
                    result[name] = null;
                }

                return result;
            }

            return value;
        }

        /// <summary>
        /// Transforms non-string values to their string representation.
        /// Used for: <c>services.*.build.provenance</c>, <c>services.*.build.sbom</c>.
        /// </summary>
        private static object? TransformStringOrX(object? value)
        {
            if (value is string)
                return value;

            if (value == null)
                return value;

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Transforms a single string to a list containing that string.
        /// Used for: <c>services.*.develop.watch.*.ignore</c>, <c>services.*.develop.watch.*.include</c>.
        /// </summary>
        private static object? TransformStringOrList(object? value)
        {
            if (value is string s)
            {
                return new List<object?> { s };
            }

            return value;
        }

        #endregion

        #region Default Value Transformers

        /// <summary>
        /// Registered path patterns for default-value filling, applied after canonical transforms.
        /// Mirrors compose-go's <c>transform.SetDefaultValues()</c>.
        /// </summary>
        private static readonly (string Pattern, Func<object?, object?> Transformer)[] DefaultTransformers =
        {
            ("services.*.build", DefaultBuildContext),
            ("services.*.secrets.*", DefaultSecretMount),
            ("services.*.ports.*", DefaultPortValues),
            ("services.*.deploy.resources.reservations.devices.*", DefaultDeviceRequest),
            ("services.*.gpus.*", DefaultDeviceRequest),
            ("services.*.volumes.*.bind", DefaultVolumeBind),
        };

        /// <summary>
        /// Fills default values in a Compose config dictionary after canonical transformation.
        /// Mirrors compose-go's <c>transform.SetDefaultValues()</c>.
        /// </summary>
        /// <param name="dict">The raw config dictionary (already canonicalized).</param>
        /// <returns>A new dictionary with default values filled in.</returns>
        public static Dictionary<string, object?> SetDefaultValues(Dictionary<string, object?> dict)
        {
            var result = SetDefaultsRecursive(dict, "") as Dictionary<string, object?>;
            return result ?? new Dictionary<string, object?>();
        }

        /// <summary>
        /// Recursively walks the config structure and applies default-value transformers at matching paths.
        /// </summary>
        private static object? SetDefaultsRecursive(object? value, string currentPath)
        {
            // First, apply any default transformer that matches this path
            value = ApplyDefaultTransformer(value, currentPath);

            switch (value)
            {
                case Dictionary<string, object?> dict:
                {
                    var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var kvp in dict)
                    {
                        var childPath = string.IsNullOrEmpty(currentPath)
                            ? kvp.Key
                            : currentPath + "." + kvp.Key;
                        result[kvp.Key] = SetDefaultsRecursive(kvp.Value, childPath);
                    }

                    return result;
                }
                case List<object?> list:
                {
                    var result = new List<object?>(list.Count);
                    for (var i = 0; i < list.Count; i++)
                    {
                        var childPath = currentPath + ".*";
                        result.Add(SetDefaultsRecursive(list[i], childPath));
                    }

                    return result;
                }
                default:
                    return value;
            }
        }

        /// <summary>
        /// Applies the first matching default transformer for the given path.
        /// </summary>
        private static object? ApplyDefaultTransformer(object? value, string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath))
                return value;

            foreach (var (pattern, transformer) in DefaultTransformers)
            {
                if (PathMatches(currentPath, pattern))
                {
                    return transformer(value);
                }
            }

            return value;
        }

        /// <summary>
        /// If build map is missing "context", sets it to ".".
        /// </summary>
        private static object? DefaultBuildContext(object? value)
        {
            if (value is Dictionary<string, object?> dict)
            {
                if (!dict.ContainsKey("context") || dict["context"] == null)
                {
                    var result = new Dictionary<string, object?>(dict, StringComparer.Ordinal);
                    result["context"] = ".";
                    return result;
                }
            }

            return value;
        }

        /// <summary>
        /// If secret mount map is missing "target", sets it to "/run/secrets/{source}".
        /// </summary>
        private static object? DefaultSecretMount(object? value)
        {
            if (value is Dictionary<string, object?> dict)
            {
                if (!dict.ContainsKey("target") || dict["target"] == null)
                {
                    var result = new Dictionary<string, object?>(dict, StringComparer.Ordinal);
                    var source = result.TryGetValue("source", out var s) && s != null
                        ? s.ToString()
                        : "";
                    result["target"] = "/run/secrets/" + source;
                    return result;
                }
            }

            return value;
        }

        /// <summary>
        /// If port map is missing "protocol", sets it to "tcp".
        /// If port map is missing "mode", sets it to "ingress".
        /// </summary>
        private static object? DefaultPortValues(object? value)
        {
            if (value is Dictionary<string, object?> dict)
            {
                var result = new Dictionary<string, object?>(dict, StringComparer.Ordinal);
                if (!result.ContainsKey("protocol") || result["protocol"] == null
                    || (result["protocol"] is string p && string.IsNullOrEmpty(p)))
                {
                    result["protocol"] = "tcp";
                }
                if (!result.ContainsKey("mode") || result["mode"] == null
                    || (result["mode"] is string m && string.IsNullOrEmpty(m)))
                {
                    result["mode"] = "ingress";
                }
                return result;
            }

            return value;
        }

        /// <summary>
        /// If device request has neither "count" nor "device_ids", sets "count" to "all".
        /// </summary>
        private static object? DefaultDeviceRequest(object? value)
        {
            if (value is Dictionary<string, object?> dict)
            {
                var hasCount = dict.ContainsKey("count") && dict["count"] != null;
                var hasDeviceIds = dict.ContainsKey("device_ids") && dict["device_ids"] != null;

                if (!hasCount && !hasDeviceIds)
                {
                    var result = new Dictionary<string, object?>(dict, StringComparer.Ordinal);
                    result["count"] = "all";
                    return result;
                }
            }

            return value;
        }

        /// <summary>
        /// If bind options don't have "create_host_path", sets it to true.
        /// </summary>
        private static object? DefaultVolumeBind(object? value)
        {
            if (value is Dictionary<string, object?> dict)
            {
                if (!dict.ContainsKey("create_host_path"))
                {
                    var result = new Dictionary<string, object?>(dict, StringComparer.Ordinal);
                    result["create_host_path"] = true;
                    return result;
                }
            }

            return value;
        }

        #endregion

        #region Type-to-Dict Converters

        /// <summary>
        /// Converts a <see cref="ServicePortConfig"/> to its canonical dictionary representation.
        /// </summary>
        internal static Dictionary<string, object?> PortConfigToDict(ServicePortConfig cfg)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["target"] = (int)cfg.Target,
                ["published"] = cfg.Published ?? "",
                ["host_ip"] = cfg.HostIP ?? "",
                ["protocol"] = cfg.Protocol
            };

            if (!string.IsNullOrEmpty(cfg.AppProtocol))
            {
                dict["app_protocol"] = cfg.AppProtocol;
            }

            return dict;
        }

        /// <summary>
        /// Converts a <see cref="ServiceVolumeConfig"/> to its canonical dictionary representation.
        /// Only includes non-null sub-objects (bind, volume, tmpfs).
        /// </summary>
        internal static Dictionary<string, object?> VolumeConfigToDict(ServiceVolumeConfig cfg)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = cfg.Type,
                ["source"] = cfg.Source ?? "",
                ["target"] = cfg.Target,
                ["read_only"] = cfg.ReadOnly
            };

            if (cfg.Bind != null)
            {
                var bindDict = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["create_host_path"] = cfg.Bind.CreateHostPath
                };

                if (!string.IsNullOrEmpty(cfg.Bind.SELinux))
                    bindDict["selinux"] = cfg.Bind.SELinux;
                if (!string.IsNullOrEmpty(cfg.Bind.Propagation))
                    bindDict["propagation"] = cfg.Bind.Propagation;
                if (!string.IsNullOrEmpty(cfg.Bind.Recursive))
                    bindDict["recursive"] = cfg.Bind.Recursive;

                dict["bind"] = bindDict;
            }

            if (cfg.Volume != null)
            {
                var volumeDict = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["nocopy"] = cfg.Volume.NoCopy
                };

                if (!string.IsNullOrEmpty(cfg.Volume.Subpath))
                    volumeDict["subpath"] = cfg.Volume.Subpath;

                dict["volume"] = volumeDict;
            }

            if (cfg.Tmpfs != null)
            {
                var tmpfsDict = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["size"] = cfg.Tmpfs.Size.Bytes,
                    ["mode"] = cfg.Tmpfs.Mode
                };

                dict["tmpfs"] = tmpfsDict;
            }

            return dict;
        }

        #endregion
    }
}
