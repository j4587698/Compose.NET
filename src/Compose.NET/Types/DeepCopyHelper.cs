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

namespace Compose.NET.Types;

/// <summary>
/// Provides deep-copy helper methods for all Compose.NET model types.
/// Mirrors compose-go's code-generated <c>deepCopy</c> functions (via goderive).
/// </summary>
internal static class DeepCopyHelper
{
    // â”€â”€ Wrapper / collection types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal static Labels? CopyLabels(Labels? src) =>
        src == null ? null : new Labels(src);

    internal static StringList? CopyStringList(StringList? src) =>
        src == null ? null : new StringList(src);

    internal static StringOrNumberList? CopyStringOrNumberList(StringOrNumberList? src) =>
        src == null ? null : new StringOrNumberList(src);

    internal static HostsList? CopyHostsList(HostsList? src) =>
        src == null ? null : new HostsList(src);

    internal static Mapping? CopyMapping(Mapping? src) =>
        src == null ? null : (Mapping)src.Clone();

    internal static MappingWithEquals? CopyMappingWithEquals(MappingWithEquals? src) =>
        src == null ? null : new MappingWithEquals(src);

    internal static Extensions CopyExtensions(Extensions? src) =>
        src == null ? new Extensions() : src.DeepCopy();

    internal static ShellCommand? CopyShellCommand(ShellCommand? src)
    {
        if (src == null) return null;
        return src.IsShellForm
            ? ShellCommand.FromShellForm(src.Parts[0])
            : ShellCommand.FromExecForm(src.Parts);
    }

    internal static List<string> CopyStringList(List<string>? src) =>
        src == null ? new List<string>() : new List<string>(src);

    internal static List<string>? CopyStringListNullable(List<string>? src) =>
        src == null ? null : new List<string>(src);

    internal static Dictionary<string, string> CopyStringDict(Dictionary<string, string>? src) =>
        src == null ? new Dictionary<string, string>() : new Dictionary<string, string>(src);

    internal static Dictionary<string, string?> CopyNullableStringDict(Dictionary<string, string?>? src) =>
        src == null ? new Dictionary<string, string?>() : new Dictionary<string, string?>(src);

    // â”€â”€ Dictionary container types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal static Services CopyServices(Services? src)
    {
        var copy = new Services();
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopyServiceConfig(kvp.Value);
        return copy;
    }

    internal static Networks CopyNetworks(Networks? src)
    {
        var copy = new Networks();
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopyNetworkConfig(kvp.Value);
        return copy;
    }

    internal static Volumes CopyVolumes(Volumes? src)
    {
        var copy = new Volumes();
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopyVolumeConfig(kvp.Value);
        return copy;
    }

    internal static Secrets CopySecrets(Secrets? src)
    {
        var copy = new Secrets();
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopySecretConfig(kvp.Value);
        return copy;
    }

    internal static Configs CopyConfigs(Configs? src)
    {
        var copy = new Configs();
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopyConfigObjConfig(kvp.Value);
        return copy;
    }

    internal static Models CopyModels(Models? src)
    {
        var copy = new Models();
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopyModelConfig(kvp.Value);
        return copy;
    }

    internal static DependsOnConfig CopyDependsOn(DependsOnConfig? src)
    {
        if (src == null) return new DependsOnConfig();
        var copy = new DependsOnConfig();
        foreach (var kvp in src)
            copy[kvp.Key] = CopyServiceDependency(kvp.Value);
        return copy;
    }

    // â”€â”€ ServiceConfig (the big one) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal static ServiceConfig CopyServiceConfig(ServiceConfig src)
    {
        return new ServiceConfig
        {
            Name = src.Name,
            Profiles = CopyStringList(src.Profiles),
            Annotations = CopyMapping(src.Annotations) ?? new Mapping(),
            Attach = src.Attach,
            Build = CopyBuildConfig(src.Build),
            BlkioConfig = CopyBlkioConfig(src.BlkioConfig),
            CapAdd = CopyStringList(src.CapAdd),
            CapDrop = CopyStringList(src.CapDrop),
            CgroupParent = src.CgroupParent,
            Cgroup = src.Cgroup,
            CPUCount = src.CPUCount,
            CPUPercent = src.CPUPercent,
            CPUPeriod = src.CPUPeriod,
            CPUQuota = src.CPUQuota,
            CPURTPeriod = src.CPURTPeriod,
            CPURTRuntime = src.CPURTRuntime,
            CPUS = src.CPUS,
            CPUSet = src.CPUSet,
            CPUShares = src.CPUShares,
            Command = CopyShellCommand(src.Command),
            Configs = CopyList(src.Configs, CopyServiceConfigObjConfig),
            ContainerName = src.ContainerName,
            CredentialSpec = CopyCredentialSpecConfig(src.CredentialSpec),
            DependsOn = CopyDependsOn(src.DependsOn),
            Deploy = CopyDeployConfig(src.Deploy),
            DeviceCgroupRules = CopyStringList(src.DeviceCgroupRules),
            Devices = CopyList(src.Devices, CopyDeviceMapping),
            DNS = CopyStringList(src.DNS) ?? new StringList(),
            DNSOpts = CopyStringList(src.DNSOpts),
            DNSSearch = CopyStringList(src.DNSSearch) ?? new StringList(),
            Dockerfile = src.Dockerfile,
            DomainName = src.DomainName,
            Entrypoint = CopyShellCommand(src.Entrypoint),
            Environment = CopyMappingWithEquals(src.Environment) ?? new MappingWithEquals(),
            EnvFiles = CopyList(src.EnvFiles, CopyEnvFile),
            Expose = CopyStringOrNumberList(src.Expose) ?? new StringOrNumberList(),
            Extends = CopyExtendsConfig(src.Extends),
            ExternalLinks = CopyStringList(src.ExternalLinks),
            ExtraHosts = CopyHostsList(src.ExtraHosts) ?? new HostsList(),
            GroupAdd = CopyStringList(src.GroupAdd),
            Gpus = CopyList(src.Gpus, CopyDeviceRequest),
            Hostname = src.Hostname,
            HealthCheck = CopyHealthCheckConfig(src.HealthCheck),
            Image = src.Image,
            Init = src.Init,
            Ipc = src.Ipc,
            Isolation = src.Isolation,
            Labels = CopyLabels(src.Labels) ?? new Labels(),
            LabelFiles = CopyStringList(src.LabelFiles),
            Links = CopyStringList(src.Links),
            Logging = CopyLoggingConfig(src.Logging),
            MemLimit = src.MemLimit,
            MemReservation = src.MemReservation,
            MemSwapLimit = src.MemSwapLimit,
            MemSwappiness = src.MemSwappiness,
            MacAddress = src.MacAddress,
            NetworkMode = src.NetworkMode,
            Networks = CopyServiceNetworks(src.Networks),
            OomKillDisable = src.OomKillDisable,
            OomScoreAdj = src.OomScoreAdj,
            Pid = src.Pid,
            PidsLimit = src.PidsLimit,
            Platform = src.Platform,
            Ports = CopyList(src.Ports, CopyServicePortConfig),
            Privileged = src.Privileged,
            PullPolicy = src.PullPolicy,
            ReadOnly = src.ReadOnly,
            Restart = src.Restart,
            Runtime = src.Runtime,
            Scale = src.Scale,
            Secrets = CopyList(src.Secrets, CopyServiceSecretConfig),
            SecurityOpt = CopyStringList(src.SecurityOpt),
            ShmSize = src.ShmSize,
            StdinOpen = src.StdinOpen,
            StopGracePeriod = src.StopGracePeriod,
            StopSignal = src.StopSignal,
            StorageOpt = CopyStringDict(src.StorageOpt),
            Sysctls = CopyMapping(src.Sysctls) ?? new Mapping(),
            Tmpfs = CopyStringList(src.Tmpfs) ?? new StringList(),
            Tty = src.Tty,
            Ulimits = CopyUlimitsDict(src.Ulimits),
            User = src.User,
            UserNSMode = src.UserNSMode,
            Uts = src.Uts,
            VolumeDriver = src.VolumeDriver,
            Volumes = CopyList(src.Volumes, CopyServiceVolumeConfig),
            VolumesFrom = CopyStringList(src.VolumesFrom),
            WorkingDir = src.WorkingDir,
            Develop = CopyDevelopConfig(src.Develop),
            Provider = CopyServiceProviderConfig(src.Provider),
            ServiceModels = CopyServiceModels(src.ServiceModels),
            PostStart = CopyList(src.PostStart, CopyServiceHook),
            PreStop = CopyList(src.PreStop, CopyServiceHook),
            LogDriver = src.LogDriver,
            LogOpt = CopyStringDict(src.LogOpt),
            Net = src.Net,
            CustomLabels = CopyLabels(src.CustomLabels) ?? new Labels(),
            UseAPISocket = src.UseAPISocket,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    // â”€â”€ Nested config types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal static BuildConfig? CopyBuildConfig(BuildConfig? src)
    {
        if (src == null) return null;
        return new BuildConfig
        {
            Context = src.Context,
            Dockerfile = src.Dockerfile,
            DockerfileInline = src.DockerfileInline,
            Args = CopyNullableStringDict(src.Args),
            SSH = CopySSHConfig(src.SSH),
            CacheFrom = CopyStringList(src.CacheFrom),
            CacheTo = CopyStringList(src.CacheTo),
            AdditionalContexts = CopyMapping(src.AdditionalContexts) ?? new Mapping(),
            Labels = CopyLabels(src.Labels) ?? new Labels(),
            NoCache = src.NoCache,
            Pull = src.Pull,
            Target = src.Target,
            ShmSize = src.ShmSize,
            ExtraHosts = CopyHostsList(src.ExtraHosts) ?? new HostsList(),
            Isolation = src.Isolation,
            Privileged = src.Privileged,
            Tags = CopyStringList(src.Tags),
            Platforms = CopyStringList(src.Platforms),
            Network = src.Network,
            Secrets = CopyList(src.Secrets, CopyServiceSecretConfig),
            Entitlements = CopyStringList(src.Entitlements),
            Provenance = src.Provenance,
            SBOM = src.SBOM,
            NoCacheFilter = CopyStringList(src.NoCacheFilter),
            Ulimits = CopyUlimitsDict(src.Ulimits),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static BlkioConfig? CopyBlkioConfig(BlkioConfig? src)
    {
        if (src == null) return null;
        return new BlkioConfig
        {
            Weight = src.Weight,
            WeightDevice = CopyList(src.WeightDevice, CopyWeightDevice),
            DeviceReadBps = CopyList(src.DeviceReadBps, CopyThrottleDevice),
            DeviceReadIOps = CopyList(src.DeviceReadIOps, CopyThrottleDevice),
            DeviceWriteBps = CopyList(src.DeviceWriteBps, CopyThrottleDevice),
            DeviceWriteIOps = CopyList(src.DeviceWriteIOps, CopyThrottleDevice),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static WeightDevice CopyWeightDevice(WeightDevice src)
    {
        return new WeightDevice
        {
            Path = src.Path,
            Weight = src.Weight,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ThrottleDevice CopyThrottleDevice(ThrottleDevice src)
    {
        return new ThrottleDevice
        {
            Path = src.Path,
            Rate = src.Rate,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static DeployConfig? CopyDeployConfig(DeployConfig? src)
    {
        if (src == null) return null;
        return new DeployConfig
        {
            Mode = src.Mode,
            Replicas = src.Replicas,
            Labels = CopyLabels(src.Labels) ?? new Labels(),
            UpdateConfig = CopyUpdateConfig(src.UpdateConfig),
            RollbackConfig = CopyUpdateConfig(src.RollbackConfig),
            Resources = CopyResources(src.Resources),
            RestartPolicy = CopyRestartPolicy(src.RestartPolicy),
            Placement = CopyPlacement(src.Placement),
            EndpointMode = src.EndpointMode,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static UpdateConfig? CopyUpdateConfig(UpdateConfig? src)
    {
        if (src == null) return null;
        return new UpdateConfig
        {
            Parallelism = src.Parallelism,
            Delay = src.Delay,
            FailureAction = src.FailureAction,
            Monitor = src.Monitor,
            MaxFailureRatio = src.MaxFailureRatio,
            Order = src.Order,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static Resources CopyResources(Resources? src)
    {
        if (src == null) return new Resources();
        return new Resources
        {
            Limits = CopyResource(src.Limits),
            Reservations = CopyResource(src.Reservations),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static Resource? CopyResource(Resource? src)
    {
        if (src == null) return null;
        return new Resource
        {
            NanoCPUs = src.NanoCPUs,
            MemoryBytes = src.MemoryBytes,
            Pids = src.Pids,
            Devices = CopyList(src.Devices, CopyDeviceRequest),
            GenericResources = CopyList(src.GenericResources, CopyGenericResource),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static DeviceRequest CopyDeviceRequest(DeviceRequest src)
    {
        return new DeviceRequest
        {
            Capabilities = CopyStringList(src.Capabilities),
            Count = src.Count,
            DeviceIDs = CopyStringList(src.DeviceIDs),
            Driver = src.Driver,
            Options = CopyStringDict(src.Options),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static GenericResource CopyGenericResource(GenericResource src)
    {
        return new GenericResource
        {
            DiscreteResourceSpec = CopyDiscreteGenericResource(src.DiscreteResourceSpec),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static DiscreteGenericResource? CopyDiscreteGenericResource(DiscreteGenericResource? src)
    {
        if (src == null) return null;
        return new DiscreteGenericResource
        {
            Kind = src.Kind,
            Value = src.Value,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static RestartPolicy? CopyRestartPolicy(RestartPolicy? src)
    {
        if (src == null) return null;
        return new RestartPolicy
        {
            Condition = src.Condition,
            Delay = src.Delay,
            MaxAttempts = src.MaxAttempts,
            Window = src.Window,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static Placement CopyPlacement(Placement? src)
    {
        if (src == null) return new Placement();
        return new Placement
        {
            Constraints = CopyStringList(src.Constraints),
            Preferences = CopyList(src.Preferences, CopyPlacementPreferences),
            MaxReplicas = src.MaxReplicas,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static PlacementPreferences CopyPlacementPreferences(PlacementPreferences src)
    {
        return new PlacementPreferences
        {
            Spread = src.Spread,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static HealthCheckConfig? CopyHealthCheckConfig(HealthCheckConfig? src)
    {
        if (src == null) return null;
        return new HealthCheckConfig
        {
            Test = CopyStringList(src.Test),
            Interval = src.Interval,
            Timeout = src.Timeout,
            Retries = src.Retries,
            StartPeriod = src.StartPeriod,
            StartInterval = src.StartInterval,
            Disable = src.Disable,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static LoggingConfig? CopyLoggingConfig(LoggingConfig? src)
    {
        if (src == null) return null;
        return new LoggingConfig
        {
            Driver = src.Driver,
            Options = CopyStringDict(src.Options),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static CredentialSpecConfig? CopyCredentialSpecConfig(CredentialSpecConfig? src)
    {
        if (src == null) return null;
        return new CredentialSpecConfig
        {
            Config = src.Config,
            File = src.File,
            Registry = src.Registry,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static DeviceMapping CopyDeviceMapping(DeviceMapping src)
    {
        return new DeviceMapping
        {
            Source = src.Source,
            Target = src.Target,
            Permissions = src.Permissions,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ExtendsConfig? CopyExtendsConfig(ExtendsConfig? src)
    {
        if (src == null) return null;
        return new ExtendsConfig
        {
            File = src.File,
            Service = src.Service
        };
    }

    internal static ServiceHook CopyServiceHook(ServiceHook src)
    {
        return new ServiceHook
        {
            Command = CopyShellCommand(src.Command),
            User = src.User,
            Privileged = src.Privileged,
            WorkingDir = src.WorkingDir,
            Environment = CopyMappingWithEquals(src.Environment) ?? new MappingWithEquals(),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static EnvFile CopyEnvFile(EnvFile src)
    {
        return new EnvFile
        {
            Path = src.Path,
            Required = src.Required,
            Format = src.Format,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceDependency CopyServiceDependency(ServiceDependency src)
    {
        return new ServiceDependency
        {
            Condition = src.Condition,
            Restart = src.Restart,
            Required = src.Required,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static UlimitsConfig CopyUlimitsConfig(UlimitsConfig src)
    {
        return new UlimitsConfig
        {
            Single = src.Single,
            Soft = src.Soft,
            Hard = src.Hard,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static Dictionary<string, UlimitsConfig> CopyUlimitsDict(Dictionary<string, UlimitsConfig>? src)
    {
        var copy = new Dictionary<string, UlimitsConfig>(StringComparer.Ordinal);
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopyUlimitsConfig(kvp.Value);
        return copy;
    }

    internal static ServicePortConfig CopyServicePortConfig(ServicePortConfig src)
    {
        return new ServicePortConfig
        {
            Name = src.Name,
            Mode = src.Mode,
            HostIP = src.HostIP,
            Target = src.Target,
            Published = src.Published,
            Protocol = src.Protocol,
            AppProtocol = src.AppProtocol,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceVolumeConfig CopyServiceVolumeConfig(ServiceVolumeConfig src)
    {
        return new ServiceVolumeConfig
        {
            Type = src.Type,
            Source = src.Source,
            Target = src.Target,
            ReadOnly = src.ReadOnly,
            Consistency = src.Consistency,
            Bind = CopyServiceVolumeBind(src.Bind),
            Volume = CopyServiceVolumeVolume(src.Volume),
            Tmpfs = CopyServiceVolumeTmpfs(src.Tmpfs),
            Image = CopyServiceVolumeImage(src.Image),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceVolumeBind? CopyServiceVolumeBind(ServiceVolumeBind? src)
    {
        if (src == null) return null;
        return new ServiceVolumeBind
        {
            SELinux = src.SELinux,
            Propagation = src.Propagation,
            CreateHostPath = src.CreateHostPath,
            Recursive = src.Recursive,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceVolumeVolume? CopyServiceVolumeVolume(ServiceVolumeVolume? src)
    {
        if (src == null) return null;
        return new ServiceVolumeVolume
        {
            Labels = CopyLabels(src.Labels),
            NoCopy = src.NoCopy,
            Subpath = src.Subpath,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceVolumeTmpfs? CopyServiceVolumeTmpfs(ServiceVolumeTmpfs? src)
    {
        if (src == null) return null;
        return new ServiceVolumeTmpfs
        {
            Size = src.Size,
            Mode = src.Mode,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceVolumeImage? CopyServiceVolumeImage(ServiceVolumeImage? src)
    {
        if (src == null) return null;
        return new ServiceVolumeImage
        {
            SubPath = src.SubPath,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceSecretConfig CopyServiceSecretConfig(ServiceSecretConfig src)
    {
        return new ServiceSecretConfig
        {
            Source = src.Source,
            Target = src.Target,
            UID = src.UID,
            GID = src.GID,
            Mode = src.Mode,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceConfigObjConfig CopyServiceConfigObjConfig(ServiceConfigObjConfig src)
    {
        return new ServiceConfigObjConfig
        {
            Source = src.Source,
            Target = src.Target,
            UID = src.UID,
            GID = src.GID,
            Mode = src.Mode,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static NetworkConfig CopyNetworkConfig(NetworkConfig src)
    {
        return new NetworkConfig
        {
            Name = src.Name,
            Driver = src.Driver,
            DriverOpts = CopyStringDict(src.DriverOpts),
            Ipam = CopyIPAMConfig(src.Ipam),
            External = src.External,
            Internal = src.Internal,
            Attachable = src.Attachable,
            Labels = CopyLabels(src.Labels) ?? new Labels(),
            CustomLabels = CopyLabels(src.CustomLabels) ?? new Labels(),
            EnableIPv4 = src.EnableIPv4,
            EnableIPv6 = src.EnableIPv6,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static IPAMConfig CopyIPAMConfig(IPAMConfig? src)
    {
        if (src == null) return new IPAMConfig();
        return new IPAMConfig
        {
            Driver = src.Driver,
            Config = CopyList(src.Config, CopyIPAMPool),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static IPAMPool CopyIPAMPool(IPAMPool src)
    {
        return new IPAMPool
        {
            Subnet = src.Subnet,
            Gateway = src.Gateway,
            IPRange = src.IPRange,
            AuxiliaryAddresses = CopyMapping(src.AuxiliaryAddresses) ?? new Mapping(),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceNetworkConfig? CopyServiceNetworkConfig(ServiceNetworkConfig? src)
    {
        if (src == null) return null;
        return new ServiceNetworkConfig
        {
            Aliases = CopyStringList(src.Aliases),
            DriverOpts = CopyStringDict(src.DriverOpts),
            GatewayPriority = src.GatewayPriority,
            InterfaceName = src.InterfaceName,
            Ipv4Address = src.Ipv4Address,
            Ipv6Address = src.Ipv6Address,
            LinkLocalIPs = CopyStringList(src.LinkLocalIPs),
            MacAddress = src.MacAddress,
            Priority = src.Priority,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static Dictionary<string, ServiceNetworkConfig?> CopyServiceNetworks(
        Dictionary<string, ServiceNetworkConfig?>? src)
    {
        var copy = new Dictionary<string, ServiceNetworkConfig?>(StringComparer.Ordinal);
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopyServiceNetworkConfig(kvp.Value);
        return copy;
    }

    internal static VolumeConfig CopyVolumeConfig(VolumeConfig src)
    {
        return new VolumeConfig
        {
            Name = src.Name,
            Driver = src.Driver,
            DriverOpts = CopyStringDict(src.DriverOpts),
            External = src.External,
            Labels = CopyLabels(src.Labels) ?? new Labels(),
            CustomLabels = CopyLabels(src.CustomLabels) ?? new Labels(),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static SecretConfig CopySecretConfig(SecretConfig src)
    {
        return new SecretConfig
        {
            Name = src.Name,
            File = src.File,
            Environment = src.Environment,
            Content = src.Content,
            External = src.External,
            Labels = CopyLabels(src.Labels) ?? new Labels(),
            Driver = src.Driver,
            DriverOpts = CopyStringDict(src.DriverOpts),
            TemplateDriver = src.TemplateDriver,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ConfigObjConfig CopyConfigObjConfig(ConfigObjConfig src)
    {
        return new ConfigObjConfig
        {
            Name = src.Name,
            File = src.File,
            Environment = src.Environment,
            Content = src.Content,
            External = src.External,
            Labels = CopyLabels(src.Labels) ?? new Labels(),
            Driver = src.Driver,
            DriverOpts = CopyStringDict(src.DriverOpts),
            TemplateDriver = src.TemplateDriver,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ModelConfig CopyModelConfig(ModelConfig src)
    {
        return new ModelConfig
        {
            Name = src.Name,
            Model = src.Model,
            ContextSize = src.ContextSize,
            RuntimeFlags = CopyStringList(src.RuntimeFlags),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceModelConfig? CopyServiceModelConfig(ServiceModelConfig? src)
    {
        if (src == null) return null;
        return new ServiceModelConfig
        {
            EndpointVariable = src.EndpointVariable,
            ModelVariable = src.ModelVariable,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static Dictionary<string, ServiceModelConfig?> CopyServiceModels(
        Dictionary<string, ServiceModelConfig?>? src)
    {
        var copy = new Dictionary<string, ServiceModelConfig?>(StringComparer.Ordinal);
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = CopyServiceModelConfig(kvp.Value);
        return copy;
    }

    internal static DevelopConfig? CopyDevelopConfig(DevelopConfig? src)
    {
        if (src == null) return null;
        return new DevelopConfig
        {
            Watch = CopyList(src.Watch, CopyTrigger),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static Trigger CopyTrigger(Trigger src)
    {
        return new Trigger
        {
            Path = src.Path,
            Action = src.Action,
            Target = src.Target,
            Exec = src.Exec == null ? null : CopyServiceHook(src.Exec),
            Include = CopyStringList(src.Include),
            Ignore = CopyStringList(src.Ignore),
            InitialSync = src.InitialSync,
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static ServiceProviderConfig? CopyServiceProviderConfig(ServiceProviderConfig? src)
    {
        if (src == null) return null;
        return new ServiceProviderConfig
        {
            Type = src.Type,
            Options = CopyProviderOptions(src.Options),
            Extensions = CopyExtensions(src.Extensions)
        };
    }

    internal static Dictionary<string, List<string>> CopyProviderOptions(
        Dictionary<string, List<string>>? src)
    {
        var copy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (src == null) return copy;
        foreach (var kvp in src)
            copy[kvp.Key] = new List<string>(kvp.Value);
        return copy;
    }

    internal static IncludeConfig CopyIncludeConfig(IncludeConfig src)
    {
        return new IncludeConfig
        {
            Path = CopyStringList(src.Path) ?? new StringList(),
            ProjectDirectory = src.ProjectDirectory,
            EnvFile = CopyStringList(src.EnvFile) ?? new StringList()
        };
    }

    // â”€â”€ Generic list copier â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal static List<T> CopyList<T>(List<T>? src, Func<T, T> copier)
    {
        if (src == null) return new List<T>();
        var copy = new List<T>(src.Count);
        foreach (var item in src)
            copy.Add(copier(item));
        return copy;
    }

    // â”€â”€ SSH config copier â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal static SSHConfig CopySSHConfig(SSHConfig? src)
    {
        if (src == null) return new SSHConfig();
        var copy = new SSHConfig();
        foreach (var key in src)
            copy.Add(new SSHKey { ID = key.ID, Path = key.Path });
        return copy;
    }
}
