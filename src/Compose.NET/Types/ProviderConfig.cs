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

#region ServiceProviderConfig

/// <summary>
/// Represents the <c>provider</c> section of a Compose service,
/// enabling cloud/IaC provider integration for service creation.
/// Mirrors compose-go's <c>ServiceProviderConfig</c> struct.
/// </summary>
public class ServiceProviderConfig
{
    /// <summary>
    /// Gets or sets the provider type (e.g. <c>"aws"</c>, <c>"terraform"</c>).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the provider options. Each option key may have multiple values.
    /// </summary>
    public Dictionary<string, List<string>> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

#endregion
