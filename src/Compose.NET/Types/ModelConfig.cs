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

#region Models collection

/// <summary>
/// A dictionary of model name to <see cref="ModelConfig"/>,
/// representing the top-level <c>models</c> section in a Compose file.
/// </summary>
public class Models : Dictionary<string, ModelConfig>
{
    /// <summary>
    /// Initializes a new, empty <see cref="Models"/> instance.
    /// </summary>
    public Models() { }

    /// <summary>
    /// Initializes a new <see cref="Models"/> instance from an existing dictionary.
    /// </summary>
    /// <param name="source">The source dictionary to copy entries from.</param>
    public Models(IDictionary<string, ModelConfig> source) : base(source) { }
}

#endregion

#region ModelConfig

/// <summary>
/// Represents a top-level model configuration in a Compose file.
/// Used for AI/ML model definitions in the Compose specification.
/// Mirrors compose-go's <c>ModelConfig</c> struct.
/// </summary>
public class ModelConfig
{
    /// <summary>
    /// Gets or sets the name of the model resource.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the model identifier (e.g. a model name or URI).
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the context window size for the model.
    /// </summary>
    public int ContextSize { get; set; }

    /// <summary>
    /// Gets or sets runtime flags passed to the model runtime.
    /// </summary>
    public List<string> RuntimeFlags { get; set; } = new();

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

#endregion

#region ServiceModelConfig

/// <summary>
/// Represents the model configuration within a service, defining how the service
/// connects to a model resource. Mirrors compose-go's <c>ServiceModelConfig</c> struct.
/// </summary>
public class ServiceModelConfig
{
    /// <summary>
    /// Gets or sets the environment variable name for the model endpoint URL.
    /// </summary>
    public string? EndpointVariable { get; set; }

    /// <summary>
    /// Gets or sets the environment variable name for the model name.
    /// </summary>
    public string? ModelVariable { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

#endregion
