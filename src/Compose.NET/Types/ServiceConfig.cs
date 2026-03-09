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

#region Constants

/// <summary>
/// Well-known pull policy values for a Compose service.
/// </summary>
public static class PullPolicies
{
    /// <summary>Always pull the image, even if it exists locally.</summary>
    public const string Always = "always";

    /// <summary>Never pull the image; it must exist locally.</summary>
    public const string Never = "never";

    /// <summary>Pull only if the image is not present locally.</summary>
    public const string IfNotPresent = "if_not_present";

    /// <summary>Alias for <see cref="IfNotPresent"/>.</summary>
    public const string Missing = "missing";

    /// <summary>Build the image instead of pulling it.</summary>
    public const string Build = "build";

    /// <summary>Refresh the image if a newer version is available.</summary>
    public const string Refresh = "refresh";
}

/// <summary>
/// Well-known restart policy values for a Compose service.
/// </summary>
public static class RestartPolicies
{
    /// <summary>Always restart the container.</summary>
    public const string Always = "always";

    /// <summary>Restart only on non-zero exit status.</summary>
    public const string OnFailure = "on-failure";

    /// <summary>Do not automatically restart.</summary>
    public const string No = "no";

    /// <summary>Restart unless manually stopped.</summary>
    public const string UnlessStopped = "unless-stopped";
}

/// <summary>
/// Common prefixes used in <c>network_mode</c>, <c>pid</c>, <c>ipc</c>, etc.
/// </summary>
public static class ServicePrefixes
{
    /// <summary>Reference another service (e.g. <c>"service:web"</c>).</summary>
    public const string Service = "service:";

    /// <summary>Reference an external container (e.g. <c>"container:abc123"</c>).</summary>
    public const string Container = "container:";
}

/// <summary>
/// Well-known service dependency condition values.
/// </summary>
public static class ServiceConditions
{
    /// <summary>The dependency completed successfully (exit code 0).</summary>
    public const string CompletedSuccessfully = "service_completed_successfully";

    /// <summary>The dependency reported healthy via its health check.</summary>
    public const string Healthy = "service_healthy";

    /// <summary>The dependency has started.</summary>
    public const string Started = "service_started";
}

#endregion

#region Services collection

/// <summary>
/// A dictionary of service name to <see cref="ServiceConfig"/>,
/// representing the top-level <c>services</c> section in a Compose file.
/// </summary>
public class Services : Dictionary<string, ServiceConfig>
{
    /// <summary>
    /// Initializes a new, empty <see cref="Services"/> instance.
    /// </summary>
    public Services() { }

    /// <summary>
    /// Initializes a new <see cref="Services"/> instance from an existing dictionary.
    /// </summary>
    /// <param name="source">The source dictionary to copy entries from.</param>
    public Services(IDictionary<string, ServiceConfig> source) : base(source) { }

    /// <summary>
    /// Returns a new <see cref="Services"/> containing only services matching the predicate.
    /// </summary>
    /// <param name="predicate">A function to test each service.</param>
    /// <returns>A new <see cref="Services"/> with the matching entries.</returns>
    public Services Filter(Func<ServiceConfig, bool> predicate)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        var result = new Services();
        foreach (var kvp in this)
        {
            if (predicate(kvp.Value))
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }
}

#endregion

#region ServiceConfig

/// <summary>
/// Complete service configuration, matching compose-go's <c>ServiceConfig</c> struct.
/// This is the central type that defines every configurable aspect of a Compose service.
/// </summary>
public class ServiceConfig
{
    #region Identity

    /// <summary>
    /// Gets or sets the name of the service, as defined in the Compose file.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the list of profiles this service belongs to.
    /// A service with no profiles is always started.
    /// </summary>
    public List<string> Profiles { get; set; } = new();

    #endregion

    #region Metadata

    /// <summary>
    /// Gets or sets the annotations applied to the service.
    /// </summary>
    public Mapping Annotations { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the service output is attached to the console.
    /// A <see langword="null"/> value means the default behavior is used.
    /// </summary>
    public bool? Attach { get; set; }

    #endregion

    #region Build

    /// <summary>
    /// Gets or sets the build configuration for the service.
    /// </summary>
    public BuildConfig? Build { get; set; }

    #endregion

    #region BlkIO

    /// <summary>
    /// Gets or sets the block I/O configuration for the service.
    /// </summary>
    public BlkioConfig? BlkioConfig { get; set; }

    #endregion

    #region Capabilities

    /// <summary>
    /// Gets or sets the list of Linux capabilities to add to the container.
    /// </summary>
    public List<string> CapAdd { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of Linux capabilities to drop from the container.
    /// </summary>
    public List<string> CapDrop { get; set; } = new();

    #endregion

    #region Cgroup

    /// <summary>
    /// Gets or sets the parent cgroup for the container.
    /// </summary>
    public string? CgroupParent { get; set; }

    /// <summary>
    /// Gets or sets the cgroup namespace mode (<c>"host"</c> or <c>"private"</c>).
    /// </summary>
    public string? Cgroup { get; set; }

    #endregion

    #region CPU resources

    /// <summary>
    /// Gets or sets the number of CPUs (Windows only).
    /// </summary>
    public long CPUCount { get; set; }

    /// <summary>
    /// Gets or sets the usable percentage of the available CPUs (Windows only).
    /// </summary>
    public float CPUPercent { get; set; }

    /// <summary>
    /// Gets or sets the CPU CFS scheduler period in microseconds.
    /// </summary>
    public long CPUPeriod { get; set; }

    /// <summary>
    /// Gets or sets the CPU CFS scheduler quota in microseconds.
    /// </summary>
    public long CPUQuota { get; set; }

    /// <summary>
    /// Gets or sets the CPU real-time period in microseconds.
    /// </summary>
    public long CPURTPeriod { get; set; }

    /// <summary>
    /// Gets or sets the CPU real-time runtime in microseconds.
    /// </summary>
    public long CPURTRuntime { get; set; }

    /// <summary>
    /// Gets or sets the number of CPUs as a fractional value (e.g. <c>0.5</c>).
    /// Uses the <see cref="Types.NanoCPUs"/> type to match compose-go's distinct type.
    /// </summary>
    public NanoCPUs CPUS { get; set; }

    /// <summary>
    /// Gets or sets the CPUs in which to allow execution (e.g. <c>"0-3"</c>, <c>"0,1"</c>).
    /// </summary>
    public string? CPUSet { get; set; }

    /// <summary>
    /// Gets or sets the CPU shares (relative weight).
    /// </summary>
    public long CPUShares { get; set; }

    #endregion

    #region Command

    /// <summary>
    /// Gets or sets the command to run in the container, overriding the image's default command.
    /// </summary>
    public ShellCommand? Command { get; set; }

    #endregion

    #region Configs

    /// <summary>
    /// Gets or sets the list of config objects to mount into this service.
    /// </summary>
    public List<ServiceConfigObjConfig> Configs { get; set; } = new();

    #endregion

    #region Container

    /// <summary>
    /// Gets or sets the custom container name.
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// Gets or sets the credential spec configuration for Windows containers.
    /// </summary>
    public CredentialSpecConfig? CredentialSpec { get; set; }

    #endregion

    #region Dependencies

    /// <summary>
    /// Gets or sets the service dependencies and their conditions.
    /// </summary>
    public DependsOnConfig DependsOn { get; set; } = new();

    #endregion

    #region Deploy

    /// <summary>
    /// Gets or sets the deployment configuration for the service.
    /// </summary>
    public DeployConfig? Deploy { get; set; }

    #endregion

    #region Devices

    /// <summary>
    /// Gets or sets the device cgroup rules for the container.
    /// </summary>
    public List<string> DeviceCgroupRules { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of device mappings (host device to container device).
    /// </summary>
    public List<DeviceMapping> Devices { get; set; } = new();

    #endregion

    #region DNS

    /// <summary>
    /// Gets or sets custom DNS servers for the container.
    /// </summary>
    public StringList DNS { get; set; } = new();

    /// <summary>
    /// Gets or sets custom DNS options.
    /// </summary>
    public List<string> DNSOpts { get; set; } = new();

    /// <summary>
    /// Gets or sets custom DNS search domains.
    /// </summary>
    public StringList DNSSearch { get; set; } = new();

    #endregion

    #region Dockerfile (legacy)

    /// <summary>
    /// Gets or sets the Dockerfile path (legacy, use <see cref="Build"/> instead).
    /// </summary>
    public string? Dockerfile { get; set; }

    /// <summary>
    /// Gets or sets the domain name for the container.
    /// </summary>
    public string? DomainName { get; set; }

    #endregion

    #region Entrypoint

    /// <summary>
    /// Gets or sets the entrypoint for the container, overriding the image's default entrypoint.
    /// </summary>
    public ShellCommand? Entrypoint { get; set; }

    #endregion

    #region Environment

    /// <summary>
    /// Gets or sets the environment variables for the container.
    /// </summary>
    public MappingWithEquals Environment { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of env files to load environment variables from.
    /// </summary>
    public List<EnvFile> EnvFiles { get; set; } = new();

    #endregion

    #region Expose

    /// <summary>
    /// Gets or sets the list of ports to expose from the container without publishing them to the host.
    /// </summary>
    public StringOrNumberList Expose { get; set; } = new();

    #endregion

    #region Extends

    /// <summary>
    /// Gets or sets the extends configuration, allowing this service to inherit from another.
    /// </summary>
    public ExtendsConfig? Extends { get; set; }

    #endregion

    #region External links

    /// <summary>
    /// Gets or sets the list of external links (links to containers outside this Compose project).
    /// </summary>
    public List<string> ExternalLinks { get; set; } = new();

    /// <summary>
    /// Gets or sets the extra hosts entries (<c>/etc/hosts</c> mappings) for the container.
    /// </summary>
    public HostsList ExtraHosts { get; set; } = new();

    #endregion

    #region Groups

    /// <summary>
    /// Gets or sets the supplementary groups the container process runs as.
    /// </summary>
    public List<string> GroupAdd { get; set; } = new();

    #endregion

    #region GPU (direct)

    /// <summary>
    /// Gets or sets the GPU device requests for the container.
    /// </summary>
    public List<DeviceRequest> Gpus { get; set; } = new();

    #endregion

    #region Host

    /// <summary>
    /// Gets or sets the hostname of the container.
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Gets or sets the health check configuration for the service.
    /// </summary>
    public HealthCheckConfig? HealthCheck { get; set; }

    #endregion

    #region Image

    /// <summary>
    /// Gets or sets the image to use for the service.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// Gets or sets whether to run an init process inside the container.
    /// A <see langword="null"/> value means the runtime default is used.
    /// </summary>
    public bool? Init { get; set; }

    #endregion

    #region IPC / Isolation

    /// <summary>
    /// Gets or sets the IPC namespace mode (<c>"host"</c>, <c>"private"</c>, <c>"shareable"</c>,
    /// or <c>"service:&lt;name&gt;"</c>).
    /// </summary>
    public string? Ipc { get; set; }

    /// <summary>
    /// Gets or sets the container isolation technology (Windows only).
    /// </summary>
    public string? Isolation { get; set; }

    #endregion

    #region Labels

    /// <summary>
    /// Gets or sets the labels applied to the container.
    /// </summary>
    public Labels Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of files to load labels from.
    /// </summary>
    public List<string> LabelFiles { get; set; } = new();

    #endregion

    #region Links

    /// <summary>
    /// Gets or sets the list of links to other services.
    /// </summary>
    public List<string> Links { get; set; } = new();

    #endregion

    #region Logging

    /// <summary>
    /// Gets or sets the logging configuration for the service.
    /// </summary>
    public LoggingConfig? Logging { get; set; }

    #endregion

    #region Memory resources

    /// <summary>
    /// Gets or sets the memory limit for the container.
    /// </summary>
    public UnitBytes MemLimit { get; set; }

    /// <summary>
    /// Gets or sets the memory reservation (soft limit) for the container.
    /// </summary>
    public UnitBytes MemReservation { get; set; }

    /// <summary>
    /// Gets or sets the total memory limit (memory + swap) for the container.
    /// Set to <c>-1</c> for unlimited swap.
    /// </summary>
    public UnitBytes MemSwapLimit { get; set; }

    /// <summary>
    /// Gets or sets the memory swappiness percentage (0â€“100).
    /// </summary>
    public int? MemSwappiness { get; set; }

    #endregion

    #region MAC

    /// <summary>
    /// Gets or sets the MAC address for the container.
    /// </summary>
    public string? MacAddress { get; set; }

    #endregion

    #region Network

    /// <summary>
    /// Gets or sets the network mode for the container
    /// (e.g. <c>"bridge"</c>, <c>"host"</c>, <c>"none"</c>, <c>"service:name"</c>).
    /// </summary>
    public string? NetworkMode { get; set; }

    /// <summary>
    /// Gets or sets the networks this service is connected to, along with per-network configuration.
    /// </summary>
    public Dictionary<string, ServiceNetworkConfig?> Networks { get; set; } = new();

    #endregion

    #region OOM

    /// <summary>
    /// Gets or sets whether to disable the OOM killer for the container.
    /// </summary>
    public bool OomKillDisable { get; set; }

    /// <summary>
    /// Gets or sets the OOM score adjustment (-1000 to 1000).
    /// </summary>
    public long OomScoreAdj { get; set; }

    #endregion

    #region PID

    /// <summary>
    /// Gets or sets the PID namespace mode (<c>"host"</c> or <c>"service:&lt;name&gt;"</c>).
    /// </summary>
    public string? Pid { get; set; }

    /// <summary>
    /// Gets or sets the PIDs limit for the container.
    /// </summary>
    public long PidsLimit { get; set; }

    #endregion

    #region Platform

    /// <summary>
    /// Gets or sets the target platform for the service (e.g. <c>"linux/amd64"</c>).
    /// </summary>
    public string? Platform { get; set; }

    #endregion

    #region Ports

    /// <summary>
    /// Gets or sets the list of port mappings for the service.
    /// </summary>
    public List<ServicePortConfig> Ports { get; set; } = new();

    #endregion

    #region Privileged / Pull / ReadOnly

    /// <summary>
    /// Gets or sets whether the container runs in privileged mode.
    /// </summary>
    public bool Privileged { get; set; }

    /// <summary>
    /// Gets or sets the pull policy for the image (see <see cref="PullPolicies"/>).
    /// </summary>
    public string? PullPolicy { get; set; }

    /// <summary>
    /// Gets or sets whether the container's root filesystem is read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

    #endregion

    #region Restart

    /// <summary>
    /// Gets or sets the restart policy for the container (see <see cref="RestartPolicies"/>).
    /// </summary>
    public string? Restart { get; set; }

    /// <summary>
    /// Gets or sets the OCI runtime to use for this container.
    /// </summary>
    public string? Runtime { get; set; }

    #endregion

    #region Scale

    /// <summary>
    /// Gets or sets the desired number of container instances for this service.
    /// A <see langword="null"/> value means the default (1) is used.
    /// </summary>
    public int? Scale { get; set; }

    #endregion

    #region Secrets

    /// <summary>
    /// Gets or sets the list of secrets to mount into this service.
    /// </summary>
    public List<ServiceSecretConfig> Secrets { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of security options for the container.
    /// </summary>
    public List<string> SecurityOpt { get; set; } = new();

    #endregion

    #region SHM

    /// <summary>
    /// Gets or sets the size of <c>/dev/shm</c> for the container.
    /// </summary>
    public UnitBytes ShmSize { get; set; }

    #endregion

    #region Stdio

    /// <summary>
    /// Gets or sets whether to keep stdin open even if not attached.
    /// </summary>
    public bool StdinOpen { get; set; }

    /// <summary>
    /// Gets or sets the grace period for stopping the container before it is forcefully killed.
    /// </summary>
    public Duration? StopGracePeriod { get; set; }

    /// <summary>
    /// Gets or sets the signal used to stop the container (e.g. <c>"SIGTERM"</c>).
    /// </summary>
    public string? StopSignal { get; set; }

    #endregion

    #region Storage

    /// <summary>
    /// Gets or sets storage driver options for the container.
    /// </summary>
    public Dictionary<string, string> StorageOpt { get; set; } = new();

    #endregion

    #region Sysctls

    /// <summary>
    /// Gets or sets kernel parameters to set in the container.
    /// </summary>
    public Mapping Sysctls { get; set; } = new();

    #endregion

    #region Tmpfs

    /// <summary>
    /// Gets or sets the list of tmpfs mounts.
    /// </summary>
    public StringList Tmpfs { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to allocate a pseudo-TTY.
    /// </summary>
    public bool Tty { get; set; }

    #endregion

    #region Ulimits

    /// <summary>
    /// Gets or sets the ulimit overrides for the container.
    /// </summary>
    public Dictionary<string, UlimitsConfig> Ulimits { get; set; } = new();

    #endregion

    #region User

    /// <summary>
    /// Gets or sets the username or UID (and optionally group/GID) to run the container process as.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Gets or sets the user namespace mode (<c>"host"</c> or empty for the default).
    /// </summary>
    public string? UserNSMode { get; set; }

    /// <summary>
    /// Gets or sets the UTS namespace mode (<c>"host"</c> or empty for the default).
    /// </summary>
    public string? Uts { get; set; }

    #endregion

    #region Volumes

    /// <summary>
    /// Gets or sets the volume driver to use (legacy).
    /// </summary>
    public string? VolumeDriver { get; set; }

    /// <summary>
    /// Gets or sets the list of volume mounts for the container.
    /// </summary>
    public List<ServiceVolumeConfig> Volumes { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of volume sources to mount from other services/containers.
    /// </summary>
    public List<string> VolumesFrom { get; set; } = new();

    #endregion

    #region Working directory

    /// <summary>
    /// Gets or sets the working directory inside the container.
    /// </summary>
    public string? WorkingDir { get; set; }

    #endregion

    #region Develop

    /// <summary>
    /// Gets or sets the develop/watch configuration for the service.
    /// </summary>
    public DevelopConfig? Develop { get; set; }

    #endregion

    #region Provider

    /// <summary>
    /// Gets or sets the cloud/IaC provider configuration for the service.
    /// </summary>
    public ServiceProviderConfig? Provider { get; set; }

    #endregion

    #region Models

    /// <summary>
    /// Gets or sets the AI/ML model configurations for the service.
    /// Maps model resource name to per-service model configuration.
    /// </summary>
    public Dictionary<string, ServiceModelConfig?> ServiceModels { get; set; } = new();

    #endregion

    #region Lifecycle hooks

    /// <summary>
    /// Gets or sets the list of hooks to run after the container starts.
    /// </summary>
    public List<ServiceHook> PostStart { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of hooks to run before the container stops.
    /// </summary>
    public List<ServiceHook> PreStop { get; set; } = new();

    #endregion

    #region Legacy fields

    /// <summary>
    /// Gets or sets the legacy <c>log_driver</c> field.
    /// This is a deprecated field that maps to <c>logging.driver</c>.
    /// </summary>
    public string? LogDriver { get; set; }

    /// <summary>
    /// Gets or sets the legacy <c>log_opt</c> field.
    /// This is a deprecated field that maps to <c>logging.options</c>.
    /// </summary>
    public Dictionary<string, string> LogOpt { get; set; } = new();

    /// <summary>
    /// Gets or sets the legacy <c>net</c> field.
    /// This is a deprecated field that maps to <c>network_mode</c>.
    /// </summary>
    public string? Net { get; set; }

    #endregion

    #region Runtime-only fields

    /// <summary>
    /// Gets or sets the custom labels injected by the Docker CLI at runtime.
    /// This field is not deserialized from YAML and not serialized (yaml:"-" in compose-go).
    /// </summary>
    public Labels CustomLabels { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to use the API socket for this service.
    /// </summary>
    public bool UseAPISocket { get; set; }

    #endregion

    #region Extensions

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();

    #endregion

    #region Methods

    /// <summary>
    /// Creates a deep copy of this <see cref="ServiceConfig"/>.
    /// All nested objects, lists, and dictionaries are recursively copied.
    /// Mirrors compose-go's code-generated <c>deepCopy</c> method.
    /// </summary>
    /// <returns>A new <see cref="ServiceConfig"/> that is a completely independent copy.</returns>
    public ServiceConfig DeepCopy() => DeepCopyHelper.CopyServiceConfig(this);

    /// <summary>
    /// Determines whether this service should be started for the given set of active profiles.
    /// A service with no profiles is always started. A service with profiles is started
    /// if any of its profiles are in the active set.
    /// </summary>
    /// <param name="profiles">The active profiles.</param>
    /// <returns><c>true</c> if this service matches the given profiles; otherwise, <c>false</c>.</returns>
    public bool HasProfile(IEnumerable<string> profiles)
    {
        if (profiles == null) throw new ArgumentNullException(nameof(profiles));

        // A service with no profiles is always active
        if (Profiles.Count == 0)
            return true;

        var activeSet = profiles is ISet<string> s
            ? s
            : new HashSet<string>(profiles, StringComparer.Ordinal);

        // Wildcard "*" activates all services
        if (activeSet.Contains("*"))
            return true;

        foreach (var profile in Profiles)
        {
            if (activeSet.Contains(profile))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the names of all services this service depends on.
    /// </summary>
    /// <returns>A list of dependency service names.</returns>
    public List<string> GetDependencies()
    {
        var deps = new List<string>(DependsOn.Count);
        foreach (var key in DependsOn.Keys)
        {
            deps.Add(key);
        }

        return deps;
    }

    /// <summary>
    /// Returns the effective scale for this service.
    /// Falls back to the deploy replicas if <see cref="Scale"/> is not set,
    /// and defaults to <c>1</c> if neither is set.
    /// </summary>
    /// <returns>The effective number of instances.</returns>
    public int GetScale()
    {
        if (Scale.HasValue)
            return Scale.Value;

        if (Deploy?.Replicas != null)
            return Deploy.Replicas.Value;

        return 1;
    }

    /// <summary>
    /// Sets the scale for this service. Also updates <see cref="DeployConfig.Replicas"/>
    /// if a deploy configuration exists.
    /// </summary>
    /// <param name="scale">The desired number of instances.</param>
    public void SetScale(int scale)
    {
        Scale = scale;

        if (Deploy != null)
        {
            Deploy.Replicas = scale;
        }
    }

    /// <summary>
    /// Returns the network names this service is connected to, sorted by
    /// <see cref="ServiceNetworkConfig.Priority"/> in descending order (highest priority first),
    /// then by name alphabetically for equal priorities.
    /// </summary>
    /// <returns>A list of network names ordered by priority.</returns>
    public List<string> NetworksByPriority()
    {
        var entries = new List<KeyValuePair<string, ServiceNetworkConfig?>>(Networks);

        entries.Sort((a, b) =>
        {
            var priorityA = a.Value?.Priority ?? 0;
            var priorityB = b.Value?.Priority ?? 0;

            // Higher priority first
            var cmp = priorityB.CompareTo(priorityA);
            if (cmp != 0)
                return cmp;

            // Then alphabetical by name
            return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
        });

        var result = new List<string>(entries.Count);
        foreach (var entry in entries)
        {
            result.Add(entry.Key);
        }

        return result;
    }

    #endregion
}

#endregion

#region Supporting types

/// <summary>
/// Represents the build configuration for a Compose service,
/// corresponding to the <c>build</c> section in a service definition.
/// </summary>
public class BuildConfig
{
    /// <summary>
    /// Gets or sets the path to the build context directory.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Gets or sets the path to the Dockerfile (relative to the context).
    /// </summary>
    public string? Dockerfile { get; set; }

    /// <summary>
    /// Gets or sets the inline Dockerfile content.
    /// </summary>
    public string? DockerfileInline { get; set; }

    /// <summary>
    /// Gets or sets the build arguments. A <see langword="null"/> value means
    /// the argument should be resolved from the environment.
    /// </summary>
    public Dictionary<string, string?> Args { get; set; } = new();

    /// <summary>
    /// Gets or sets the SSH agent socket or keys to expose to the build.
    /// Uses the strongly-typed <see cref="Types.SSHConfig"/> collection,
    /// matching compose-go's <c>SSHConfig</c> type.
    /// </summary>
    public SSHConfig SSH { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of external cache sources.
    /// </summary>
    public List<string> CacheFrom { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of cache export destinations.
    /// </summary>
    public List<string> CacheTo { get; set; } = new();

    /// <summary>
    /// Gets or sets additional build contexts (e.g. named contexts for multi-stage builds).
    /// Maps context name to context source, matching compose-go's <c>Mapping</c> type.
    /// </summary>
    public Mapping AdditionalContexts { get; set; } = new();

    /// <summary>
    /// Gets or sets the labels to apply to the built image.
    /// </summary>
    public Labels Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to disable the build cache.
    /// </summary>
    public bool NoCache { get; set; }

    /// <summary>
    /// Gets or sets whether to always attempt to pull a newer version of base images.
    /// </summary>
    public bool Pull { get; set; }

    /// <summary>
    /// Gets or sets the target build stage in a multi-stage Dockerfile.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Gets or sets the size of <c>/dev/shm</c> for the build container.
    /// </summary>
    public UnitBytes? ShmSize { get; set; }

    /// <summary>
    /// Gets or sets extra hosts entries for the build container.
    /// Uses the strongly-typed <see cref="Types.HostsList"/>, matching compose-go.
    /// </summary>
    public HostsList ExtraHosts { get; set; } = new();

    /// <summary>
    /// Gets or sets the isolation technology for the build (Windows only).
    /// </summary>
    public string? Isolation { get; set; }

    /// <summary>
    /// Gets or sets whether the build runs in privileged mode.
    /// </summary>
    public bool Privileged { get; set; }

    /// <summary>
    /// Gets or sets the tags to apply to the built image.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the target platforms for multi-platform builds.
    /// </summary>
    public List<string> Platforms { get; set; } = new();

    /// <summary>
    /// Gets or sets the network mode for the build container.
    /// </summary>
    public string? Network { get; set; }

    /// <summary>
    /// Gets or sets the secrets to expose to the build.
    /// </summary>
    public List<ServiceSecretConfig> Secrets { get; set; } = new();

    /// <summary>
    /// Gets or sets the build entitlements (e.g. <c>"network.host"</c>, <c>"security.insecure"</c>).
    /// </summary>
    public List<string> Entitlements { get; set; } = new();

    /// <summary>
    /// Gets or sets the provenance attestation mode for the build (e.g. <c>"max"</c>, <c>"min"</c>).
    /// </summary>
    public string? Provenance { get; set; }

    /// <summary>
    /// Gets or sets the SBOM (Software Bill of Materials) generation mode for the build.
    /// </summary>
    public string? SBOM { get; set; }

    /// <summary>
    /// Gets or sets the list of build stages to skip caching for.
    /// </summary>
    public List<string> NoCacheFilter { get; set; } = new();

    /// <summary>
    /// Gets or sets the ulimit overrides for the build container.
    /// </summary>
    public Dictionary<string, UlimitsConfig> Ulimits { get; set; } = new();

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents the block I/O configuration for a Compose service,
/// corresponding to the <c>blkio_config</c> section.
/// </summary>
public class BlkioConfig
{
    /// <summary>
    /// Gets or sets the I/O weight (relative to other containers, 10â€“1000).
    /// </summary>
    public ushort Weight { get; set; }

    /// <summary>
    /// Gets or sets per-device weight overrides.
    /// </summary>
    public List<WeightDevice> WeightDevice { get; set; } = new();

    /// <summary>
    /// Gets or sets per-device read bandwidth (bytes/sec) limits.
    /// </summary>
    public List<ThrottleDevice> DeviceReadBps { get; set; } = new();

    /// <summary>
    /// Gets or sets per-device read I/O operations per second limits.
    /// </summary>
    public List<ThrottleDevice> DeviceReadIOps { get; set; } = new();

    /// <summary>
    /// Gets or sets per-device write bandwidth (bytes/sec) limits.
    /// </summary>
    public List<ThrottleDevice> DeviceWriteBps { get; set; } = new();

    /// <summary>
    /// Gets or sets per-device write I/O operations per second limits.
    /// </summary>
    public List<ThrottleDevice> DeviceWriteIOps { get; set; } = new();

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a per-device weight override within <see cref="BlkioConfig"/>.
/// </summary>
public class WeightDevice
{
    /// <summary>
    /// Gets or sets the device path.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Gets or sets the weight for this device (10â€“1000).
    /// </summary>
    public ushort Weight { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a per-device I/O throttle configuration within <see cref="BlkioConfig"/>.
/// </summary>
public class ThrottleDevice
{
    /// <summary>
    /// Gets or sets the device path.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Gets or sets the rate limit (in bytes or I/O operations per second).
    /// </summary>
    public UnitBytes Rate { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a device mapping between the host and the container.
/// </summary>
public class DeviceMapping
{
    /// <summary>
    /// Gets or sets the device path on the host.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Gets or sets the device path in the container.
    /// </summary>
    public string Target { get; set; } = "";

    /// <summary>
    /// Gets or sets the cgroup permissions for the device (<c>"r"</c>, <c>"w"</c>, <c>"m"</c>,
    /// or combinations like <c>"rwm"</c>).
    /// </summary>
    public string? Permissions { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a credential specification for Windows containers.
/// </summary>
public class CredentialSpecConfig
{
    /// <summary>
    /// Gets or sets the name of the Docker config containing the credential spec.
    /// </summary>
    public string? Config { get; set; }

    /// <summary>
    /// Gets or sets the path to the credential spec file.
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// Gets or sets the registry entry for the credential spec.
    /// </summary>
    public string? Registry { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents the health check configuration for a Compose service,
/// corresponding to the <c>healthcheck</c> section.
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// Gets or sets the test command to run. The first element may be <c>"NONE"</c>,
    /// <c>"CMD"</c>, or <c>"CMD-SHELL"</c>.
    /// </summary>
    public List<string> Test { get; set; } = new();

    /// <summary>
    /// Gets or sets the time between health checks.
    /// </summary>
    public Duration? Interval { get; set; }

    /// <summary>
    /// Gets or sets the maximum time to wait for a health check to complete.
    /// </summary>
    public Duration? Timeout { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive failures needed to report unhealthy.
    /// Uses <c>ulong?</c> to match compose-go's <c>*uint64</c>.
    /// </summary>
    public ulong? Retries { get; set; }

    /// <summary>
    /// Gets or sets the start period: the time to wait before starting health checks
    /// after the container starts (failures during this period don't count).
    /// </summary>
    public Duration? StartPeriod { get; set; }

    /// <summary>
    /// Gets or sets the time between health checks during the start period.
    /// </summary>
    public Duration? StartInterval { get; set; }

    /// <summary>
    /// Gets or sets whether the health check is disabled.
    /// </summary>
    public bool Disable { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents the logging configuration for a Compose service,
/// corresponding to the <c>logging</c> section.
/// </summary>
public class LoggingConfig
{
    /// <summary>
    /// Gets or sets the logging driver name (e.g. <c>"json-file"</c>, <c>"syslog"</c>).
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Gets or sets driver-specific logging options.
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a ulimit configuration for a Compose service.
/// Use either <see cref="Single"/> for a single value, or <see cref="Soft"/>/<see cref="Hard"/>
/// for separate soft and hard limits.
/// </summary>
public class UlimitsConfig
{
    /// <summary>
    /// Gets or sets the single ulimit value (sets both soft and hard to the same value).
    /// </summary>
    public int Single { get; set; }

    /// <summary>
    /// Gets or sets the soft ulimit value.
    /// </summary>
    public int Soft { get; set; }

    /// <summary>
    /// Gets or sets the hard ulimit value.
    /// </summary>
    public int Hard { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents an environment file reference with optional configuration.
/// </summary>
public class EnvFile
{
    /// <summary>
    /// Gets or sets the path to the environment file.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the file is required. When <c>false</c>, a missing file is silently ignored.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Gets or sets the format of the environment file (e.g. <c>"raw"</c>).
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// A dictionary of service name to <see cref="ServiceDependency"/>,
/// representing the <c>depends_on</c> section for a service.
/// </summary>
public class DependsOnConfig : Dictionary<string, ServiceDependency>
{
    /// <summary>
    /// Initializes a new, empty <see cref="DependsOnConfig"/> instance.
    /// </summary>
    public DependsOnConfig() { }

    /// <summary>
    /// Initializes a new <see cref="DependsOnConfig"/> instance from an existing dictionary.
    /// </summary>
    /// <param name="source">The source dictionary.</param>
    public DependsOnConfig(IDictionary<string, ServiceDependency> source) : base(source) { }
}

/// <summary>
/// Represents a single service dependency with its condition and options.
/// </summary>
public class ServiceDependency
{
    /// <summary>
    /// Gets or sets the condition that must be met for the dependency to be considered satisfied.
    /// Defaults to <see cref="ServiceConditions.Started"/>.
    /// </summary>
    public string Condition { get; set; } = ServiceConditions.Started;

    /// <summary>
    /// Gets or sets whether the dependent service should be restarted when this dependency restarts.
    /// </summary>
    public bool Restart { get; set; }

    /// <summary>
    /// Gets or sets whether this dependency is required.
    /// When <c>false</c>, a missing dependency does not prevent the service from starting.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents the <c>extends</c> configuration, allowing a service to inherit
/// its configuration from another service (optionally in another file).
/// </summary>
public class ExtendsConfig
{
    /// <summary>
    /// Gets or sets the path to the Compose file containing the service to extend from.
    /// If <see langword="null"/>, the service is in the same file.
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// Gets or sets the name of the service to extend from.
    /// </summary>
    public string Service { get; set; } = "";
}

/// <summary>
/// Represents a lifecycle hook command to run at a specific point in the service lifecycle
/// (e.g. post-start, pre-stop).
/// </summary>
public class ServiceHook
{
    /// <summary>
    /// Gets or sets the command to execute.
    /// </summary>
    public ShellCommand? Command { get; set; }

    /// <summary>
    /// Gets or sets the user to run the hook command as.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Gets or sets whether the hook runs in privileged mode.
    /// </summary>
    public bool Privileged { get; set; }

    /// <summary>
    /// Gets or sets the working directory for the hook command.
    /// </summary>
    public string? WorkingDir { get; set; }

    /// <summary>
    /// Gets or sets the environment variables for the hook command.
    /// </summary>
    public MappingWithEquals Environment { get; set; } = new();

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents an include configuration for importing other Compose files.
/// </summary>
public class IncludeConfig
{
    /// <summary>
    /// Gets or sets the list of Compose file paths to include.
    /// </summary>
    public StringList Path { get; set; } = new();

    /// <summary>
    /// Gets or sets the project directory for resolving relative paths in included files.
    /// </summary>
    public string? ProjectDirectory { get; set; }

    /// <summary>
    /// Gets or sets the list of env files to load for the included Compose files.
    /// </summary>
    public StringList EnvFile { get; set; } = new();
}

#endregion
