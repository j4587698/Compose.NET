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
using System.Linq;

namespace Compose.NET.Loader
{
    /// <summary>
    /// Result of compose file discovery.
    /// </summary>
    public class ComposeFileDiscoveryResult
    {
        /// <summary>
        /// Gets the discovered compose file paths in priority order.
        /// The first path is the primary config file; subsequent paths are override files.
        /// </summary>
        public List<string> ConfigPaths { get; } = new List<string>();

        /// <summary>
        /// Gets any warnings generated during discovery (e.g., multiple candidates found).
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>
        /// Gets the resolved working directory (the directory containing the primary config file).
        /// </summary>
        public string WorkingDir { get; set; } = string.Empty;
    }

    /// <summary>
    /// Discovers Docker Compose configuration files following the compose-go algorithm.
    /// Mirrors the behavior of compose-go's cli/options.go (WithDefaultConfigPath, WithConfigFileEnv).
    /// </summary>
    /// <remarks>
    /// Discovery order:
    /// <list type="number">
    ///   <item>If explicit paths are provided, use them directly.</item>
    ///   <item>Check COMPOSE_FILE environment variable.</item>
    ///   <item>Search default filenames starting from workingDir, walking up parent directories.</item>
    /// </list>
    /// </remarks>
    public static class ComposeFileFinder
    {
        /// <summary>
        /// Environment variable name for specifying compose file paths.
        /// </summary>
        public const string ComposeFileEnvVar = "COMPOSE_FILE";

        /// <summary>
        /// Environment variable name for customizing the path separator used in COMPOSE_FILE.
        /// </summary>
        public const string ComposePathSeparatorEnvVar = "COMPOSE_PATH_SEPARATOR";

        /// <summary>
        /// Default compose file names in priority order (highest first).
        /// Mirrors compose-go's DefaultFileNames.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultFileNames = new[]
        {
            "compose.yaml",
            "compose.yml",
            "docker-compose.yml",
            "docker-compose.yaml",
        };

        /// <summary>
        /// Default override file names in priority order (highest first).
        /// Mirrors compose-go's DefaultOverrideFileNames.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultOverrideFileNames = new[]
        {
            "compose.override.yml",
            "compose.override.yaml",
            "docker-compose.override.yml",
            "docker-compose.override.yaml",
        };

        /// <summary>
        /// Discovers compose files using the full compose-go algorithm:
        /// 1. Use explicit paths if provided.
        /// 2. Check COMPOSE_FILE environment variable.
        /// 3. Search default filenames walking up from workingDir.
        /// </summary>
        /// <param name="workingDir">
        /// Starting directory for file discovery. If null, uses <see cref="Directory.GetCurrentDirectory"/>.
        /// </param>
        /// <param name="explicitPaths">
        /// Explicit config file paths (e.g., from CLI -f flags). If non-empty, skips all discovery.
        /// </param>
        /// <param name="environment">
        /// Environment variables to check for COMPOSE_FILE/COMPOSE_PATH_SEPARATOR.
        /// If null, reads from <see cref="Environment.GetEnvironmentVariable(string)"/>.
        /// </param>
        /// <returns>A <see cref="ComposeFileDiscoveryResult"/> with discovered paths and any warnings.</returns>
        public static ComposeFileDiscoveryResult Discover(
            string? workingDir = null,
            IReadOnlyList<string>? explicitPaths = null,
            IDictionary<string, string>? environment = null)
        {
            var result = new ComposeFileDiscoveryResult();
            var resolvedWorkingDir = workingDir ?? Directory.GetCurrentDirectory();

            // Step 1: If explicit paths are provided, use them directly
            if (explicitPaths != null && explicitPaths.Count > 0)
            {
                foreach (var path in explicitPaths)
                {
                    var absPath = Path.IsPathRooted(path)
                        ? path
                        : Path.GetFullPath(Path.Combine(resolvedWorkingDir, path));

                    if (!File.Exists(absPath))
                    {
                        throw new FileNotFoundException(
                            $"Compose file not found: {absPath}", absPath);
                    }

                    result.ConfigPaths.Add(absPath);
                }

                result.WorkingDir = Path.GetDirectoryName(result.ConfigPaths[0]) ?? resolvedWorkingDir;
                return result;
            }

            // Step 2: Check COMPOSE_FILE environment variable
            var composeFile = GetEnvVar(environment, ComposeFileEnvVar);
            if (!string.IsNullOrEmpty(composeFile))
            {
                var separator = GetEnvVar(environment, ComposePathSeparatorEnvVar);
                if (string.IsNullOrEmpty(separator))
                {
                    separator = Path.PathSeparator.ToString();
                }

                var paths = composeFile!.Split(new[] { separator! }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var path in paths)
                {
                    var trimmed = path.Trim();
                    if (trimmed == "-")
                    {
                        // stdin marker â€” pass through as-is
                        result.ConfigPaths.Add("-");
                        continue;
                    }

                    var absPath = Path.IsPathRooted(trimmed)
                        ? trimmed
                        : Path.GetFullPath(Path.Combine(resolvedWorkingDir, trimmed));

                    if (!File.Exists(absPath))
                    {
                        throw new FileNotFoundException(
                            $"Compose file not found: {absPath}", absPath);
                    }

                    result.ConfigPaths.Add(absPath);
                }

                if (result.ConfigPaths.Count > 0 && result.ConfigPaths[0] != "-")
                {
                    result.WorkingDir = Path.GetDirectoryName(result.ConfigPaths[0]) ?? resolvedWorkingDir;
                }
                else
                {
                    result.WorkingDir = resolvedWorkingDir;
                }

                // When using COMPOSE_FILE, no override file discovery
                return result;
            }

            // Step 3: Auto-discover by walking up from workingDir
            var discoveryResult = DiscoverDefaultFiles(resolvedWorkingDir);
            result.ConfigPaths.AddRange(discoveryResult.ConfigPaths);
            result.Warnings.AddRange(discoveryResult.Warnings);
            result.WorkingDir = discoveryResult.WorkingDir.Length > 0
                ? discoveryResult.WorkingDir
                : resolvedWorkingDir;

            return result;
        }

        /// <summary>
        /// Discovers compose files using only the default filename search (no env var check).
        /// Walks up from <paramref name="startDir"/> looking for default compose file names.
        /// </summary>
        /// <param name="startDir">The directory to start searching from.</param>
        /// <returns>A <see cref="ComposeFileDiscoveryResult"/> with discovered paths.</returns>
        public static ComposeFileDiscoveryResult DiscoverDefaultFiles(string startDir)
        {
            var result = new ComposeFileDiscoveryResult();
            var currentDir = Path.GetFullPath(startDir);

            while (true)
            {
                var candidates = FindFiles(DefaultFileNames, currentDir);
                if (candidates.Count > 0)
                {
                    // Use highest-priority candidate (first in list)
                    if (candidates.Count > 1)
                    {
                        result.Warnings.Add(
                            $"Found multiple config files with supported names: {string.Join(", ", candidates.Select(Path.GetFileName))}");
                        result.Warnings.Add($"Using {Path.GetFileName(candidates[0])}");
                    }

                    result.ConfigPaths.Add(candidates[0]);
                    result.WorkingDir = currentDir;

                    // Look for override files in the same directory
                    var overrides = FindFiles(DefaultOverrideFileNames, currentDir);
                    if (overrides.Count > 0)
                    {
                        // Use highest-priority override
                        result.ConfigPaths.Add(overrides[0]);
                    }

                    return result;
                }

                // Move to parent directory
                var parent = Path.GetDirectoryName(currentDir);
                if (parent == null || parent == currentDir)
                {
                    // Reached filesystem root â€” no config found (not an error)
                    result.WorkingDir = startDir;
                    return result;
                }

                currentDir = parent;
            }
        }

        /// <summary>
        /// Finds existing files from a list of candidate names in the given directory.
        /// Returns full paths of files that exist, in the same order as <paramref name="names"/>.
        /// </summary>
        /// <param name="names">The candidate file names to check.</param>
        /// <param name="directory">The directory to search in.</param>
        /// <returns>List of full paths that exist, ordered by priority.</returns>
        public static List<string> FindFiles(IReadOnlyList<string> names, string directory)
        {
            var found = new List<string>();
            foreach (var name in names)
            {
                var path = Path.Combine(directory, name);
                if (File.Exists(path))
                {
                    found.Add(Path.GetFullPath(path));
                }
            }
            return found;
        }

        /// <summary>
        /// Gets an environment variable value, either from the provided dictionary
        /// or from <see cref="Environment.GetEnvironmentVariable(string)"/> as fallback.
        /// </summary>
        private static string? GetEnvVar(IDictionary<string, string>? environment, string name)
        {
            if (environment != null)
            {
                return environment.TryGetValue(name, out var value) ? value : null;
            }
            return Environment.GetEnvironmentVariable(name);
        }
    }
}
