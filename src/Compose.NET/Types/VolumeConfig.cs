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
/// Represents the top-level volume configuration for a Compose project,
/// corresponding to an entry in the <c>volumes</c> section of a Compose file.
/// </summary>
public class VolumeConfig
{
    /// <summary>
    /// Gets or sets the custom name for the volume.
    /// When set, this is used as the actual volume name instead of the key in the <c>volumes</c> mapping.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the volume driver to use (e.g. <c>"local"</c>, <c>"nfs"</c>).
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Gets or sets the driver-specific options as key-value pairs.
    /// </summary>
    public Dictionary<string, string> DriverOpts { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this volume is externally managed
    /// (i.e. not created by Compose).
    /// </summary>
    public bool External { get; set; }

    /// <summary>
    /// Gets or sets the labels applied to this volume.
    /// </summary>
    public Labels Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom labels injected by the Docker CLI at runtime.
    /// This field is not deserialized from YAML and not serialized (yaml:"-" in compose-go).
    /// </summary>
    public Labels CustomLabels { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents the <c>volumes</c> section of a Compose file as a dictionary
/// mapping volume names to their <see cref="VolumeConfig"/>.
/// </summary>
public class Volumes : Dictionary<string, VolumeConfig> { }
