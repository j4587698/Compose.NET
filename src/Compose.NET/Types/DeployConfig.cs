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
/// Represents the deploy configuration for a Compose service,
/// corresponding to the <c>deploy</c> section in a Compose file.
/// </summary>
public class DeployConfig
{
    /// <summary>
    /// Gets or sets the scheduling mode: <c>"global"</c> or <c>"replicated"</c>.
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// Gets or sets the number of replicas when <see cref="Mode"/> is <c>"replicated"</c>.
    /// </summary>
    public int? Replicas { get; set; }

    /// <summary>
    /// Gets or sets the labels applied to the service when deployed.
    /// </summary>
    public Labels? Labels { get; set; }

    /// <summary>
    /// Gets or sets the configuration for how updates to the service should be applied.
    /// </summary>
    public UpdateConfig? UpdateConfig { get; set; }

    /// <summary>
    /// Gets or sets the configuration for how rollbacks to the service should be applied.
    /// </summary>
    public UpdateConfig? RollbackConfig { get; set; }

    /// <summary>
    /// Gets or sets the resource constraints (limits and reservations) for the service.
    /// </summary>
    public Resources Resources { get; set; } = new();

    /// <summary>
    /// Gets or sets the restart policy for the service.
    /// </summary>
    public RestartPolicy? RestartPolicy { get; set; }

    /// <summary>
    /// Gets or sets the placement constraints and preferences for the service.
    /// </summary>
    public Placement Placement { get; set; } = new();

    /// <summary>
    /// Gets or sets the endpoint mode: <c>"vip"</c> or <c>"dnsrr"</c>.
    /// </summary>
    public string? EndpointMode { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents the configuration for how service updates or rollbacks should be applied.
/// </summary>
public class UpdateConfig
{
    /// <summary>
    /// Gets or sets the number of containers to update simultaneously.
    /// A value of <c>0</c> means all containers are updated at once.
    /// </summary>
    public ulong? Parallelism { get; set; }

    /// <summary>
    /// Gets or sets the delay between updating each group of containers.
    /// </summary>
    public Duration? Delay { get; set; }

    /// <summary>
    /// Gets or sets the action to take on failure: <c>"pause"</c>, <c>"continue"</c>, or <c>"rollback"</c>.
    /// </summary>
    public string? FailureAction { get; set; }

    /// <summary>
    /// Gets or sets the duration after each task update to monitor for failure.
    /// </summary>
    public Duration? Monitor { get; set; }

    /// <summary>
    /// Gets or sets the failure rate to tolerate during an update, as a fraction between 0 and 1.
    /// </summary>
    public float MaxFailureRatio { get; set; }

    /// <summary>
    /// Gets or sets the order of operations during updates: <c>"stop-first"</c> or <c>"start-first"</c>.
    /// </summary>
    public string? Order { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents resource constraints for a Compose service, including limits and reservations.
/// </summary>
public class Resources
{
    /// <summary>
    /// Gets or sets the hard resource limits for the service.
    /// </summary>
    public Resource? Limits { get; set; }

    /// <summary>
    /// Gets or sets the resource reservations (minimum guaranteed resources) for the service.
    /// </summary>
    public Resource? Reservations { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a single resource specification (either a limit or a reservation).
/// </summary>
public class Resource
{
    /// <summary>
    /// Gets or sets the CPU limit/reservation as a fractional number of CPUs
    /// (e.g. <c>0.5</c> for half a CPU). Corresponds to the <c>cpus</c> field in Compose.
    /// Despite the name, stores a CPU fraction (not nanoseconds), matching compose-go's type.
    /// </summary>
    public NanoCPUs NanoCPUs { get; set; }

    /// <summary>
    /// Gets or sets the memory limit/reservation in bytes.
    /// Corresponds to the <c>memory</c> field in Compose.
    /// </summary>
    public UnitBytes MemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the PIDs limit for the service.
    /// </summary>
    public long Pids { get; set; }

    /// <summary>
    /// Gets or sets the list of device requests (e.g. GPUs).
    /// </summary>
    public List<DeviceRequest> Devices { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of generic resources.
    /// </summary>
    public List<GenericResource> GenericResources { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a device request for a Compose service (e.g. GPU access).
/// </summary>
public class DeviceRequest
{
    /// <summary>
    /// Gets or sets the list of required device capabilities (e.g. <c>["gpu"]</c>).
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of devices to request. A value of <c>-1</c> requests all available devices.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the specific device IDs to request.
    /// </summary>
    public List<string> DeviceIDs { get; set; } = new();

    /// <summary>
    /// Gets or sets the device driver to use.
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Gets or sets driver-specific options.
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a generic resource specification.
/// </summary>
public class GenericResource
{
    /// <summary>
    /// Gets or sets the discrete resource specification.
    /// </summary>
    public DiscreteGenericResource? DiscreteResourceSpec { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a discrete generic resource with a kind and value
/// (e.g. <c>kind: "gpu", value: 2</c>).
/// </summary>
public class DiscreteGenericResource
{
    /// <summary>
    /// Gets or sets the kind of the discrete resource (e.g. <c>"gpu"</c>).
    /// </summary>
    public string Kind { get; set; } = "";

    /// <summary>
    /// Gets or sets the quantity of the discrete resource.
    /// </summary>
    public long Value { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents the restart policy for a deployed Compose service.
/// </summary>
public class RestartPolicy
{
    /// <summary>
    /// Gets or sets the restart condition: <c>"none"</c>, <c>"on-failure"</c>, or <c>"any"</c>.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Gets or sets the delay between restart attempts.
    /// </summary>
    public Duration? Delay { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of restart attempts.
    /// </summary>
    public ulong? MaxAttempts { get; set; }

    /// <summary>
    /// Gets or sets the time window used to evaluate the restart policy.
    /// </summary>
    public Duration? Window { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents placement constraints and preferences for a deployed Compose service.
/// </summary>
public class Placement
{
    /// <summary>
    /// Gets or sets the list of placement constraint expressions
    /// (e.g. <c>"node.role == manager"</c>).
    /// </summary>
    public List<string> Constraints { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of placement preferences for spreading tasks across nodes.
    /// </summary>
    public List<PlacementPreferences> Preferences { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum number of replicas per node.
    /// </summary>
    public ulong MaxReplicas { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a placement preference for spreading service tasks across nodes.
/// </summary>
public class PlacementPreferences
{
    /// <summary>
    /// Gets or sets the spread strategy value (e.g. <c>"node.labels.zone"</c>).
    /// </summary>
    public string? Spread { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}
