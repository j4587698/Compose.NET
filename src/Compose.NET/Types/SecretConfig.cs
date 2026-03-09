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
/// Base type for file-based objects used by both secrets and configs
/// at the top level of a Compose file.
/// </summary>
public class FileObjectConfig
{
    /// <summary>
    /// Gets or sets the name of the secret/config object as it appears in the platform.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the path to the file whose contents populate the secret/config.
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// Gets or sets the name of an environment variable whose value populates the secret/config.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the inline content of the secret/config.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the secret/config is externally managed
    /// (i.e. already exists in the platform and is not created by Compose).
    /// </summary>
    public bool External { get; set; }

    /// <summary>
    /// Gets or sets the labels associated with this secret/config.
    /// </summary>
    public Labels Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets the driver to use for this secret/config.
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Gets or sets driver-specific options.
    /// </summary>
    public Dictionary<string, string> DriverOpts { get; set; } = new();

    /// <summary>
    /// Gets or sets the template driver to use for rendering the secret/config content.
    /// </summary>
    public string? TemplateDriver { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a top-level secret configuration in a Compose file.
/// </summary>
public class SecretConfig : FileObjectConfig { }

/// <summary>
/// Represents a top-level config object configuration in a Compose file.
/// </summary>
public class ConfigObjConfig : FileObjectConfig { }

/// <summary>
/// Represents a per-service secret reference that controls how a secret
/// is mounted into a service container.
/// </summary>
public class ServiceSecretConfig
{
    /// <summary>
    /// Gets or sets the name of the secret as defined in the top-level <c>secrets</c> section.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Gets or sets the path within the container where the secret is mounted.
    /// Defaults to <c>/run/secrets/{Source}</c> if not specified.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Gets or sets the UID that owns the mounted secret file.
    /// </summary>
    public string? UID { get; set; }

    /// <summary>
    /// Gets or sets the GID that owns the mounted secret file.
    /// </summary>
    public string? GID { get; set; }

    /// <summary>
    /// Gets or sets the octal file mode for the mounted secret file.
    /// </summary>
    public long? Mode { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// Represents a per-service config reference that controls how a config object
/// is mounted into a service container.
/// </summary>
public class ServiceConfigObjConfig
{
    /// <summary>
    /// Gets or sets the name of the config as defined in the top-level <c>configs</c> section.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Gets or sets the path within the container where the config is mounted.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Gets or sets the UID that owns the mounted config file.
    /// </summary>
    public string? UID { get; set; }

    /// <summary>
    /// Gets or sets the GID that owns the mounted config file.
    /// </summary>
    public string? GID { get; set; }

    /// <summary>
    /// Gets or sets the octal file mode for the mounted config file.
    /// </summary>
    public long? Mode { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

/// <summary>
/// A dictionary of secret name to <see cref="SecretConfig"/>,
/// representing the top-level <c>secrets</c> section in a Compose file.
/// </summary>
public class Secrets : Dictionary<string, SecretConfig> { }

/// <summary>
/// A dictionary of config name to <see cref="ConfigObjConfig"/>,
/// representing the top-level <c>configs</c> section in a Compose file.
/// </summary>
public class Configs : Dictionary<string, ConfigObjConfig> { }
