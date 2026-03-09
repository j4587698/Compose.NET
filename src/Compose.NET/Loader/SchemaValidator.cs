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
using System.Net;

namespace Compose.NET.Loader
{
    /// <summary>
    /// Validates the structure and contents of a parsed Compose YAML dictionary
    /// against the Compose specification schema. Mirrors compose-go's schema validation.
    /// </summary>
    internal static class SchemaValidator
    {
        /// <summary>
        /// Known top-level keys in a Compose file.
        /// Extension keys (x-*) are always allowed at any level.
        /// </summary>
        private static readonly HashSet<string> ValidTopLevelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "name", "version", "services", "networks", "volumes", "secrets", "configs", "include", "models"
        };

        /// <summary>
        /// Known service-level keys. This covers the major fields from the Compose spec.
        /// </summary>
        private static readonly HashSet<string> ValidServiceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core
            "image", "build", "command", "entrypoint", "container_name",
            "hostname", "domainname", "user", "working_dir", "mac_address",
            "platform", "pull_policy", "profiles", "runtime",

            // Lifecycle
            "restart", "init", "stop_signal", "stop_grace_period",
            "depends_on", "extends", "links", "external_links",

            // Networking
            "ports", "expose", "networks", "network_mode",
            "dns", "dns_search", "dns_opt", "extra_hosts",

            // Volumes & storage
            "volumes", "volumes_from", "tmpfs",

            // Environment
            "environment", "env_file",

            // Logging & monitoring
            "logging", "healthcheck",

            // Resources
            "deploy", "mem_limit", "mem_reservation", "mem_swappiness",
            "memswap_limit", "oom_kill_disable", "oom_score_adj",
            "cpu_count", "cpu_percent", "cpu_shares", "cpu_period", "cpu_quota",
            "cpu_rt_runtime", "cpu_rt_period", "cpus", "cpuset",
            "pids_limit", "blkio_config", "device_cgroup_rules", "devices",
            "storage_opt", "shm_size",

            // Security
            "cap_add", "cap_drop", "cgroup", "cgroup_parent",
            "security_opt", "privileged", "read_only", "userns_mode",
            "credential_spec", "secrets", "configs",

            // Process
            "pid", "ipc", "uts",
            "stdin_open", "tty", "isolation",
            "sysctls", "ulimits",

            // Labels & annotations
            "labels", "annotations",

            // Other
            "scale", "attach", "develop",
            "post_start", "pre_stop",
            "provider", "models", "gpus",
            "label_file", "dockerfile",

            // Volume drivers (legacy)
            "volume_driver",

            // Custom extensions handled separately (x-*)
        };

        /// <summary>
        /// Validates the top-level structure of a Compose config dictionary.
        /// Returns a list of validation errors (empty if valid).
        /// </summary>
        public static List<string> ValidateSchema(Dictionary<string, object?> config)
        {
            var errors = new List<string>();

            // Validate top-level keys
            foreach (var key in config.Keys)
            {
                if (IsExtensionKey(key)) continue;
                if (!ValidTopLevelKeys.Contains(key))
                {
                    errors.Add($"Unknown top-level key: '{key}'.");
                }
            }

            // Validate services section
            if (config.TryGetValue("services", out var servicesObj))
            {
                if (servicesObj is Dictionary<string, object?> services)
                {
                    foreach (var kvp in services)
                    {
                        if (IsExtensionKey(kvp.Key)) continue;
                        ValidateService(kvp.Key, kvp.Value, errors);
                    }
                }
                else if (servicesObj != null)
                {
                    errors.Add("'services' must be a mapping.");
                }
            }

            // Validate networks section
            if (config.TryGetValue("networks", out var networksObj) && networksObj != null)
            {
                if (networksObj is not Dictionary<string, object?>)
                {
                    errors.Add("'networks' must be a mapping.");
                }
            }

            // Validate volumes section
            if (config.TryGetValue("volumes", out var volumesObj) && volumesObj != null)
            {
                if (volumesObj is not Dictionary<string, object?>)
                {
                    errors.Add("'volumes' must be a mapping.");
                }
            }

            // Validate secrets section
            if (config.TryGetValue("secrets", out var secretsObj) && secretsObj != null)
            {
                if (secretsObj is not Dictionary<string, object?>)
                {
                    errors.Add("'secrets' must be a mapping.");
                }
            }

            // Validate configs section
            if (config.TryGetValue("configs", out var configsObj) && configsObj != null)
            {
                if (configsObj is not Dictionary<string, object?>)
                {
                    errors.Add("'configs' must be a mapping.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates a single service definition.
        /// </summary>
        private static void ValidateService(string serviceName, object? serviceObj, List<string> errors)
        {
            if (serviceObj is not Dictionary<string, object?> svc)
            {
                if (serviceObj != null)
                    errors.Add($"Service '{serviceName}' must be a mapping.");
                return;
            }

            // Check for unknown keys
            foreach (var key in svc.Keys)
            {
                if (IsExtensionKey(key)) continue;
                if (!ValidServiceKeys.Contains(key))
                {
                    errors.Add($"Service '{serviceName}': unknown key '{key}'.");
                }
            }
        }

        /// <summary>
        /// Performs post-binding validation on the merged config.
        /// Checks for basic logical consistency after defaults have been set.
        /// Mirrors compose-go's pre-struct validation in <c>validation.Validate()</c>.
        /// </summary>
        public static List<string> ValidateLogic(Dictionary<string, object?> config)
        {
            var errors = new List<string>();

            // â”€â”€ Top-level resource validations â”€â”€

            ValidateExternalResource(config, "volumes", errors);
            ValidateExternalResource(config, "networks", errors);
            ValidateExternalResource(config, "configs", errors);
            ValidateExternalResource(config, "secrets", errors);

            ValidateConfigsMutualExclusivity(config, errors);
            ValidateSecretsMutualExclusivity(config, errors);

            // â”€â”€ Service-level validations â”€â”€

            if (!config.TryGetValue("services", out var servicesObj) ||
                servicesObj is not Dictionary<string, object?> services)
                return errors;

            foreach (var kvp in services)
            {
                if (IsExtensionKey(kvp.Key)) continue;
                if (kvp.Value is not Dictionary<string, object?> svc) continue;

                var name = kvp.Key;

                // Validate ports (target required + host_ip IP validation)
                ValidateServicePorts(name, svc, errors);

                // Validate device_request count/device_ids mutual exclusivity
                ValidateDeviceRequests(name, svc, errors);

                // Validate develop.watch path non-empty
                ValidateWatchPaths(name, svc, errors);
            }

            return errors;
        }

        /// <summary>
        /// Validates port entries: target is required, and host_ip must be a valid IP address.
        /// Mirrors compose-go's port <c>host_ip</c> IP validation.
        /// </summary>
        private static void ValidateServicePorts(string serviceName, Dictionary<string, object?> svc, List<string> errors)
        {
            if (!svc.TryGetValue("ports", out var portsObj) || portsObj is not List<object?> ports)
                return;

            foreach (var port in ports)
            {
                if (port is not Dictionary<string, object?> portDict)
                    continue;

                if (!portDict.ContainsKey("target") || portDict["target"] == null)
                {
                    errors.Add($"Service '{serviceName}': port entry missing 'target'.");
                }

                // Validate host_ip is a valid IP address
                if (portDict.TryGetValue("host_ip", out var hostIpVal) && hostIpVal != null)
                {
                    var hostIp = hostIpVal.ToString()!;
                    if (!string.IsNullOrEmpty(hostIp) && !IPAddress.TryParse(hostIp, out _))
                    {
                        errors.Add($"Service '{serviceName}': port host_ip '{hostIp}' is not a valid IP address.");
                    }
                }
            }
        }

        /// <summary>
        /// Validates that external resources only have allowed keys (name, external, x-*).
        /// Mirrors compose-go's external resource conflict check.
        /// </summary>
        private static void ValidateExternalResource(Dictionary<string, object?> config, string section, List<string> errors)
        {
            if (!config.TryGetValue(section, out var sectionObj) ||
                sectionObj is not Dictionary<string, object?> resources)
                return;

            // Keys that are allowed when external: true
            var allowedExternalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name", "external"
            };

            foreach (var kvp in resources)
            {
                if (kvp.Value is not Dictionary<string, object?> resource)
                    continue;

                if (!resource.TryGetValue("external", out var extVal))
                    continue;

                var isExternal = extVal is true || (extVal is string s && s.Equals("true", StringComparison.OrdinalIgnoreCase));
                if (!isExternal) continue;

                foreach (var key in resource.Keys)
                {
                    if (IsExtensionKey(key)) continue;
                    if (allowedExternalKeys.Contains(key)) continue;

                    errors.Add($"{section}.{kvp.Key}: '{key}' is not allowed for external {section.TrimEnd('s')}. " +
                               $"Only 'name' and extension fields are allowed when 'external: true'.");
                }
            }
        }

        /// <summary>
        /// Validates that top-level configs have mutually exclusive file/environment/content.
        /// Mirrors compose-go's configs mutual exclusivity check.
        /// </summary>
        private static void ValidateConfigsMutualExclusivity(Dictionary<string, object?> config, List<string> errors)
        {
            if (!config.TryGetValue("configs", out var configsObj) ||
                configsObj is not Dictionary<string, object?> configs)
                return;

            foreach (var kvp in configs)
            {
                if (kvp.Value is not Dictionary<string, object?> cfg)
                    continue;

                var sources = new List<string>();
                if (cfg.TryGetValue("file", out var fileVal) && fileVal != null && !string.IsNullOrEmpty(fileVal.ToString()))
                    sources.Add("file");
                if (cfg.TryGetValue("environment", out var envVal) && envVal != null && !string.IsNullOrEmpty(envVal.ToString()))
                    sources.Add("environment");
                if (cfg.TryGetValue("content", out var contentVal) && contentVal != null && !string.IsNullOrEmpty(contentVal.ToString()))
                    sources.Add("content");

                if (sources.Count > 1)
                {
                    errors.Add($"configs.{kvp.Key}: {string.Join(", ", sources)} are mutually exclusive.");
                }
            }
        }

        /// <summary>
        /// Validates that top-level secrets have mutually exclusive file/environment/content.
        /// Mirrors compose-go's secrets mutual exclusivity check.
        /// </summary>
        private static void ValidateSecretsMutualExclusivity(Dictionary<string, object?> config, List<string> errors)
        {
            if (!config.TryGetValue("secrets", out var secretsObj) ||
                secretsObj is not Dictionary<string, object?> secrets)
                return;

            foreach (var kvp in secrets)
            {
                if (kvp.Value is not Dictionary<string, object?> secret)
                    continue;

                var sources = new List<string>();
                if (secret.TryGetValue("file", out var fileVal) && fileVal != null && !string.IsNullOrEmpty(fileVal.ToString()))
                    sources.Add("file");
                if (secret.TryGetValue("environment", out var envVal) && envVal != null && !string.IsNullOrEmpty(envVal.ToString()))
                    sources.Add("environment");
                if (secret.TryGetValue("content", out var contentVal) && contentVal != null && !string.IsNullOrEmpty(contentVal.ToString()))
                    sources.Add("content");

                if (sources.Count > 1)
                {
                    errors.Add($"secrets.{kvp.Key}: {string.Join(", ", sources)} are mutually exclusive.");
                }
            }
        }

        /// <summary>
        /// Validates that device requests do not have both count and device_ids set.
        /// Mirrors compose-go's device_request count/device_ids mutual exclusivity.
        /// </summary>
        private static void ValidateDeviceRequests(string serviceName, Dictionary<string, object?> svc, List<string> errors)
        {
            // Check deploy.resources.reservations.devices
            if (svc.TryGetValue("deploy", out var deployObj) && deployObj is Dictionary<string, object?> deploy &&
                deploy.TryGetValue("resources", out var resObj) && resObj is Dictionary<string, object?> resources &&
                resources.TryGetValue("reservations", out var reservObj) && reservObj is Dictionary<string, object?> reservations &&
                reservations.TryGetValue("devices", out var devicesObj) && devicesObj is List<object?> devices)
            {
                ValidateDeviceList(serviceName, "deploy.resources.reservations.devices", devices, errors);
            }

            // Check top-level gpus field
            if (svc.TryGetValue("gpus", out var gpusObj) && gpusObj is List<object?> gpus)
            {
                ValidateDeviceList(serviceName, "gpus", gpus, errors);
            }
        }

        /// <summary>
        /// Validates a list of device request entries for count/device_ids mutual exclusivity.
        /// </summary>
        private static void ValidateDeviceList(string serviceName, string path, List<object?> devices, List<string> errors)
        {
            for (var i = 0; i < devices.Count; i++)
            {
                if (devices[i] is not Dictionary<string, object?> device)
                    continue;

                var hasCount = device.TryGetValue("count", out var countVal) && countVal != null;
                var hasDeviceIds = device.TryGetValue("device_ids", out var idsVal) &&
                                   idsVal is List<object?> idsList && idsList.Count > 0;

                if (hasCount && hasDeviceIds)
                {
                    errors.Add($"Service '{serviceName}': {path}[{i}]: 'count' and 'device_ids' are mutually exclusive.");
                }
            }
        }

        /// <summary>
        /// Validates that develop.watch entries have non-empty path fields.
        /// Mirrors compose-go's watch path non-empty validation.
        /// </summary>
        private static void ValidateWatchPaths(string serviceName, Dictionary<string, object?> svc, List<string> errors)
        {
            if (!svc.TryGetValue("develop", out var developObj) ||
                developObj is not Dictionary<string, object?> develop)
                return;

            if (!develop.TryGetValue("watch", out var watchObj) ||
                watchObj is not List<object?> watchList)
                return;

            for (var i = 0; i < watchList.Count; i++)
            {
                if (watchList[i] is not Dictionary<string, object?> trigger)
                    continue;

                var hasPath = trigger.TryGetValue("path", out var pathVal) && pathVal != null &&
                              !string.IsNullOrWhiteSpace(pathVal.ToString());

                if (!hasPath)
                {
                    errors.Add($"Service '{serviceName}': develop.watch[{i}].path must not be empty.");
                }
            }
        }

        /// <summary>
        /// Returns true if the key is a Compose extension key (starts with "x-").
        /// </summary>
        private static bool IsExtensionKey(string key)
        {
            return key.StartsWith("x-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
