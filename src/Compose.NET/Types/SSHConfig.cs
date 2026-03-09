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
/// Represents an SSH key configuration for build contexts.
/// Maps to compose-go's <c>SSHKey</c> struct with <c>ID</c> and <c>Path</c> fields.
/// </summary>
public class SSHKey
{
    /// <summary>
    /// Gets or sets the identifier for this SSH key (e.g. <c>"default"</c>, <c>"mykey"</c>).
    /// </summary>
    public string ID { get; set; } = "";

    /// <summary>
    /// Gets or sets the file path to the SSH key.
    /// An empty string means the default SSH agent socket.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Returns the string representation in the format <c>"id=path"</c> or just <c>"id"</c>
    /// if the path is empty.
    /// </summary>
    public override string ToString()
    {
        if (string.IsNullOrEmpty(Path))
            return ID;
        return $"{ID}={Path}";
    }

    /// <summary>
    /// Parses a string like <c>"id=path"</c> or <c>"id"</c> into an <see cref="SSHKey"/>.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>A new <see cref="SSHKey"/> instance.</returns>
    public static SSHKey Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
            return new SSHKey();

        var eqIndex = value.IndexOf('=');
        if (eqIndex < 0)
        {
            return new SSHKey { ID = value };
        }

        return new SSHKey
        {
            ID = value.Substring(0, eqIndex),
            Path = value.Substring(eqIndex + 1)
        };
    }
}

/// <summary>
/// Represents a collection of SSH key configurations for build contexts.
/// Maps to compose-go's <c>type SSHConfig []SSHKey</c> with its
/// <c>Get(id)</c> lookup and <c>DecodeMapstructure</c> methods.
/// </summary>
public class SSHConfig : List<SSHKey>
{
    /// <summary>
    /// Initializes a new, empty <see cref="SSHConfig"/> instance.
    /// </summary>
    public SSHConfig() { }

    /// <summary>
    /// Initializes a new <see cref="SSHConfig"/> from an existing collection.
    /// </summary>
    /// <param name="collection">The collection to copy elements from.</param>
    public SSHConfig(IEnumerable<SSHKey> collection) : base(collection) { }

    /// <summary>
    /// Looks up an SSH key by its <see cref="SSHKey.ID"/>.
    /// </summary>
    /// <param name="id">The ID to search for.</param>
    /// <returns>The matching <see cref="SSHKey"/>, or <c>null</c> if not found.</returns>
    public SSHKey? Get(string id)
    {
        foreach (var key in this)
        {
            if (string.Equals(key.ID, id, StringComparison.Ordinal))
                return key;
        }

        return null;
    }

    /// <summary>
    /// Parses SSH configuration from a dictionary (map form) where keys are IDs
    /// and values are paths (or null for default agent).
    /// This handles the canonical (post-transform) form: <c>{"default": null, "mykey": "/path"}</c>.
    /// </summary>
    /// <param name="dict">The dictionary to parse.</param>
    /// <returns>A new <see cref="SSHConfig"/>.</returns>
    public static SSHConfig FromDictionary(Dictionary<string, object?> dict)
    {
        var config = new SSHConfig();
        foreach (var kvp in dict)
        {
            config.Add(new SSHKey
            {
                ID = kvp.Key,
                Path = kvp.Value?.ToString() ?? ""
            });
        }

        return config;
    }

    /// <summary>
    /// Parses SSH configuration from a list of strings in the format <c>"id=path"</c> or <c>"id"</c>.
    /// </summary>
    /// <param name="items">The list of strings to parse.</param>
    /// <returns>A new <see cref="SSHConfig"/>.</returns>
    public static SSHConfig FromStringList(IEnumerable<string> items)
    {
        var config = new SSHConfig();
        foreach (var item in items)
        {
            config.Add(SSHKey.Parse(item));
        }

        return config;
    }
}
