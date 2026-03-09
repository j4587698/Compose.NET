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

using System.Collections.Generic;
using Compose.NET.Types;

namespace Compose.NET.Loader
{
    /// <summary>
    /// The main input to the loader, containing all details needed to load a Compose project.
    /// Mirrors compose-go's types.ConfigDetails.
    /// </summary>
    public class ConfigDetails
    {
        /// <summary>
        /// Gets or sets the project working directory.
        /// </summary>
        public string WorkingDir { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets one or more compose files to load.
        /// </summary>
        public List<ConfigFile> ConfigFiles { get; set; } = new List<ConfigFile>();

        /// <summary>
        /// Gets or sets the host environment variables.
        /// </summary>
        public Dictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the .env file paths.
        /// </summary>
        public List<string> EnvFiles { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a single compose file with its path and optional pre-loaded content.
    /// Mirrors compose-go's types.ConfigFile.
    /// </summary>
    public class ConfigFile
    {
        /// <summary>
        /// Gets or sets the file path.
        /// </summary>
        public string Filename { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the raw YAML content. Null means read from <see cref="Filename"/>.
        /// </summary>
        public byte[]? Content { get; set; }

        /// <summary>
        /// Gets or sets an already-parsed config dictionary, if available.
        /// </summary>
        public Dictionary<string, object?>? Config { get; set; }
    }

    /// <summary>
    /// Options for configuring the loader pipeline.
    /// Mirrors compose-go's types.Options (loader options).
    /// </summary>
    public class LoadOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to skip schema validation.
        /// </summary>
        public bool SkipValidation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip variable interpolation.
        /// </summary>
        public bool SkipInterpolation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip normalization.
        /// </summary>
        public bool SkipNormalization { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to resolve relative paths.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool ResolvePaths { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to convert Windows-style paths.
        /// </summary>
        public bool ConvertWindowsPaths { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip the consistency check.
        /// </summary>
        public bool SkipConsistencyCheck { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip processing of extends directives.
        /// </summary>
        public bool SkipExtends { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip processing of include directives.
        /// </summary>
        public bool SkipInclude { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip environment variable resolution.
        /// </summary>
        public bool SkipResolveEnvironment { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip applying default values.
        /// </summary>
        public bool SkipDefaultValues { get; set; }

        /// <summary>
        /// Gets or sets the project name override.
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// Gets or sets the active profiles.
        /// </summary>
        public List<string>? Profiles { get; set; }
    }

    /// <summary>
    /// The output of the loader, containing the loaded project and any warnings.
    /// </summary>
    public class LoadResult
    {
        /// <summary>
        /// Gets or sets the loaded project.
        /// </summary>
        public Project Project { get; set; } = new Project();

        /// <summary>
        /// Gets or sets any warnings generated during loading.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
