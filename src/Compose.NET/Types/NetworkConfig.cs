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
/// Networks is a dictionary of network name to <see cref="NetworkConfig"/>.
/// </summary>
public class Networks : Dictionary<string, NetworkConfig> { }

/// <summary>
/// Represents a top-level network configuration in a Compose file,
/// corresponding to an entry under the <c>networks</c> section.
/// </summary>
public class NetworkConfig
{
    /// <summary>
    /// Gets or sets the actual name of the network (as seen by the container runtime).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the network driver to use (e.g. <c>"bridge"</c>, <c>"overlay"</c>).
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Gets or sets driver-specific options as key-value pairs.
    /// </summary>
    public Dictionary<string, string> DriverOpts { get; set; } = new();

    /// <summary>
    /// Gets or sets the IPAM (IP Address Management) configuration for this network.
    /// </summary>
    public IPAMConfig Ipam { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this network is externally managed
    /// (i.e. not created by Compose).
    /// </summary>
    public bool External { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the network is internal
    /// (i.e. does not provide external connectivity).
    /// </summary>
    public bool Internal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether standalone containers can attach to this network.
    /// </summary>
    public bool Attachable { get; set; }

    /// <summary>
    /// Gets or sets the labels applied to the network.
    /// </summary>
    public Labels Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom labels injected by the Docker CLI at runtime.
    /// This field is not deserialized from YAML and not serialized (yaml:"-" in compose-go).
    /// </summary>
    public Labels CustomLabels { get; set; } = new();

    /// <summary>
    /// Gets or sets whether IPv4 networking is enabled on this network.
    /// A <see langword="null"/> value means the runtime default is used.
    /// </summary>
    public bool? EnableIPv4 { get; set; }

    /// <summary>
    /// Gets or sets whether IPv6 networking is enabled on this network.
    /// A <see langword="null"/> value means the runtime default is used.
    /// </summary>
    public bool? EnableIPv6 { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents per-service network configuration,
/// corresponding to entries under a service's <c>networks</c> section.
/// </summary>
public class ServiceNetworkConfig
{
    /// <summary>
    /// Gets or sets the list of hostname aliases for this service on the network.
    /// </summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>
    /// Gets or sets driver-specific options for this service's network attachment.
    /// </summary>
    public Dictionary<string, string> DriverOpts { get; set; } = new();

    /// <summary>
    /// Gets or sets the gateway priority for this service on the network.
    /// </summary>
    public int GatewayPriority { get; set; }

    /// <summary>
    /// Gets or sets the interface name to use for this network attachment.
    /// </summary>
    public string? InterfaceName { get; set; }

    /// <summary>
    /// Gets or sets the static IPv4 address assigned to this service on the network.
    /// </summary>
    public string? Ipv4Address { get; set; }

    /// <summary>
    /// Gets or sets the static IPv6 address assigned to this service on the network.
    /// </summary>
    public string? Ipv6Address { get; set; }

    /// <summary>
    /// Gets or sets the list of link-local IP addresses for this service on the network.
    /// </summary>
    public List<string> LinkLocalIPs { get; set; } = new();

    /// <summary>
    /// Gets or sets the MAC address assigned to this service on the network.
    /// </summary>
    public string? MacAddress { get; set; }

    /// <summary>
    /// Gets or sets the priority of this network for the service.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents the IPAM (IP Address Management) configuration for a network.
/// </summary>
public class IPAMConfig
{
    /// <summary>
    /// Gets or sets the IPAM driver to use.
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Gets or sets the list of IPAM pool configurations.
    /// </summary>
    public List<IPAMPool> Config { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a single IPAM pool configuration within an <see cref="IPAMConfig"/>.
/// </summary>
public class IPAMPool
{
    /// <summary>
    /// Gets or sets the subnet in CIDR format (e.g. <c>"172.28.0.0/16"</c>).
    /// </summary>
    public string? Subnet { get; set; }

    /// <summary>
    /// Gets or sets the gateway address for this pool.
    /// </summary>
    public string? Gateway { get; set; }

    /// <summary>
    /// Gets or sets the allocatable IP range within the subnet in CIDR format.
    /// </summary>
    public string? IPRange { get; set; }

    /// <summary>
    /// Gets or sets the auxiliary addresses for this pool, mapping hostnames to IP addresses.
    /// </summary>
    public Mapping AuxiliaryAddresses { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}
