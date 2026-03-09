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

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Compose.NET.Types
{
    /// <summary>
    /// Constants for volume mount types, matching compose-go's volume type constants.
    /// </summary>
    public static class VolumeTypes
    {
        /// <summary>Bind mount from host path.</summary>
        public const string Bind = "bind";

        /// <summary>Named or anonymous volume.</summary>
        public const string Volume = "volume";

        /// <summary>Temporary filesystem mount.</summary>
        public const string Tmpfs = "tmpfs";

        /// <summary>Named pipe mount (Windows).</summary>
        public const string NamedPipe = "npipe";

        /// <summary>Cluster volume mount.</summary>
        public const string Cluster = "cluster";

        /// <summary>Image volume mount.</summary>
        public const string Image = "image";
    }

    /// <summary>
    /// Represents a volume mount configuration for a Compose service.
    /// Corresponds to the <c>volumes</c> entries in a service definition.
    /// </summary>
    public class ServiceVolumeConfig
    {
        /// <summary>
        /// Gets or sets the type of the volume mount.
        /// One of: <c>"bind"</c>, <c>"volume"</c>, <c>"tmpfs"</c>, <c>"npipe"</c>, <c>"cluster"</c>, <c>"image"</c>.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source path or volume name. May be <c>null</c> for anonymous volumes and tmpfs mounts.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the target (container) path for the mount.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the mount is read-only.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the consistency requirement. May be <c>null</c>.
        /// </summary>
        public string? Consistency { get; set; }

        /// <summary>
        /// Gets or sets the bind-specific options. Only applicable when <see cref="Type"/> is <c>"bind"</c>.
        /// </summary>
        public ServiceVolumeBind? Bind { get; set; }

        /// <summary>
        /// Gets or sets the volume-specific options. Only applicable when <see cref="Type"/> is <c>"volume"</c>.
        /// </summary>
        public ServiceVolumeVolume? Volume { get; set; }

        /// <summary>
        /// Gets or sets the tmpfs-specific options. Only applicable when <see cref="Type"/> is <c>"tmpfs"</c>.
        /// </summary>
        public ServiceVolumeTmpfs? Tmpfs { get; set; }

        /// <summary>
        /// Gets or sets the image-specific options. Only applicable when <see cref="Type"/> is <c>"image"</c>.
        /// </summary>
        public ServiceVolumeImage? Image { get; set; }

        /// <summary>
        /// Gets or sets the extension fields (<c>x-*</c>) associated with this volume config.
        /// </summary>
        public Extensions Extensions { get; set; } = new Extensions();
    }

    /// <summary>
    /// Bind-mount specific configuration options for a <see cref="ServiceVolumeConfig"/>.
    /// </summary>
    public class ServiceVolumeBind
    {
        /// <summary>
        /// Gets or sets the SELinux relabeling option (<c>"z"</c> for shared, <c>"Z"</c> for private).
        /// </summary>
        public string? SELinux { get; set; }

        /// <summary>
        /// Gets or sets the bind propagation mode (e.g. <c>"rprivate"</c>, <c>"shared"</c>).
        /// </summary>
        public string? Propagation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to create the host path if it does not exist.
        /// Defaults to <c>true</c>, matching compose-go's OptOut behavior.
        /// </summary>
        public bool CreateHostPath { get; set; } = true;

        /// <summary>
        /// Gets or sets the recursive mount option.
        /// </summary>
        public string? Recursive { get; set; }

        /// <summary>
        /// Gets or sets the extension fields (<c>x-*</c>) associated with this bind config.
        /// </summary>
        public Extensions Extensions { get; set; } = new Extensions();
    }

    /// <summary>
    /// Volume-specific configuration options for a <see cref="ServiceVolumeConfig"/>.
    /// </summary>
    public class ServiceVolumeVolume
    {
        /// <summary>
        /// Gets or sets the labels to apply to the volume.
        /// </summary>
        public Labels? Labels { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether data should not be copied from the container
        /// when the volume is first created.
        /// </summary>
        public bool NoCopy { get; set; }

        /// <summary>
        /// Gets or sets the subpath within the volume to mount.
        /// </summary>
        public string? Subpath { get; set; }

        /// <summary>
        /// Gets or sets the extension fields (<c>x-*</c>) associated with this volume config.
        /// </summary>
        public Extensions Extensions { get; set; } = new Extensions();
    }

    /// <summary>
    /// Tmpfs-specific configuration options for a <see cref="ServiceVolumeConfig"/>.
    /// </summary>
    public class ServiceVolumeTmpfs
    {
        /// <summary>
        /// Gets or sets the size of the tmpfs mount.
        /// Supports human-readable strings like <c>"64m"</c>, <c>"1gb"</c>.
        /// </summary>
        public UnitBytes Size { get; set; }

        /// <summary>
        /// Gets or sets the file mode (permission bits) for the tmpfs mount.
        /// </summary>
        public uint Mode { get; set; }

        /// <summary>
        /// Gets or sets the extension fields (<c>x-*</c>) associated with this tmpfs config.
        /// </summary>
        public Extensions Extensions { get; set; } = new Extensions();
    }

    /// <summary>
    /// Image-specific configuration options for a <see cref="ServiceVolumeConfig"/>.
    /// Corresponds to compose-go's <c>ServiceVolumeImage</c> type.
    /// </summary>
    public class ServiceVolumeImage
    {
        /// <summary>
        /// Gets or sets the subpath within the image to mount.
        /// </summary>
        public string? SubPath { get; set; }

        /// <summary>
        /// Gets or sets the extension fields (<c>x-*</c>) associated with this image volume config.
        /// </summary>
        public Extensions Extensions { get; set; } = new Extensions();
    }

    /// <summary>
    /// Parses short-syntax volume mount strings into <see cref="ServiceVolumeConfig"/> instances.
    /// Handles bind mounts, named volumes, anonymous volumes, and named pipes.
    /// </summary>
    public static class VolumeParser
    {
        private static readonly HashSet<string> ValidOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            "ro", "rw",
            "z", "Z",
            "nocopy",
            "rprivate", "private",
            "rshared", "shared",
            "rslave", "slave"
        };

        private static readonly HashSet<string> PropagationOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            "rprivate", "private",
            "rshared", "shared",
            "rslave", "slave"
        };

        /// <summary>
        /// Parses a short-syntax volume mount string into a <see cref="ServiceVolumeConfig"/>.
        /// <para>
        /// Supported formats:
        /// <list type="bullet">
        ///   <item><c>"/target"</c> â€” anonymous volume</item>
        ///   <item><c>"name:/target"</c> â€” named volume</item>
        ///   <item><c>"./host:/target"</c> or <c>"/host:/target"</c> â€” bind mount</item>
        ///   <item><c>"source:/target:ro"</c> â€” read-only mount</item>
        ///   <item><c>"source:/target:rw,z"</c> â€” read-write with SELinux shared label</item>
        ///   <item><c>"C:\path:/target"</c> â€” Windows host path bind mount</item>
        ///   <item><c>"\\.\pipe\name:/target"</c> â€” Windows named pipe</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="value">The volume mount string to parse.</param>
        /// <returns>A <see cref="ServiceVolumeConfig"/> representing the parsed volume mount.</returns>
        /// <exception cref="ArgumentException"><paramref name="value"/> is null or empty.</exception>
        /// <exception cref="FormatException">The volume string is not in a valid format.</exception>
        public static ServiceVolumeConfig ParseVolume(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Volume specification cannot be empty.", nameof(value));

            var config = new ServiceVolumeConfig();

            // Handle Windows named pipes: \\.\pipe\name or //./pipe/name
            if (IsNamedPipe(value))
            {
                return ParseNamedPipe(value);
            }

            // Split into parts, respecting Windows drive letters
            var parts = SplitVolumeParts(value);

            switch (parts.Count)
            {
                case 1:
                    // "/target" -> anonymous volume
                    config.Target = parts[0];
                    config.Type = VolumeTypes.Volume;
                    break;

                case 2:
                    // "source:/target"
                    config.Source = parts[0];
                    config.Target = parts[1];
                    config.Type = DetectVolumeType(config.Source);
                    break;

                case 3:
                    // "source:/target:options"
                    config.Source = parts[0];
                    config.Target = parts[1];
                    config.Type = DetectVolumeType(config.Source);
                    ParseOptions(config, parts[2]);
                    break;

                default:
                    throw new FormatException($"Invalid volume specification: '{value}'");
            }

            return config;
        }

        /// <summary>
        /// Determines whether the specified volume string represents a Windows named pipe.
        /// </summary>
        private static bool IsNamedPipe(string value)
        {
            // Windows named pipes: \\.\pipe\... or //./pipe/...
            return value.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("//./pipe/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a Windows named pipe volume specification.
        /// </summary>
        private static ServiceVolumeConfig ParseNamedPipe(string value)
        {
            // Find the separator between source pipe and target
            // Named pipes look like \\.\pipe\name:/target[:options]
            // We need to find the colon that separates the pipe path from the target.
            // The pipe path itself does not contain colons after the initial pattern.

            var pipePrefix = value.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase)
                ? @"\\.\pipe\"
                : "//./pipe/";

            var remainder = value.Substring(pipePrefix.Length);
            var colonIndex = remainder.IndexOf(':');

            var config = new ServiceVolumeConfig
            {
                Type = VolumeTypes.NamedPipe
            };

            if (colonIndex < 0)
            {
                // Only pipe path, use as both source and target
                config.Source = value;
                config.Target = value;
            }
            else
            {
                config.Source = pipePrefix + remainder.Substring(0, colonIndex);
                var afterSource = remainder.Substring(colonIndex + 1);

                // afterSource may be "target" or "target:options"
                var targetColonIndex = afterSource.IndexOf(':');
                if (targetColonIndex < 0)
                {
                    config.Target = afterSource;
                }
                else
                {
                    config.Target = afterSource.Substring(0, targetColonIndex);
                    ParseOptions(config, afterSource.Substring(targetColonIndex + 1));
                }
            }

            return config;
        }

        /// <summary>
        /// Splits a volume specification string into its constituent parts (source, target, options),
        /// correctly handling Windows drive letter paths like <c>C:\path</c>.
        /// </summary>
        private static List<string> SplitVolumeParts(string value)
        {
            var parts = new List<string>();
            var current = 0;

            while (current < value.Length)
            {
                var colonIndex = value.IndexOf(':', current);

                if (colonIndex < 0)
                {
                    // No more colons, rest is the last part
                    parts.Add(value.Substring(current));
                    break;
                }

                // Check if this colon is part of a Windows drive letter (e.g., C:\)
                if (IsWindowsDriveLetter(value, colonIndex))
                {
                    // Skip this colon, look for the next one
                    var nextColon = value.IndexOf(':', colonIndex + 1);
                    if (nextColon < 0)
                    {
                        // No more colons, rest is the last part
                        parts.Add(value.Substring(current));
                        break;
                    }
                    else
                    {
                        parts.Add(value.Substring(current, nextColon - current));
                        current = nextColon + 1;
                    }
                }
                else
                {
                    parts.Add(value.Substring(current, colonIndex - current));
                    current = colonIndex + 1;
                }
            }

            return parts;
        }

        /// <summary>
        /// Determines whether the colon at the specified index is part of a Windows drive letter
        /// pattern (a single letter followed by <c>:</c> and then <c>\</c> or <c>/</c>).
        /// </summary>
        private static bool IsWindowsDriveLetter(string value, int colonIndex)
        {
            // A Windows drive letter is: single alpha char, then ':', then '\' or '/'
            // The alpha char must be at the start of a "part" (i.e., colonIndex == 1 or preceded by a colon)
            if (colonIndex < 1)
                return false;

            var letter = value[colonIndex - 1];
            if (!((letter >= 'a' && letter <= 'z') || (letter >= 'A' && letter <= 'Z')))
                return false;

            // The letter must be at the start of a segment:
            // either at position 0, or the character before it is ':'
            if (colonIndex - 1 != 0 && value[colonIndex - 2] != ':')
                return false;

            // Must be followed by '\' or '/'
            if (colonIndex + 1 >= value.Length)
                return false;

            var afterColon = value[colonIndex + 1];
            return afterColon == '\\' || afterColon == '/';
        }

        /// <summary>
        /// Determines the volume type based on the source path.
        /// Paths starting with <c>.</c>, <c>/</c>, <c>~</c>, or containing a path separator
        /// are treated as bind mounts; otherwise they are named volumes.
        /// </summary>
        private static string DetectVolumeType(string source)
        {
            if (string.IsNullOrEmpty(source))
                return VolumeTypes.Volume;

            // Absolute paths and relative paths indicate bind mounts
            if (source.StartsWith("/", StringComparison.Ordinal) ||
                source.StartsWith(".", StringComparison.Ordinal) ||
                source.StartsWith("~", StringComparison.Ordinal))
            {
                return VolumeTypes.Bind;
            }

            // Windows absolute paths (already handled by the caller splitting correctly,
            // but the source may still contain backslashes)
            if (source.Length >= 3 &&
                ((source[0] >= 'a' && source[0] <= 'z') || (source[0] >= 'A' && source[0] <= 'Z')) &&
                source[1] == ':' &&
                (source[2] == '\\' || source[2] == '/'))
            {
                return VolumeTypes.Bind;
            }

            // If source contains path separators, treat as bind mount
            if (source.Contains("/") || source.Contains("\\"))
            {
                return VolumeTypes.Bind;
            }

            return VolumeTypes.Volume;
        }

        /// <summary>
        /// Parses the options portion of a volume mount string (e.g. <c>"ro"</c>, <c>"rw,z"</c>)
        /// and applies them to the given config.
        /// </summary>
        private static void ParseOptions(ServiceVolumeConfig config, string optionsStr)
        {
            if (string.IsNullOrEmpty(optionsStr))
                return;

            var options = optionsStr.Split(',');

            foreach (var option in options)
            {
                var trimmed = option.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (!ValidOptions.Contains(trimmed))
                    throw new FormatException($"Invalid volume option: '{trimmed}'");

                switch (trimmed)
                {
                    case "ro":
                        config.ReadOnly = true;
                        break;

                    case "rw":
                        config.ReadOnly = false;
                        break;

                    case "z":
                        EnsureBindConfig(config);
                        config.Bind!.SELinux = "z";
                        break;

                    case "Z":
                        EnsureBindConfig(config);
                        config.Bind!.SELinux = "Z";
                        break;

                    case "nocopy":
                        if (config.Volume == null)
                            config.Volume = new ServiceVolumeVolume();
                        config.Volume.NoCopy = true;
                        break;

                    default:
                        if (PropagationOptions.Contains(trimmed))
                        {
                            EnsureBindConfig(config);
                            config.Bind!.Propagation = trimmed;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Ensures that the <see cref="ServiceVolumeConfig.Bind"/> property is initialized.
        /// </summary>
        private static void EnsureBindConfig(ServiceVolumeConfig config)
        {
            if (config.Bind == null)
                config.Bind = new ServiceVolumeBind();
        }
    }
}
