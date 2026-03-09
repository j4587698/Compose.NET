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

namespace Compose.NET.Consts;

/// <summary>
/// Standard Docker Compose labels used to identify resources.
/// </summary>
public static class ComposeLabels
{
    /// <summary>The Compose project name.</summary>
    public const string Project = "com.docker.compose.project";

    /// <summary>The Compose service name.</summary>
    public const string Service = "com.docker.compose.service";

    /// <summary>The Compose version that created the resource.</summary>
    public const string Version = "com.docker.compose.version";

    /// <summary>Hash of the service configuration, used to detect changes.</summary>
    public const string ConfigHash = "com.docker.compose.config-hash";

    /// <summary>The container number within a scaled service.</summary>
    public const string ContainerNumber = "com.docker.compose.container-number";

    /// <summary>Indicates whether the container is a one-off run.</summary>
    public const string OneOff = "com.docker.compose.oneoff";

    /// <summary>The Compose config file paths used to create the project.</summary>
    public const string ProjectConfigFiles = "com.docker.compose.project.config_files";

    /// <summary>The working directory of the Compose project.</summary>
    public const string ProjectWorkingDir = "com.docker.compose.project.working_dir";

    /// <summary>The environment file used by the Compose project.</summary>
    public const string ProjectEnvironmentFile = "com.docker.compose.project.environment_file";

    /// <summary>The Compose network name.</summary>
    public const string Network = "com.docker.compose.network";

    /// <summary>The Compose volume name.</summary>
    public const string Volume = "com.docker.compose.volume";

    /// <summary>A unique slug identifying the resource instance.</summary>
    public const string Slug = "com.docker.compose.slug";

    /// <summary>The image used by the service.</summary>
    public const string Image = "com.docker.compose.image";

    /// <summary>Indicates whether service dependencies have been resolved.</summary>
    public const string DependenciesResolved = "com.docker.compose.dependencies.resolved";

    /// <summary>Indicates a container replacement operation.</summary>
    public const string Replace = "com.docker.compose.replace";
}
