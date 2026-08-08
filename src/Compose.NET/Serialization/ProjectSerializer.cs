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
using System.Text;
using Compose.NET.Types;

namespace Compose.NET.Serialization
{
    /// <summary>
    /// Serializes a <see cref="Project"/> to YAML or JSON format.
    /// Converts the strongly-typed model back into a dictionary tree,
    /// then renders it via YamlDotNet or System.Text.Json.
    /// </summary>
    public static class ProjectSerializer
    {
        #region Public API

        /// <summary>
        /// Serializes a <see cref="Project"/> to a Docker Compose YAML string.
        /// </summary>
        /// <param name="project">The project to serialize.</param>
        /// <returns>A YAML string representing the project.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="project"/> is <c>null</c>.</exception>
        public static string ToYaml(Project project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var dict = ProjectToDict(project);
            return DictTreeSerializer.ToYaml(dict);
        }

        /// <summary>
        /// Serializes a <see cref="Project"/> to a JSON string.
        /// </summary>
        /// <param name="project">The project to serialize.</param>
        /// <param name="indented">Whether to format the output with indentation. Defaults to <c>true</c>.</param>
        /// <returns>A JSON string representing the project.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="project"/> is <c>null</c>.</exception>
        public static string ToJson(Project project, bool indented = true)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var dict = ProjectToDict(project);
            return DictTreeSerializer.ToJson(dict, indented);
        }

        #endregion

        #region ProjectToDict

        /// <summary>
        /// Converts a <see cref="Project"/> to a <see cref="Dictionary{String, Object}"/>
        /// tree suitable for YAML/JSON serialization.
        /// </summary>
        internal static Dictionary<string, object?> ProjectToDict(Project project)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            // name
            if (!string.IsNullOrEmpty(project.Name))
                dict["name"] = project.Name;

            // services
            if (project.Services.Count > 0)
            {
                var services = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in project.Services.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    services[kvp.Key] = ServiceToDict(kvp.Value);
                }
                dict["services"] = services;
            }

            // networks
            if (project.Networks.Count > 0)
            {
                var networks = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in project.Networks.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    networks[kvp.Key] = NetworkConfigToDict(kvp.Value);
                }
                dict["networks"] = networks;
            }

            // volumes
            if (project.Volumes.Count > 0)
            {
                var volumes = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in project.Volumes.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    volumes[kvp.Key] = VolumeConfigToDict(kvp.Value);
                }
                dict["volumes"] = volumes;
            }

            // secrets
            if (project.Secrets.Count > 0)
            {
                var secrets = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in project.Secrets.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    secrets[kvp.Key] = FileObjectConfigToDict(kvp.Value);
                }
                dict["secrets"] = secrets;
            }

            // configs
            if (project.Configs.Count > 0)
            {
                var configs = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in project.Configs.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    configs[kvp.Key] = FileObjectConfigToDict(kvp.Value);
                }
                dict["configs"] = configs;
            }

            // models
            if (project.Models.Count > 0)
            {
                var models = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in project.Models.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    models[kvp.Key] = ModelConfigToDict(kvp.Value);
                }
                dict["models"] = models;
            }

            // extensions (x-* keys flattened into root)
            FlattenExtensions(dict, project.Extensions);

            return dict;
        }

        #endregion

        #region ServiceConfig

        private static Dictionary<string, object?> ServiceToDict(ServiceConfig svc)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            // Identity â€” skip Name since it's the dict key
            EmitList(dict, "profiles", svc.Profiles);

            // Metadata
            EmitMapping(dict, "annotations", svc.Annotations);
            EmitNullableBool(dict, "attach", svc.Attach);

            // Build
            if (svc.Build != null)
                dict["build"] = BuildConfigToDict(svc.Build);

            // BlkIO
            if (svc.BlkioConfig != null)
                dict["blkio_config"] = BlkioConfigToDict(svc.BlkioConfig);

            // Capabilities
            EmitList(dict, "cap_add", svc.CapAdd);
            EmitList(dict, "cap_drop", svc.CapDrop);

            // Cgroup
            EmitString(dict, "cgroup_parent", svc.CgroupParent);
            EmitString(dict, "cgroup", svc.Cgroup);

            // CPU resources
            EmitNonZeroLong(dict, "cpu_count", svc.CPUCount);
            EmitNonZeroFloat(dict, "cpu_percent", svc.CPUPercent);
            EmitNonZeroLong(dict, "cpu_period", svc.CPUPeriod);
            EmitNonZeroLong(dict, "cpu_quota", svc.CPUQuota);
            EmitNonZeroLong(dict, "cpu_rt_period", svc.CPURTPeriod);
            EmitNonZeroLong(dict, "cpu_rt_runtime", svc.CPURTRuntime);
            EmitNonZeroFloat(dict, "cpus", svc.CPUS.Value);
            EmitString(dict, "cpuset", svc.CPUSet);
            EmitNonZeroLong(dict, "cpu_shares", svc.CPUShares);

            // Command
            EmitShellCommand(dict, "command", svc.Command);

            // Configs
            if (svc.Configs.Count > 0)
                dict["configs"] = svc.Configs.Select(ServiceConfigObjToDict).ToList();

            // Container
            EmitString(dict, "container_name", svc.ContainerName);
            if (svc.CredentialSpec != null)
                dict["credential_spec"] = CredentialSpecToDict(svc.CredentialSpec);

            // Dependencies
            if (svc.DependsOn.Count > 0)
                dict["depends_on"] = DependsOnToDict(svc.DependsOn);

            // Deploy
            if (svc.Deploy != null)
                dict["deploy"] = DeployConfigToDict(svc.Deploy);

            // Devices
            EmitList(dict, "device_cgroup_rules", svc.DeviceCgroupRules);
            if (svc.Devices.Count > 0)
                dict["devices"] = svc.Devices.Select(DeviceMappingToDict).ToList();

            // DNS
            EmitStringList(dict, "dns", svc.DNS);
            EmitList(dict, "dns_opt", svc.DNSOpts);
            EmitStringList(dict, "dns_search", svc.DNSSearch);

            // Dockerfile (legacy)
            EmitString(dict, "dockerfile", svc.Dockerfile);
            EmitString(dict, "domainname", svc.DomainName);

            // Entrypoint
            EmitShellCommand(dict, "entrypoint", svc.Entrypoint);

            // Environment
            EmitMappingWithEquals(dict, "environment", svc.Environment);
            if (svc.EnvFiles.Count > 0)
                dict["env_file"] = svc.EnvFiles.Select(EnvFileToDict).ToList();

            // Expose
            EmitStringOrNumberList(dict, "expose", svc.Expose);

            // Extends
            if (svc.Extends != null)
                dict["extends"] = ExtendsConfigToDict(svc.Extends);

            // External links
            EmitList(dict, "external_links", svc.ExternalLinks);
            EmitHostsList(dict, "extra_hosts", svc.ExtraHosts);

            // Groups
            EmitList(dict, "group_add", svc.GroupAdd);

            // GPUs
            if (svc.Gpus.Count > 0)
                dict["gpus"] = svc.Gpus.Select(DeviceRequestToDict).ToList();

            // Host
            EmitString(dict, "hostname", svc.Hostname);
            if (svc.HealthCheck != null)
                dict["healthcheck"] = HealthCheckToDict(svc.HealthCheck);

            // Image
            EmitString(dict, "image", svc.Image);
            EmitNullableBool(dict, "init", svc.Init);

            // IPC / Isolation
            EmitString(dict, "ipc", svc.Ipc);
            EmitString(dict, "isolation", svc.Isolation);

            // Labels
            EmitLabels(dict, "labels", svc.Labels);
            EmitList(dict, "label_file", svc.LabelFiles);

            // Links
            EmitList(dict, "links", svc.Links);

            // Logging
            if (svc.Logging != null)
                dict["logging"] = LoggingConfigToDict(svc.Logging);

            // Memory resources
            EmitUnitBytes(dict, "mem_limit", svc.MemLimit);
            EmitUnitBytes(dict, "mem_reservation", svc.MemReservation);
            EmitUnitBytes(dict, "memswap_limit", svc.MemSwapLimit);
            EmitNullableInt(dict, "mem_swappiness", svc.MemSwappiness);

            // MAC
            EmitString(dict, "mac_address", svc.MacAddress);

            // Network
            EmitString(dict, "network_mode", svc.NetworkMode);
            if (svc.Networks.Count > 0)
            {
                var networks = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in svc.Networks.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    networks[kvp.Key] = kvp.Value != null
                        ? ServiceNetworkConfigToDict(kvp.Value)
                        : null;
                }
                dict["networks"] = networks;
            }

            // OOM
            EmitNonZeroBool(dict, "oom_kill_disable", svc.OomKillDisable);
            EmitNonZeroLong(dict, "oom_score_adj", svc.OomScoreAdj);

            // PID
            EmitString(dict, "pid", svc.Pid);
            EmitNonZeroLong(dict, "pids_limit", svc.PidsLimit);

            // Platform
            EmitString(dict, "platform", svc.Platform);

            // Ports
            if (svc.Ports.Count > 0)
                dict["ports"] = svc.Ports.Select(PortConfigToDict).ToList();

            // Privileged / Pull / ReadOnly
            EmitNonZeroBool(dict, "privileged", svc.Privileged);
            EmitString(dict, "pull_policy", svc.PullPolicy);
            EmitNonZeroBool(dict, "read_only", svc.ReadOnly);

            // Restart
            EmitString(dict, "restart", svc.Restart);
            EmitString(dict, "runtime", svc.Runtime);

            // Scale
            if (svc.Scale.HasValue)
                dict["scale"] = svc.Scale.Value;

            // Secrets
            if (svc.Secrets.Count > 0)
                dict["secrets"] = svc.Secrets.Select(ServiceSecretToDict).ToList();
            EmitList(dict, "security_opt", svc.SecurityOpt);

            // SHM
            EmitUnitBytes(dict, "shm_size", svc.ShmSize);

            // Stdio
            EmitNonZeroBool(dict, "stdin_open", svc.StdinOpen);
            EmitDuration(dict, "stop_grace_period", svc.StopGracePeriod);
            EmitString(dict, "stop_signal", svc.StopSignal);

            // Storage
            EmitStringDict(dict, "storage_opt", svc.StorageOpt);

            // Sysctls
            EmitMapping(dict, "sysctls", svc.Sysctls);

            // Tmpfs
            EmitStringList(dict, "tmpfs", svc.Tmpfs);
            EmitNonZeroBool(dict, "tty", svc.Tty);

            // Ulimits
            if (svc.Ulimits.Count > 0)
                dict["ulimits"] = UlimitsToDict(svc.Ulimits);

            // User
            EmitString(dict, "user", svc.User);
            EmitString(dict, "userns_mode", svc.UserNSMode);
            EmitString(dict, "uts", svc.Uts);

            // Volumes
            EmitString(dict, "volume_driver", svc.VolumeDriver);
            if (svc.Volumes.Count > 0)
                dict["volumes"] = svc.Volumes.Select(VolumeToDict).ToList();
            EmitList(dict, "volumes_from", svc.VolumesFrom);

            // Working directory
            EmitString(dict, "working_dir", svc.WorkingDir);

            // Develop
            if (svc.Develop != null)
                dict["develop"] = DevelopConfigToDict(svc.Develop);

            // Provider
            if (svc.Provider != null)
                dict["provider"] = ProviderConfigToDict(svc.Provider);

            // Models
            if (svc.ServiceModels.Count > 0)
            {
                var models = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in svc.ServiceModels.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    models[kvp.Key] = kvp.Value != null
                        ? ServiceModelConfigToDict(kvp.Value)
                        : null;
                }
                dict["models"] = models;
            }

            // Lifecycle hooks
            if (svc.PostStart.Count > 0)
                dict["post_start"] = svc.PostStart.Select(h => (object?)ServiceHookToDict(h)).ToList();
            if (svc.PreStop.Count > 0)
                dict["pre_stop"] = svc.PreStop.Select(h => (object?)ServiceHookToDict(h)).ToList();

            // Extensions
            FlattenExtensions(dict, svc.Extensions);

            return dict;
        }

        #endregion

        #region BuildConfig

        private static Dictionary<string, object?> BuildConfigToDict(BuildConfig build)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "context", build.Context);
            EmitString(dict, "dockerfile", build.Dockerfile);
            EmitString(dict, "dockerfile_inline", build.DockerfileInline);
            EmitNullableStringDict(dict, "args", build.Args);
            EmitSSHConfig(dict, "ssh", build.SSH);
            EmitList(dict, "cache_from", build.CacheFrom);
            EmitList(dict, "cache_to", build.CacheTo);
            EmitMapping(dict, "additional_contexts", build.AdditionalContexts);
            EmitLabels(dict, "labels", build.Labels);
            EmitNonZeroBool(dict, "no_cache", build.NoCache);
            EmitNonZeroBool(dict, "pull", build.Pull);
            EmitString(dict, "target", build.Target);
            if (build.ShmSize.HasValue && build.ShmSize.Value.Bytes != 0)
                dict["shm_size"] = build.ShmSize.Value.ToString();
            EmitHostsList(dict, "extra_hosts", build.ExtraHosts);
            EmitString(dict, "isolation", build.Isolation);
            EmitNonZeroBool(dict, "privileged", build.Privileged);
            EmitList(dict, "tags", build.Tags);
            EmitList(dict, "platforms", build.Platforms);
            EmitString(dict, "network", build.Network);
            if (build.Secrets.Count > 0)
                dict["secrets"] = build.Secrets.Select(ServiceSecretToDict).ToList();
            EmitList(dict, "entitlements", build.Entitlements);
            EmitString(dict, "provenance", build.Provenance);
            EmitString(dict, "sbom", build.SBOM);
            EmitList(dict, "no_cache_filter", build.NoCacheFilter);
            if (build.Ulimits.Count > 0)
                dict["ulimits"] = UlimitsToDict(build.Ulimits);

            FlattenExtensions(dict, build.Extensions);
            return dict;
        }

        #endregion

        #region BlkioConfig

        private static Dictionary<string, object?> BlkioConfigToDict(BlkioConfig blkio)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (blkio.Weight > 0)
                dict["weight"] = (int)blkio.Weight;
            if (blkio.WeightDevice.Count > 0)
                dict["weight_device"] = blkio.WeightDevice.Select(wd =>
                {
                    var d = new Dictionary<string, object?>(StringComparer.Ordinal);
                    d["path"] = wd.Path;
                    if (wd.Weight > 0) d["weight"] = (int)wd.Weight;
                    FlattenExtensions(d, wd.Extensions);
                    return d;
                }).ToList();
            EmitThrottleDeviceList(dict, "device_read_bps", blkio.DeviceReadBps);
            EmitThrottleDeviceList(dict, "device_read_iops", blkio.DeviceReadIOps);
            EmitThrottleDeviceList(dict, "device_write_bps", blkio.DeviceWriteBps);
            EmitThrottleDeviceList(dict, "device_write_iops", blkio.DeviceWriteIOps);

            FlattenExtensions(dict, blkio.Extensions);
            return dict;
        }

        private static void EmitThrottleDeviceList(Dictionary<string, object?> dict, string key, List<ThrottleDevice> devices)
        {
            if (devices.Count == 0) return;
            dict[key] = devices.Select(td =>
            {
                var d = new Dictionary<string, object?>(StringComparer.Ordinal);
                d["path"] = td.Path;
                d["rate"] = td.Rate.Bytes;
                FlattenExtensions(d, td.Extensions);
                return d;
            }).ToList();
        }

        #endregion

        #region NetworkConfig (top-level)

        private static Dictionary<string, object?> NetworkConfigToDict(NetworkConfig net)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "name", net.Name);
            EmitString(dict, "driver", net.Driver);
            EmitStringDict(dict, "driver_opts", net.DriverOpts);

            if (net.Ipam.Driver != null || net.Ipam.Config.Count > 0 || net.Ipam.Extensions.Count > 0)
                dict["ipam"] = IpamConfigToDict(net.Ipam);

            EmitNonZeroBool(dict, "external", net.External);
            EmitNonZeroBool(dict, "internal", net.Internal);
            EmitNonZeroBool(dict, "attachable", net.Attachable);
            EmitLabels(dict, "labels", net.Labels);
            EmitNullableBool(dict, "enable_ipv4", net.EnableIPv4);
            EmitNullableBool(dict, "enable_ipv6", net.EnableIPv6);

            FlattenExtensions(dict, net.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> IpamConfigToDict(IPAMConfig ipam)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "driver", ipam.Driver);
            if (ipam.Config.Count > 0)
            {
                dict["config"] = ipam.Config.Select(pool =>
                {
                    var d = new Dictionary<string, object?>(StringComparer.Ordinal);
                    EmitString(d, "subnet", pool.Subnet);
                    EmitString(d, "gateway", pool.Gateway);
                    EmitString(d, "ip_range", pool.IPRange);
                    EmitMapping(d, "auxiliary_addresses", pool.AuxiliaryAddresses);
                    FlattenExtensions(d, pool.Extensions);
                    return d;
                }).ToList();
            }

            FlattenExtensions(dict, ipam.Extensions);
            return dict;
        }

        #endregion

        #region VolumeConfig (top-level)

        private static Dictionary<string, object?> VolumeConfigToDict(VolumeConfig vol)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "name", vol.Name);
            EmitString(dict, "driver", vol.Driver);
            EmitStringDict(dict, "driver_opts", vol.DriverOpts);
            EmitNonZeroBool(dict, "external", vol.External);
            EmitLabels(dict, "labels", vol.Labels);

            FlattenExtensions(dict, vol.Extensions);
            return dict;
        }

        #endregion

        #region FileObjectConfig (secrets/configs top-level)

        private static Dictionary<string, object?> FileObjectConfigToDict(FileObjectConfig foc)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "name", foc.Name);
            EmitString(dict, "file", foc.File);
            EmitString(dict, "environment", foc.Environment);
            EmitString(dict, "content", foc.Content);
            EmitNonZeroBool(dict, "external", foc.External);
            EmitLabels(dict, "labels", foc.Labels);
            EmitString(dict, "driver", foc.Driver);
            EmitStringDict(dict, "driver_opts", foc.DriverOpts);
            EmitString(dict, "template_driver", foc.TemplateDriver);

            FlattenExtensions(dict, foc.Extensions);
            return dict;
        }

        #endregion

        #region ModelConfig (top-level)

        private static Dictionary<string, object?> ModelConfigToDict(ModelConfig model)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "name", model.Name);
            EmitString(dict, "model", model.Model);
            if (model.ContextSize != 0)
                dict["context_size"] = model.ContextSize;
            EmitList(dict, "runtime_flags", model.RuntimeFlags);

            FlattenExtensions(dict, model.Extensions);
            return dict;
        }

        #endregion

        #region Service sub-types

        private static Dictionary<string, object?> ServiceNetworkConfigToDict(ServiceNetworkConfig net)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitList(dict, "aliases", net.Aliases);
            EmitStringDict(dict, "driver_opts", net.DriverOpts);
            if (net.GatewayPriority != 0)
                dict["gateway_priority"] = net.GatewayPriority;
            EmitString(dict, "interface_name", net.InterfaceName);
            EmitString(dict, "ipv4_address", net.Ipv4Address);
            EmitString(dict, "ipv6_address", net.Ipv6Address);
            EmitList(dict, "link_local_ips", net.LinkLocalIPs);
            EmitString(dict, "mac_address", net.MacAddress);
            if (net.Priority != 0)
                dict["priority"] = net.Priority;

            FlattenExtensions(dict, net.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> PortConfigToDict(ServicePortConfig port)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "name", port.Name);
            if (port.Target != 0)
                dict["target"] = (int)port.Target;
            EmitString(dict, "published", port.Published);
            EmitString(dict, "host_ip", port.HostIP);
            if (!string.IsNullOrEmpty(port.Protocol) && port.Protocol != "tcp")
                dict["protocol"] = port.Protocol;
            if (!string.IsNullOrEmpty(port.Mode) && port.Mode != "ingress")
                dict["mode"] = port.Mode;
            EmitString(dict, "app_protocol", port.AppProtocol);

            FlattenExtensions(dict, port.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> VolumeToDict(ServiceVolumeConfig vol)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "type", vol.Type);
            EmitString(dict, "source", vol.Source);
            EmitString(dict, "target", vol.Target);
            EmitNonZeroBool(dict, "read_only", vol.ReadOnly);
            EmitString(dict, "consistency", vol.Consistency);

            if (vol.Bind != null)
            {
                var bind = new Dictionary<string, object?>(StringComparer.Ordinal);
                EmitString(bind, "selinux", vol.Bind.SELinux);
                EmitString(bind, "propagation", vol.Bind.Propagation);
                if (vol.Bind.CreateHostPath)
                    bind["create_host_path"] = true;
                EmitString(bind, "recursive", vol.Bind.Recursive);
                FlattenExtensions(bind, vol.Bind.Extensions);
                if (bind.Count > 0)
                    dict["bind"] = bind;
            }

            if (vol.Volume != null)
            {
                var volOpts = new Dictionary<string, object?>(StringComparer.Ordinal);
                EmitLabels(volOpts, "labels", vol.Volume.Labels);
                EmitNonZeroBool(volOpts, "nocopy", vol.Volume.NoCopy);
                EmitString(volOpts, "subpath", vol.Volume.Subpath);
                FlattenExtensions(volOpts, vol.Volume.Extensions);
                if (volOpts.Count > 0)
                    dict["volume"] = volOpts;
            }

            if (vol.Tmpfs != null)
            {
                var tmpfs = new Dictionary<string, object?>(StringComparer.Ordinal);
                if (vol.Tmpfs.Size != UnitBytes.Zero)
                    tmpfs["size"] = vol.Tmpfs.Size.Bytes;
                if (vol.Tmpfs.Mode != 0)
                    tmpfs["mode"] = vol.Tmpfs.Mode;
                FlattenExtensions(tmpfs, vol.Tmpfs.Extensions);
                if (tmpfs.Count > 0)
                    dict["tmpfs"] = tmpfs;
            }

            if (vol.Image != null)
            {
                var img = new Dictionary<string, object?>(StringComparer.Ordinal);
                EmitString(img, "subpath", vol.Image.SubPath);
                FlattenExtensions(img, vol.Image.Extensions);
                if (img.Count > 0)
                    dict["image"] = img;
            }

            FlattenExtensions(dict, vol.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> DependsOnToDict(DependsOnConfig dependsOn)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in dependsOn.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var dep = new Dictionary<string, object?>(StringComparer.Ordinal);
                EmitString(dep, "condition", kvp.Value.Condition);
                EmitNonZeroBool(dep, "restart", kvp.Value.Restart);
                if (!kvp.Value.Required)
                    dep["required"] = false;
                FlattenExtensions(dep, kvp.Value.Extensions);
                dict[kvp.Key] = dep;
            }
            return dict;
        }

        private static Dictionary<string, object?> DeployConfigToDict(DeployConfig deploy)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "mode", deploy.Mode);
            if (deploy.Replicas.HasValue)
                dict["replicas"] = deploy.Replicas.Value;
            EmitLabels(dict, "labels", deploy.Labels);

            if (deploy.UpdateConfig != null)
                dict["update_config"] = UpdateConfigToDict(deploy.UpdateConfig);
            if (deploy.RollbackConfig != null)
                dict["rollback_config"] = UpdateConfigToDict(deploy.RollbackConfig);

            if (deploy.Resources.Limits != null || deploy.Resources.Reservations != null
                || deploy.Resources.Extensions.Count > 0)
            {
                dict["resources"] = ResourcesConfigToDict(deploy.Resources);
            }

            if (deploy.RestartPolicy != null)
                dict["restart_policy"] = RestartPolicyToDict(deploy.RestartPolicy);

            if (deploy.Placement.Constraints.Count > 0 || deploy.Placement.Preferences.Count > 0
                || deploy.Placement.MaxReplicas > 0 || deploy.Placement.Extensions.Count > 0)
            {
                dict["placement"] = PlacementToDict(deploy.Placement);
            }

            EmitString(dict, "endpoint_mode", deploy.EndpointMode);

            FlattenExtensions(dict, deploy.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> UpdateConfigToDict(UpdateConfig cfg)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (cfg.Parallelism.HasValue)
                dict["parallelism"] = cfg.Parallelism.Value;
            EmitDuration(dict, "delay", cfg.Delay);
            EmitString(dict, "failure_action", cfg.FailureAction);
            EmitDuration(dict, "monitor", cfg.Monitor);
            if (cfg.MaxFailureRatio != 0f)
                dict["max_failure_ratio"] = cfg.MaxFailureRatio;
            EmitString(dict, "order", cfg.Order);

            FlattenExtensions(dict, cfg.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> ResourcesConfigToDict(Resources res)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (res.Limits != null)
                dict["limits"] = ResourceToDict(res.Limits);
            if (res.Reservations != null)
                dict["reservations"] = ResourceToDict(res.Reservations);

            FlattenExtensions(dict, res.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> ResourceToDict(Resource res)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (res.NanoCPUs.Value != 0f)
                dict["cpus"] = res.NanoCPUs.Value;
            EmitUnitBytes(dict, "memory", res.MemoryBytes);
            EmitNonZeroLong(dict, "pids", res.Pids);
            if (res.Devices.Count > 0)
                dict["devices"] = res.Devices.Select(DeviceRequestToDict).ToList();
            if (res.GenericResources.Count > 0)
                dict["generic_resources"] = res.GenericResources.Select(GenericResourceToDict).ToList();

            FlattenExtensions(dict, res.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> DeviceRequestToDict(DeviceRequest dev)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitList(dict, "capabilities", dev.Capabilities);
            if (dev.Count != 0)
                dict["count"] = dev.Count;
            EmitList(dict, "device_ids", dev.DeviceIDs);
            EmitString(dict, "driver", dev.Driver);
            EmitStringDict(dict, "options", dev.Options);

            FlattenExtensions(dict, dev.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> GenericResourceToDict(GenericResource gr)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (gr.DiscreteResourceSpec != null)
            {
                var spec = new Dictionary<string, object?>(StringComparer.Ordinal);
                EmitString(spec, "kind", gr.DiscreteResourceSpec.Kind);
                if (gr.DiscreteResourceSpec.Value != 0)
                    spec["value"] = gr.DiscreteResourceSpec.Value;
                FlattenExtensions(spec, gr.DiscreteResourceSpec.Extensions);
                dict["discrete_resource_spec"] = spec;
            }

            FlattenExtensions(dict, gr.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> RestartPolicyToDict(RestartPolicy rp)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "condition", rp.Condition);
            EmitDuration(dict, "delay", rp.Delay);
            if (rp.MaxAttempts.HasValue)
                dict["max_attempts"] = rp.MaxAttempts.Value;
            EmitDuration(dict, "window", rp.Window);

            FlattenExtensions(dict, rp.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> PlacementToDict(Placement placement)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitList(dict, "constraints", placement.Constraints);
            if (placement.Preferences.Count > 0)
            {
                dict["preferences"] = placement.Preferences.Select(p =>
                {
                    var d = new Dictionary<string, object?>(StringComparer.Ordinal);
                    EmitString(d, "spread", p.Spread);
                    FlattenExtensions(d, p.Extensions);
                    return d;
                }).ToList();
            }
            if (placement.MaxReplicas > 0)
                dict["max_replicas_per_node"] = placement.MaxReplicas;

            FlattenExtensions(dict, placement.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> HealthCheckToDict(HealthCheckConfig hc)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitList(dict, "test", hc.Test);
            EmitDuration(dict, "interval", hc.Interval);
            EmitDuration(dict, "timeout", hc.Timeout);
            if (hc.Retries.HasValue && hc.Retries.Value != 0)
                dict["retries"] = hc.Retries.Value;
            EmitDuration(dict, "start_period", hc.StartPeriod);
            EmitDuration(dict, "start_interval", hc.StartInterval);
            EmitNonZeroBool(dict, "disable", hc.Disable);

            FlattenExtensions(dict, hc.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> LoggingConfigToDict(LoggingConfig logging)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "driver", logging.Driver);
            EmitStringDict(dict, "options", logging.Options);

            FlattenExtensions(dict, logging.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> CredentialSpecToDict(CredentialSpecConfig cs)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "config", cs.Config);
            EmitString(dict, "file", cs.File);
            EmitString(dict, "registry", cs.Registry);

            FlattenExtensions(dict, cs.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> DeviceMappingToDict(DeviceMapping dm)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "source", dm.Source);
            EmitString(dict, "target", dm.Target);
            EmitString(dict, "permissions", dm.Permissions);

            FlattenExtensions(dict, dm.Extensions);
            return dict;
        }

        private static object EnvFileToDict(EnvFile ef)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            dict["path"] = ef.Path;
            if (!ef.Required)
                dict["required"] = false;
            EmitString(dict, "format", ef.Format);
            FlattenExtensions(dict, ef.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> ExtendsConfigToDict(ExtendsConfig ext)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            EmitString(dict, "file", ext.File);
            EmitString(dict, "service", ext.Service);
            return dict;
        }

        private static object ServiceSecretToDict(ServiceSecretConfig sc)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            EmitString(dict, "source", sc.Source);
            EmitString(dict, "target", sc.Target);
            EmitString(dict, "uid", sc.UID);
            EmitString(dict, "gid", sc.GID);
            if (sc.Mode.HasValue)
                dict["mode"] = sc.Mode.Value;
            FlattenExtensions(dict, sc.Extensions);
            return dict;
        }

        private static object ServiceConfigObjToDict(ServiceConfigObjConfig cc)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            EmitString(dict, "source", cc.Source);
            EmitString(dict, "target", cc.Target);
            EmitString(dict, "uid", cc.UID);
            EmitString(dict, "gid", cc.GID);
            if (cc.Mode.HasValue)
                dict["mode"] = cc.Mode.Value;
            FlattenExtensions(dict, cc.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> UlimitsToDict(Dictionary<string, UlimitsConfig> ulimits)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in ulimits.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var u = kvp.Value;
                if (u.Single != 0)
                {
                    dict[kvp.Key] = u.Single;
                }
                else
                {
                    var inner = new Dictionary<string, object?>(StringComparer.Ordinal);
                    inner["soft"] = u.Soft;
                    inner["hard"] = u.Hard;
                    FlattenExtensions(inner, u.Extensions);
                    dict[kvp.Key] = inner;
                }
            }
            return dict;
        }

        private static Dictionary<string, object?> DevelopConfigToDict(DevelopConfig dev)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (dev.Watch.Count > 0)
            {
                dict["watch"] = dev.Watch.Select(t =>
                {
                    var d = new Dictionary<string, object?>(StringComparer.Ordinal);
                    EmitString(d, "path", t.Path);
                    EmitString(d, "action", t.Action);
                    EmitString(d, "target", t.Target);
                    if (t.Exec != null)
                        d["exec"] = ServiceHookToDict(t.Exec);
                    EmitList(d, "include", t.Include);
                    EmitList(d, "ignore", t.Ignore);
                    EmitNonZeroBool(d, "initial_sync", t.InitialSync);
                    FlattenExtensions(d, t.Extensions);
                    return d;
                }).ToList();
            }

            FlattenExtensions(dict, dev.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> ProviderConfigToDict(ServiceProviderConfig prov)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "type", prov.Type);
            if (prov.Options.Count > 0)
            {
                var opts = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in prov.Options.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    opts[kvp.Key] = kvp.Value.ToList<object?>();
                }
                dict["options"] = opts;
            }

            FlattenExtensions(dict, prov.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> ServiceModelConfigToDict(ServiceModelConfig mc)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitString(dict, "endpoint_variable", mc.EndpointVariable);
            EmitString(dict, "model_variable", mc.ModelVariable);

            FlattenExtensions(dict, mc.Extensions);
            return dict;
        }

        private static Dictionary<string, object?> ServiceHookToDict(ServiceHook hook)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

            EmitShellCommand(dict, "command", hook.Command);
            EmitString(dict, "user", hook.User);
            EmitNonZeroBool(dict, "privileged", hook.Privileged);
            EmitString(dict, "working_dir", hook.WorkingDir);
            EmitMappingWithEquals(dict, "environment", hook.Environment);

            FlattenExtensions(dict, hook.Extensions);
            return dict;
        }

        #endregion

        #region Emit helpers

        /// <summary>
        /// Merges extension <c>x-*</c> keys into the parent dictionary.
        /// </summary>
        private static void FlattenExtensions(Dictionary<string, object?> dict, Extensions? extensions)
        {
            if (extensions == null || extensions.Count == 0) return;
            foreach (var kvp in extensions.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                dict[kvp.Key] = ConvertExtensionValue(kvp.Value);
            }
        }

        /// <summary>
        /// Recursively converts extension values to serializable types.
        /// </summary>
        private static object? ConvertExtensionValue(object? value)
        {
            return value switch
            {
                null => null,
                Extensions ext => ext.OrderBy(k => k.Key, StringComparer.Ordinal)
                    .ToDictionary(k => k.Key, k => ConvertExtensionValue(k.Value), StringComparer.Ordinal),
                IDictionary<string, object> dict => dict.OrderBy(k => k.Key, StringComparer.Ordinal)
                    .ToDictionary(k => k.Key, k => ConvertExtensionValue(k.Value), StringComparer.Ordinal),
                IList<object> list => list.Select(ConvertExtensionValue).ToList(),
                _ => value
            };
        }

        private static void EmitString(Dictionary<string, object?> dict, string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                dict[key] = value;
        }

        private static void EmitNonZeroBool(Dictionary<string, object?> dict, string key, bool value)
        {
            if (value)
                dict[key] = true;
        }

        private static void EmitNullableBool(Dictionary<string, object?> dict, string key, bool? value)
        {
            if (value.HasValue)
                dict[key] = value.Value;
        }

        private static void EmitNonZeroLong(Dictionary<string, object?> dict, string key, long value)
        {
            if (value != 0)
                dict[key] = value;
        }

        private static void EmitNullableInt(Dictionary<string, object?> dict, string key, int? value)
        {
            if (value.HasValue)
                dict[key] = value.Value;
        }

        private static void EmitNonZeroFloat(Dictionary<string, object?> dict, string key, float value)
        {
            if (value != 0f)
                dict[key] = value;
        }

        private static void EmitList(Dictionary<string, object?> dict, string key, ICollection<string> list)
        {
            if (list.Count > 0)
                dict[key] = list.ToList<object?>();
        }

        private static void EmitStringList(Dictionary<string, object?> dict, string key, ICollection<string> list)
        {
            if (list.Count > 0)
                dict[key] = list.ToList<object?>();
        }

        private static void EmitStringOrNumberList(Dictionary<string, object?> dict, string key, StringOrNumberList list)
        {
            if (list.Count > 0)
                dict[key] = list.ToList<object?>();
        }

        private static void EmitSSHConfig(Dictionary<string, object?> dict, string key, SSHConfig ssh)
        {
            if (ssh.Count == 0) return;
            var sshDict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var sshKey in ssh)
            {
                sshDict[sshKey.ID] = string.IsNullOrEmpty(sshKey.Path) ? null : (object)sshKey.Path;
            }
            dict[key] = sshDict;
        }

        private static void EmitMapping(Dictionary<string, object?> dict, string key, Mapping mapping)
        {
            if (mapping.Count == 0) return;
            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in mapping.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                d[kvp.Key] = kvp.Value;
            }
            dict[key] = d;
        }

        private static void EmitMappingWithEquals(Dictionary<string, object?> dict, string key, MappingWithEquals mapping)
        {
            if (mapping.Count == 0) return;
            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in mapping.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                d[kvp.Key] = kvp.Value;
            }
            dict[key] = d;
        }

        private static void EmitLabels(Dictionary<string, object?> dict, string key, Labels? labels)
        {
            if (labels == null || labels.Count == 0) return;
            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in labels.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                d[kvp.Key] = kvp.Value;
            }
            dict[key] = d;
        }

        private static void EmitStringDict(Dictionary<string, object?> dict, string key, Dictionary<string, string> map)
        {
            if (map.Count == 0) return;
            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in map.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                d[kvp.Key] = kvp.Value;
            }
            dict[key] = d;
        }

        private static void EmitNullableStringDict(Dictionary<string, object?> dict, string key, Dictionary<string, string?> map)
        {
            if (map.Count == 0) return;
            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in map.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                d[kvp.Key] = kvp.Value;
            }
            dict[key] = d;
        }

        private static void EmitShellCommand(Dictionary<string, object?> dict, string key, ShellCommand? cmd)
        {
            if (cmd == null) return;
            if (cmd.IsShellForm)
            {
                dict[key] = cmd.Parts[0];
            }
            else
            {
                dict[key] = cmd.Parts.ToList<object?>();
            }
        }

        private static void EmitHostsList(Dictionary<string, object?> dict, string key, HostsList hosts)
        {
            if (hosts.Count == 0) return;
            dict[key] = hosts.Select(h => (object?)h.ToString()).ToList();
        }

        private static void EmitUnitBytes(Dictionary<string, object?> dict, string key, UnitBytes value)
        {
            if (value.Bytes != 0)
                dict[key] = value.ToString();
        }

        private static void EmitDuration(Dictionary<string, object?> dict, string key, Duration? value)
        {
            if (value.HasValue)
                dict[key] = value.Value.ToString();
        }

        #endregion
    }
}
