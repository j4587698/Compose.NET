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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Compose.NET.Errors;
using Compose.NET.Interpolation;
using Compose.NET.Override;
using Compose.NET.Types;

namespace Compose.NET.Loader
{
    /// <summary>
    /// Main loader pipeline that orchestrates all phases of Docker Compose YAML loading.
    /// Mirrors compose-go's loader.LoadWithContext().
    /// </summary>
    public static class ComposeLoader
    {
        private static readonly Regex ProjectNamePattern = new Regex(
            @"[^a-z0-9_-]", RegexOptions.Compiled);

        #region Public entry points

        /// <summary>
        /// Loads a Compose project from the given configuration details.
        /// </summary>
        public static LoadResult Load(ConfigDetails configDetails, LoadOptions? options = null)
        {
            if (configDetails == null) throw new ArgumentNullException(nameof(configDetails));
            options ??= new LoadOptions();

            var warnings = new List<string>();
            var workingDir = configDetails.WorkingDir;
            if (string.IsNullOrEmpty(workingDir))
                workingDir = Directory.GetCurrentDirectory();

            // Phase 0-1: Load YAML for each config file
            // Each file is parsed with ResetProcessor to detect !reset/!override tags
            // while correctly handling YAML anchors/aliases/merge keys.
            var configs = new List<Dictionary<string, object?>>();
            var resetProcessors = new List<ResetProcessor?>();
            var composeFiles = new List<string>();

            foreach (var cf in configDetails.ConfigFiles)
            {
                Dictionary<string, object?> parsed;
                ResetProcessor? resetProcessor = null;

                if (cf.Config != null)
                {
                    // Pre-parsed config â€” no tag detection possible
                    parsed = cf.Config;
                }
                else if (cf.Content != null)
                {
                    // Parse with ResetProcessor to detect !reset/!override tags
                    resetProcessor = new ResetProcessor();
                    parsed = resetProcessor.ParseWithResetDetection(cf.Content);
                }
                else if (!string.IsNullOrEmpty(cf.Filename))
                {
                    var bytes = string.Equals(cf.Filename, "-", StringComparison.Ordinal)
                        ? ReadFromStandardInput()
                        : File.ReadAllBytes(cf.Filename);
                    resetProcessor = new ResetProcessor();
                    parsed = resetProcessor.ParseWithResetDetection(bytes);
                }
                else
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(cf.Filename))
                    composeFiles.Add(cf.Filename);

                // Phase 3: Interpolation
                if (!options.SkipInterpolation)
                {
                    var interpOptions = new InterpolateOptions
                    {
                        LookupValue = Interpolator.CreateLookup(
                            ToNullableEnvironment(configDetails.Environment), fallbackToEnv: true),
                        TypeCastMapping = BuildDefaultTypeCastMapping()
                    };

                    parsed = Interpolator.Interpolate(parsed, interpOptions);
                }

                // Phase 4: Fix empty/null values
                parsed = YamlParser.FixEmptyNotNull(parsed);

                // Phase 5: Extends resolution (including cross-file extends)
                if (!options.SkipExtends)
                {
                    var currentFile = !string.IsNullOrEmpty(cf.Filename) ? cf.Filename : null;
                    parsed = ResolveExtends(parsed, warnings, workingDir, currentFile, options);
                }

                // Phase 6: Include â€” load included compose files as sub-projects and merge
                if (!options.SkipInclude && parsed.ContainsKey("include"))
                {
                    var currentFile = !string.IsNullOrEmpty(cf.Filename) ? cf.Filename : null;
                    parsed = ResolveIncludes(parsed, warnings, workingDir, currentFile, configDetails.Environment, options);
                }

                // Phase 9: Canonical transform
                parsed = CanonicalTransformer.Transform(parsed);

                configs.Add(parsed);
                resetProcessors.Add(resetProcessor);
            }

            if (configs.Count == 0)
            {
                return new LoadResult
                {
                    Project = new Project { Name = NormalizeProjectName(options.ProjectName ?? ""), WorkingDir = workingDir },
                    Warnings = warnings
                };
            }

            // Phase 7: Multi-file merge
            // Before merging each override file, apply !reset/!override null overrides
            // to the accumulated base. This clears paths in the base that the override
            // file has tagged with !reset or !override, so the override values replace
            // rather than merge with the base.
            var merged = configs[0];
            for (int i = 1; i < configs.Count; i++)
            {
                // Apply null overrides from the override file's ResetProcessor
                // onto the merged base BEFORE merging the override's values in.
                var processor = resetProcessors[i];
                if (processor != null && processor.HasResets)
                {
                    processor.ApplyNullOverrides(merged);
                }

                merged = MergeProject.MergeYaml(merged, configs[i]);
            }

            // Phase 7b: EnforceUnicity â€” deduplicate sequences after merge
            MergeProject.EnforceUnicity(merged);

            // Phase 0: Project name determination
            var projectName = DetermineProjectName(merged, options, workingDir);

            // Phase 8: Schema validation
            if (!options.SkipValidation)
            {
                var schemaErrors = SchemaValidator.ValidateSchema(merged);
                if (schemaErrors.Count > 0)
                {
                    throw new ComposeInvalidException(string.Join(Environment.NewLine, schemaErrors));
                }

                // Phase 8b: Obsolete "version" attribute warning and removal
                if (merged.ContainsKey("version"))
                {
                    var versionSource = composeFiles.Count > 0 ? composeFiles[0] : "compose";
                    warnings.Add(
                        $"{versionSource}: the attribute `version` is obsolete, it will be ignored, please remove it to avoid potential confusion");
                    merged.Remove("version");
                }
            }

            // Phase 10: OmitEmpty â€” remove nil/empty-string values at specific paths
            OmitEmpty(merged);

            // Phase 11: Set defaults
            if (!options.SkipDefaultValues)
            {
                SetDefaults(merged);
            }

            // Phase 12: Validation (logical consistency)
            if (!options.SkipValidation)
            {
                var logicErrors = SchemaValidator.ValidateLogic(merged);
                if (logicErrors.Count > 0)
                {
                    throw new ComposeInvalidException(string.Join(Environment.NewLine, logicErrors));
                }
            }

            // Phase 13: Resolve relative paths
            if (options.ResolvePaths)
            {
                ResolveRelativePaths(merged, workingDir);
            }

            // Phase 14: Resolve environment
            if (!options.SkipResolveEnvironment)
            {
                ResolveEnvironment(merged, configDetails.Environment, workingDir);
            }

            // Phase 15: Normalize
            if (!options.SkipNormalization)
            {
                Normalize(merged, projectName, configDetails.Environment);
            }

            // Phase 16-17: Bind dict to Project
            var project = BindProject(merged, projectName, workingDir);
            project.ComposeFiles = composeFiles;

            if (configDetails.Environment != null && configDetails.Environment.Count > 0)
            {
                foreach (var kvp in configDetails.Environment)
                {
                    project.Environment[kvp.Key] = kvp.Value;
                }
            }

            if (options.Profiles != null)
            {
                project.Profiles = new List<string>(options.Profiles);
                project = project.WithProfiles(options.Profiles);
            }

            // Phase 17b: Convert Windows volume paths (C:\foo â†’ /c/foo)
            if (options.ConvertWindowsPaths)
            {
                ConvertWindowsVolumePaths(project);
            }

            // Phase 18: Consistency check
            if (!options.SkipConsistencyCheck && !options.SkipValidation)
            {
                var consistencyErrors = CheckConsistency(project);
                if (consistencyErrors.Count > 0)
                {
                    throw new ComposeInvalidException(
                        string.Join(Environment.NewLine, consistencyErrors));
                }
            }

            return new LoadResult
            {
                Project = project,
                Warnings = warnings
            };
        }

        /// <summary>
        /// Loads a Compose project from a file path.
        /// </summary>
        public static LoadResult LoadFromFile(string filePath, LoadOptions? options = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (string.Equals(filePath, "-", StringComparison.Ordinal))
            {
                var stdinDetails = new ConfigDetails
                {
                    WorkingDir = Directory.GetCurrentDirectory(),
                    ConfigFiles = new List<ConfigFile>
                    {
                        new ConfigFile { Filename = "-" }
                    }
                };
                return Load(stdinDetails, options);
            }

            var fullPath = Path.GetFullPath(filePath);
            var dir = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();

            var details = new ConfigDetails
            {
                WorkingDir = dir,
                ConfigFiles = new List<ConfigFile>
                {
                    new ConfigFile { Filename = fullPath }
                }
            };

            return Load(details, options);
        }

        /// <summary>
        /// Loads a Compose project from YAML content string.
        /// </summary>
        public static LoadResult LoadFromContent(string yamlContent, string? workingDir = null, LoadOptions? options = null)
        {
            if (string.IsNullOrEmpty(yamlContent))
                throw new ArgumentException("YAML content cannot be null or empty.", nameof(yamlContent));

            var dir = workingDir ?? Directory.GetCurrentDirectory();

            var details = new ConfigDetails
            {
                WorkingDir = dir,
                ConfigFiles = new List<ConfigFile>
                {
                    new ConfigFile
                    {
                        Filename = Path.Combine(dir, "compose.yaml"),
                        Content = System.Text.Encoding.UTF8.GetBytes(yamlContent)
                    }
                }
            };

            return Load(details, options);
        }

        /// <summary>
        /// Reads compose content from redirected standard input for <c>Filename == "-"</c>.
        /// </summary>
        private static byte[] ReadFromStandardInput()
        {
            if (!Console.IsInputRedirected)
            {
                throw new ComposeUnsupportedException(
                    "Config file '-' requires redirected standard input. " +
                    "Set ConfigFile.Content explicitly when using ComposeLoader as a library.");
            }

            var content = Console.In.ReadToEnd();
            return System.Text.Encoding.UTF8.GetBytes(content);
        }

        private static Dictionary<string, string?> ToNullableEnvironment(
            IDictionary<string, string> environment)
        {
            var result = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var kvp in environment)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        #endregion

        #region Pipeline phases

        private static Dictionary<TreePath, TypeCast> BuildDefaultTypeCastMapping()
        {
            return new Dictionary<TreePath, TypeCast>
            {
                [TreePath.New("services", "*", "ports", "[]", "target")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "ports", "[]", "published")] = s => s,
                [TreePath.New("services", "*", "scale")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "deploy", "replicas")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "deploy", "update_config", "parallelism")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "deploy", "rollback_config", "parallelism")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "deploy", "restart_policy", "max_attempts")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "privileged")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "read_only")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "stdin_open")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "tty")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "init")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "oom_kill_disable")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "healthcheck", "disable")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "healthcheck", "retries")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "ulimits", "*", "soft")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "ulimits", "*", "hard")] = Interpolator.CastToInt,
                [TreePath.New("services", "*", "pids_limit")] = Interpolator.CastToLong,
                [TreePath.New("services", "*", "cpu_count")] = Interpolator.CastToLong,
                [TreePath.New("services", "*", "cpu_shares")] = Interpolator.CastToLong,
                [TreePath.New("services", "*", "cpu_period")] = Interpolator.CastToLong,
                [TreePath.New("services", "*", "cpu_quota")] = Interpolator.CastToLong,
                [TreePath.New("services", "*", "oom_score_adj")] = Interpolator.CastToLong,
                [TreePath.New("services", "*", "cpus")] = Interpolator.CastToDouble,
                [TreePath.New("services", "*", "cpu_percent")] = Interpolator.CastToDouble,
                [TreePath.New("services", "*", "build", "no_cache")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "build", "pull")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "build", "privileged")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "depends_on", "*", "required")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "depends_on", "*", "restart")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "env_file", "[]", "required")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "attach")] = Interpolator.CastToBool,
                [TreePath.New("services", "*", "volumes", "[]", "read_only")] = Interpolator.CastToBool,
                [TreePath.New("networks", "*", "external")] = Interpolator.CastToBool,
                [TreePath.New("networks", "*", "internal")] = Interpolator.CastToBool,
                [TreePath.New("networks", "*", "attachable")] = Interpolator.CastToBool,
                [TreePath.New("volumes", "*", "external")] = Interpolator.CastToBool,
                [TreePath.New("secrets", "*", "external")] = Interpolator.CastToBool,
                [TreePath.New("configs", "*", "external")] = Interpolator.CastToBool,
            };
        }

        private static string DetermineProjectName(Dictionary<string, object?> config, LoadOptions options, string workingDir)
        {
            // Priority: options -> YAML name field -> directory name
            if (!string.IsNullOrEmpty(options.ProjectName))
                return NormalizeProjectName(options.ProjectName!);

            var yamlName = YamlParser.GetProjectName(config);
            if (!string.IsNullOrEmpty(yamlName))
                return NormalizeProjectName(yamlName!);

            var dirName = new DirectoryInfo(workingDir).Name;
            return NormalizeProjectName(dirName);
        }

        private static string NormalizeProjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "default";

            var lower = name.ToLowerInvariant();
            var normalized = ProjectNamePattern.Replace(lower, "");
            if (string.IsNullOrEmpty(normalized))
                return "default";
            return normalized;
        }

        private static Dictionary<string, object?> ResolveExtends(
            Dictionary<string, object?> config,
            List<string> warnings,
            string workingDir,
            string? currentFile,
            LoadOptions options)
        {
            if (!config.TryGetValue("services", out var servicesObj) ||
                servicesObj is not Dictionary<string, object?> services)
                return config;

            // Build a dependency-free traversal order
            var resolved = new HashSet<string>(StringComparer.Ordinal);
            var resolving = new HashSet<string>(StringComparer.Ordinal);
            // Cache for loaded external files: absolute path â†’ services dict
            var externalCache = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in services.Keys.ToList())
            {
                ResolveServiceExtends(name, services, resolved, resolving, warnings,
                    workingDir, currentFile, options, externalCache);
            }

            return config;
        }

        private static void ResolveServiceExtends(
            string serviceName,
            Dictionary<string, object?> services,
            HashSet<string> resolved,
            HashSet<string> resolving,
            List<string> warnings,
            string workingDir,
            string? currentFile,
            LoadOptions options,
            Dictionary<string, Dictionary<string, object?>> externalCache)
        {
            if (resolved.Contains(serviceName)) return;
            if (resolving.Contains(serviceName))
            {
                throw new ComposeInvalidException(
                    $"Circular extends dependency detected for service '{serviceName}'.");
            }

            resolving.Add(serviceName);

            if (!services.TryGetValue(serviceName, out var svcObj) ||
                svcObj is not Dictionary<string, object?> svcDict)
            {
                resolving.Remove(serviceName);
                resolved.Add(serviceName);
                return;
            }

            if (!svcDict.TryGetValue("extends", out var extendsObj) ||
                extendsObj is not Dictionary<string, object?> extendsDict)
            {
                resolving.Remove(serviceName);
                resolved.Add(serviceName);
                return;
            }

            var baseServiceName = extendsDict.TryGetValue("service", out var baseName)
                ? baseName?.ToString() ?? ""
                : "";

            if (string.IsNullOrEmpty(baseServiceName))
            {
                throw new ComposeInvalidException(
                    $"Service '{serviceName}' extends without specifying a service name.");
            }

            // Determine if this is a cross-file extends
            var hasFile = extendsDict.TryGetValue("file", out var fileObj) && fileObj != null
                          && !string.IsNullOrEmpty(fileObj.ToString());

            if (hasFile)
            {
                // Cross-file extends: load external file and resolve
                var filePath = fileObj!.ToString()!;
                var baseDir = !string.IsNullOrEmpty(currentFile)
                    ? Path.GetDirectoryName(Path.GetFullPath(currentFile)) ?? workingDir
                    : workingDir;
                var absolutePath = Path.GetFullPath(Path.Combine(baseDir, filePath));

                if (!File.Exists(absolutePath))
                {
                    throw new ComposeNotFoundException(
                        $"Service '{serviceName}' extends from file '{filePath}' which was not found at '{absolutePath}'.");
                }

                // Load and cache external file
                var externalServices = LoadExternalFileServices(
                    absolutePath, warnings, options, externalCache);

                if (!externalServices.ContainsKey(baseServiceName))
                {
                    throw new ComposeInvalidException(
                        $"Service '{serviceName}' extends unknown service '{baseServiceName}' from file '{filePath}'.");
                }

                // Merge base into current
                if (externalServices[baseServiceName] is Dictionary<string, object?> baseDict)
                {
                    var merged = MergeProject.MergeYaml(baseDict, svcDict);
                    merged.Remove("extends");
                    services[serviceName] = merged;
                }
            }
            else
            {
                // Same-file extends
                if (!services.ContainsKey(baseServiceName))
                {
                    throw new ComposeInvalidException(
                        $"Service '{serviceName}' extends unknown service '{baseServiceName}'.");
                }

                // Resolve the base service first
                ResolveServiceExtends(baseServiceName, services, resolved, resolving, warnings,
                    workingDir, currentFile, options, externalCache);

                // Merge base into current
                if (services[baseServiceName] is Dictionary<string, object?> baseDict)
                {
                    var merged = MergeProject.MergeYaml(baseDict, svcDict);
                    merged.Remove("extends");
                    services[serviceName] = merged;
                }
            }

            resolving.Remove(serviceName);
            resolved.Add(serviceName);
        }

        /// <summary>
        /// Loads an external Compose file and returns its services dictionary (with extends resolved).
        /// Results are cached to avoid re-loading the same file multiple times.
        /// </summary>
        private static Dictionary<string, object?> LoadExternalFileServices(
            string absolutePath,
            List<string> warnings,
            LoadOptions options,
            Dictionary<string, Dictionary<string, object?>> cache)
        {
            if (cache.TryGetValue(absolutePath, out var cached))
                return cached;

            try
            {
                var bytes = File.ReadAllBytes(absolutePath);
                var parsed = YamlParser.ParseYaml(bytes);

                // Apply interpolation if not skipped
                if (!options.SkipInterpolation)
                {
                    var interpOptions = new InterpolateOptions
                    {
                        LookupValue = Interpolator.CreateLookup(
                            new Dictionary<string, string?>(), fallbackToEnv: true),
                        TypeCastMapping = BuildDefaultTypeCastMapping()
                    };
                    parsed = Interpolator.Interpolate(parsed, interpOptions);
                }

                // Fix empty/null values
                parsed = YamlParser.FixEmptyNotNull(parsed);

                // Recursively resolve extends in the external file
                var externalDir = Path.GetDirectoryName(absolutePath) ?? "";
                parsed = ResolveExtends(parsed, warnings, externalDir, absolutePath, options);

                // Apply canonical transform
                parsed = CanonicalTransformer.Transform(parsed);

                // Extract services
                if (parsed.TryGetValue("services", out var servicesObj) &&
                    servicesObj is Dictionary<string, object?> services)
                {
                    cache[absolutePath] = services;
                    return services;
                }

                cache[absolutePath] = new Dictionary<string, object?>();
                return cache[absolutePath];
            }
            catch (Exception ex)
            {
                throw new ComposeInvalidException(
                    $"Failed to load external file '{absolutePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resolves <c>include</c> directives by loading each included file as a sub-project
        /// and merging its top-level resources (services, networks, volumes, secrets, configs)
        /// into the current config. Mirrors compose-go's include resolution.
        /// </summary>
        private static Dictionary<string, object?> ResolveIncludes(
            Dictionary<string, object?> config,
            List<string> warnings,
            string workingDir,
            string? currentFile,
            Dictionary<string, string> environment,
            LoadOptions options,
            HashSet<string>? includeStack = null)
        {
            if (!config.TryGetValue("include", out var includeObj))
                return config;

            // The include value should be a list (already canonicalized by CanonicalTransformer)
            if (includeObj is not List<object?> includeList || includeList.Count == 0)
            {
                config.Remove("include");
                return config;
            }

            var baseDir = !string.IsNullOrEmpty(currentFile)
                ? Path.GetDirectoryName(Path.GetFullPath(currentFile)) ?? workingDir
                : workingDir;

            includeStack ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(currentFile))
                includeStack.Add(Path.GetFullPath(currentFile));

            foreach (var entry in includeList)
            {
                if (entry == null) continue;

                string? includePath = null;
                string? projectDir = null;
                string? envFilePath = null;

                if (entry is Dictionary<string, object?> entryDict)
                {
                    // Long form: {path: "...", project_directory: "...", env_file: "..."}
                    if (entryDict.TryGetValue("path", out var pathObj) && pathObj != null)
                        includePath = pathObj.ToString();
                    if (entryDict.TryGetValue("project_directory", out var pdObj) && pdObj != null)
                        projectDir = pdObj.ToString();
                    if (entryDict.TryGetValue("env_file", out var efObj) && efObj != null)
                        envFilePath = efObj.ToString();
                }
                else if (entry is string s)
                {
                    includePath = s;
                }

                if (string.IsNullOrEmpty(includePath))
                {
                    throw new ComposeInvalidException("Include entry has no path specified.");
                }

                // Resolve paths relative to the including file's directory
                var absoluteIncludePath = Path.GetFullPath(Path.Combine(baseDir, includePath));

                if (!File.Exists(absoluteIncludePath))
                {
                    throw new ComposeNotFoundException(
                        $"Include file '{includePath}' not found at '{absoluteIncludePath}'.");
                }

                if (includeStack.Contains(absoluteIncludePath))
                {
                    throw new ComposeInvalidException(
                        $"Circular include detected for file '{includePath}'.");
                }

                // Determine the project directory for the included file
                var includeDir = !string.IsNullOrEmpty(projectDir)
                    ? Path.GetFullPath(Path.Combine(baseDir, projectDir))
                    : Path.GetDirectoryName(absoluteIncludePath) ?? baseDir;

                // Build environment for the included file
                var includeEnv = new Dictionary<string, string>(environment ?? new Dictionary<string, string>());

                // Load env_file if specified
                if (!string.IsNullOrEmpty(envFilePath))
                {
                    var absoluteEnvPath = Path.GetFullPath(Path.Combine(baseDir, envFilePath));
                    if (!File.Exists(absoluteEnvPath))
                    {
                        throw new ComposeNotFoundException(
                            $"Include env_file '{envFilePath}' not found at '{absoluteEnvPath}'.");
                    }

                    try
                    {
                        var envVars = DotEnv.EnvFileParser.ReadFile(absoluteEnvPath);
                        foreach (var kvp in envVars)
                        {
                            includeEnv[kvp.Key] = kvp.Value ?? "";
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ComposeInvalidException(
                            $"Failed to load env_file '{envFilePath}' for include: {ex.Message}", ex);
                    }
                }

                var nextIncludeStack = new HashSet<string>(includeStack, StringComparer.OrdinalIgnoreCase)
                {
                    absoluteIncludePath
                };

                var includedParsed = LoadIncludedFileAsDict(
                    absoluteIncludePath, warnings, includeEnv, includeDir, options, nextIncludeStack);
                MergeIncludedConfig(config, includedParsed);
            }

            // Remove include from config after processing
            config.Remove("include");
            return config;
        }

        /// <summary>
        /// Loads an included file through the standard pipeline phases (up to canonical transform)
        /// and returns the processed dictionary for merging.
        /// </summary>
        private static Dictionary<string, object?> LoadIncludedFileAsDict(
            string absolutePath,
            List<string> warnings,
            Dictionary<string, string> environment,
            string workingDir,
            LoadOptions options,
            HashSet<string> includeStack)
        {
            var bytes = File.ReadAllBytes(absolutePath);
            var parsed = YamlParser.ParseYaml(bytes);

            // Interpolation
            if (!options.SkipInterpolation)
            {
                var interpOptions = new InterpolateOptions
                {
                    LookupValue = Interpolator.CreateLookup(ToNullableEnvironment(environment), fallbackToEnv: true),
                    TypeCastMapping = BuildDefaultTypeCastMapping()
                };
                parsed = Interpolator.Interpolate(parsed, interpOptions);
            }

            // Fix empty/null
            parsed = YamlParser.FixEmptyNotNull(parsed);

            // Resolve extends
            if (!options.SkipExtends)
            {
                parsed = ResolveExtends(parsed, warnings, workingDir, absolutePath, options);
            }

            // Resolve nested includes
            if (!options.SkipInclude && parsed.ContainsKey("include"))
            {
                parsed = ResolveIncludes(parsed, warnings, workingDir, absolutePath, environment, options, includeStack);
            }

            // Canonical transform
            parsed = CanonicalTransformer.Transform(parsed);

            // Set defaults
            if (!options.SkipDefaultValues)
            {
                SetDefaults(parsed);
            }

            // Resolve relative paths
            if (options.ResolvePaths)
            {
                ResolveRelativePaths(parsed, workingDir);
            }

            // Resolve environment
            if (!options.SkipResolveEnvironment)
            {
                ResolveEnvironment(parsed, environment, workingDir);
            }

            return parsed;
        }

        /// <summary>
        /// Merges resources from an included config into the main config.
        /// For each top-level section (services, networks, volumes, secrets, configs),
        /// entries from the included config are added if they don't already exist.
        /// Existing entries in the main config take precedence.
        /// </summary>
        private static void MergeIncludedConfig(
            Dictionary<string, object?> main,
            Dictionary<string, object?> included)
        {
            var sectionsToMerge = new[] { "services", "networks", "volumes", "secrets", "configs" };

            foreach (var section in sectionsToMerge)
            {
                if (!included.TryGetValue(section, out var includedSectionObj) ||
                    includedSectionObj is not Dictionary<string, object?> includedSection ||
                    includedSection.Count == 0)
                    continue;

                if (!main.TryGetValue(section, out var mainSectionObj) ||
                    mainSectionObj is not Dictionary<string, object?> mainSection)
                {
                    mainSection = new Dictionary<string, object?>(StringComparer.Ordinal);
                    main[section] = mainSection;
                }

                foreach (var kvp in includedSection)
                {
                    if (mainSection.ContainsKey(kvp.Key))
                    {
                        // Main config takes precedence â€” included resources don't override
                        continue;
                    }
                    mainSection[kvp.Key] = kvp.Value;
                }
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  OmitEmpty â€” remove nil / empty-string values at specific
        //  paths in the raw dictionary, matching compose-go's
        //  loader/omitEmpty.go behaviour.
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Paths whose list items should be stripped when nil or empty-string.
        /// compose-go currently registers only <c>services.*.dns</c>.
        /// </summary>
        private static readonly string[][] OmitEmptyPaths = new[]
        {
            new[] { "services", "*", "dns" },
        };

        /// <summary>
        /// Walks the dictionary tree and removes nil / empty-string entries
        /// from lists found at the registered <see cref="OmitEmptyPaths"/>.
        /// </summary>
        internal static void OmitEmpty(Dictionary<string, object?> config)
        {
            OmitEmptyRecursive(config, Array.Empty<string>());
        }

        private static void OmitEmptyRecursive(Dictionary<string, object?> node, string[] currentPath)
        {
            foreach (var kvp in node.ToList())
            {
                var childPath = AppendSegment(currentPath, kvp.Key);

                if (kvp.Value is List<object?> list && MatchesAnyOmitEmptyPath(childPath))
                {
                    // Remove nil and empty-string entries
                    list.RemoveAll(item => IsEmpty(item));
                }

                if (kvp.Value is Dictionary<string, object?> childDict)
                {
                    OmitEmptyRecursive(childDict, childPath);
                }
            }
        }

        private static bool IsEmpty(object? value)
        {
            if (value == null) return true;
            if (value is string s && s.Length == 0) return true;
            return false;
        }

        private static bool MatchesAnyOmitEmptyPath(string[] path)
        {
            foreach (var pattern in OmitEmptyPaths)
            {
                if (PathMatchesPattern(path, pattern))
                    return true;
            }
            return false;
        }

        private static bool PathMatchesPattern(string[] path, string[] pattern)
        {
            if (path.Length != pattern.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == "*") continue;
                if (!string.Equals(path[i], pattern[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static string[] AppendSegment(string[] path, string segment)
        {
            var result = new string[path.Length + 1];
            Array.Copy(path, result, path.Length);
            result[path.Length] = segment;
            return result;
        }

        private static void SetDefaults(Dictionary<string, object?> config)
        {
            if (!config.TryGetValue("services", out var servicesObj) ||
                servicesObj is not Dictionary<string, object?> services)
                return;

            foreach (var kvp in services)
            {
                if (kvp.Value is not Dictionary<string, object?> svcDict)
                    continue;

                // Build defaults: context defaults to "."
                if (svcDict.TryGetValue("build", out var buildObj) && buildObj is Dictionary<string, object?> buildDict)
                {
                    if (!buildDict.ContainsKey("context") || buildDict["context"] == null)
                        buildDict["context"] = ".";
                }

                // Port defaults
                if (svcDict.TryGetValue("ports", out var portsObj) && portsObj is List<object?> ports)
                {
                    foreach (var portItem in ports)
                    {
                        if (portItem is Dictionary<string, object?> portDict)
                        {
                            if (!portDict.ContainsKey("protocol") || portDict["protocol"] == null
                                || (portDict["protocol"] is string pp && string.IsNullOrEmpty(pp)))
                                portDict["protocol"] = "tcp";
                            if (!portDict.ContainsKey("mode") || portDict["mode"] == null
                                || (portDict["mode"] is string mm && string.IsNullOrEmpty(mm)))
                                portDict["mode"] = "ingress";
                        }
                    }
                }

                // Volume bind defaults
                if (svcDict.TryGetValue("volumes", out var volumesObj) && volumesObj is List<object?> volumes)
                {
                    foreach (var volItem in volumes)
                    {
                        if (volItem is Dictionary<string, object?> volDict)
                        {
                            var volType = GetString(volDict, "type");
                            if (volType == "bind")
                            {
                                if (!volDict.ContainsKey("bind"))
                                {
                                    volDict["bind"] = new Dictionary<string, object?>
                                    {
                                        ["create_host_path"] = true
                                    };
                                }
                                else if (volDict["bind"] is Dictionary<string, object?> bindDict)
                                {
                                    if (!bindDict.ContainsKey("create_host_path"))
                                        bindDict["create_host_path"] = true;
                                }
                            }
                        }
                    }
                }

                // Secret mount defaults: target defaults to "/run/secrets/{source}"
                if (svcDict.TryGetValue("secrets", out var secretsObj) && secretsObj is List<object?> secrets)
                {
                    foreach (var secretItem in secrets)
                    {
                        if (secretItem is Dictionary<string, object?> secretDict)
                        {
                            if (!secretDict.ContainsKey("target") || secretDict["target"] == null)
                            {
                                var source = secretDict.TryGetValue("source", out var s) && s != null
                                    ? s.ToString()
                                    : "";
                                secretDict["target"] = "/run/secrets/" + source;
                            }
                        }
                    }
                }

                // depends_on defaults
                if (svcDict.TryGetValue("depends_on", out var depObj) && depObj is Dictionary<string, object?> deps)
                {
                    foreach (var dep in deps)
                    {
                        if (dep.Value is Dictionary<string, object?> depDict)
                        {
                            if (!depDict.ContainsKey("condition"))
                                depDict["condition"] = "service_started";
                            if (!depDict.ContainsKey("required"))
                                depDict["required"] = true;
                        }
                    }
                }

                // Deploy device request defaults: count defaults to "all" if no count and no device_ids
                if (svcDict.TryGetValue("deploy", out var deployObj) && deployObj is Dictionary<string, object?> deployDict)
                {
                    if (deployDict.TryGetValue("resources", out var resObj) && resObj is Dictionary<string, object?> resDict)
                    {
                        if (resDict.TryGetValue("reservations", out var revObj) && revObj is Dictionary<string, object?> revDict)
                        {
                            if (revDict.TryGetValue("devices", out var devObj) && devObj is List<object?> devices)
                            {
                                foreach (var devItem in devices)
                                {
                                    if (devItem is Dictionary<string, object?> devDict)
                                    {
                                        var hasCount = devDict.ContainsKey("count") && devDict["count"] != null;
                                        var hasDeviceIds = devDict.ContainsKey("device_ids") && devDict["device_ids"] != null;
                                        if (!hasCount && !hasDeviceIds)
                                            devDict["count"] = "all";
                                    }
                                }
                            }
                        }
                    }
                }

                // GPU device request defaults
                if (svcDict.TryGetValue("gpus", out var gpusObj) && gpusObj is List<object?> gpus)
                {
                    foreach (var gpuItem in gpus)
                    {
                        if (gpuItem is Dictionary<string, object?> gpuDict)
                        {
                            var hasCount = gpuDict.ContainsKey("count") && gpuDict["count"] != null;
                            var hasDeviceIds = gpuDict.ContainsKey("device_ids") && gpuDict["device_ids"] != null;
                            if (!hasCount && !hasDeviceIds)
                                gpuDict["count"] = "all";
                        }
                    }
                }
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  ConvertWindowsVolumePaths â€” convert Windows drive-letter
        //  paths to Docker-style /c/... format in volume mounts.
        //  Mirrors compose-go's convertVolumePath in loader.go.
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Converts Windows-style drive-letter volume source paths (e.g. <c>C:\my\path</c>)
        /// to Docker-compatible paths (e.g. <c>/c/my/path</c>) for all service volumes.
        /// Only applies when <see cref="LoadOptions.ConvertWindowsPaths"/> is <c>true</c>.
        /// </summary>
        private static void ConvertWindowsVolumePaths(Project project)
        {
            foreach (var kvp in project.Services)
            {
                var svc = kvp.Value;
                for (int i = 0; i < svc.Volumes.Count; i++)
                {
                    svc.Volumes[i] = ConvertVolumePath(svc.Volumes[i]);
                }
            }
        }

        /// <summary>
        /// Converts a single volume source path from Windows drive-letter format
        /// (e.g. <c>C:\my\path</c>) to Docker-compatible format (e.g. <c>/c/my/path</c>).
        /// </summary>
        internal static ServiceVolumeConfig ConvertVolumePath(ServiceVolumeConfig volume)
        {
            if (string.IsNullOrEmpty(volume.Source))
                return volume;

            var driveLetter = GetDriveLetter(volume.Source);
            if (driveLetter == null)
                return volume;

            // C:\my\path â†’ /c/my/path
            var converted = "/" + char.ToLowerInvariant(driveLetter.Value) + volume.Source.Substring(2);
            converted = converted.Replace('\\', '/');
            volume.Source = converted;
            return volume;
        }

        /// <summary>
        /// Returns the drive letter if <paramref name="path"/> starts with a Windows
        /// drive letter pattern (e.g. <c>C:\</c> or <c>c:/</c>), otherwise <c>null</c>.
        /// </summary>
        internal static char? GetDriveLetter(string path)
        {
            if (path.Length < 3)
                return null;

            if (char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'))
                return path[0];

            return null;
        }

        private static void ResolveRelativePaths(Dictionary<string, object?> config, string workingDir)
        {
            if (!config.TryGetValue("services", out var servicesObj) ||
                servicesObj is not Dictionary<string, object?> services)
                return;

            foreach (var kvp in services)
            {
                if (kvp.Value is not Dictionary<string, object?> svcDict) continue;

                // Resolve build context
                if (svcDict.TryGetValue("build", out var buildObj) && buildObj is Dictionary<string, object?> buildDict)
                {
                    if (buildDict.TryGetValue("context", out var ctxObj) && ctxObj is string ctx && !string.IsNullOrEmpty(ctx))
                    {
                        if (!Path.IsPathRooted(ctx))
                            buildDict["context"] = Path.GetFullPath(Path.Combine(workingDir, ctx));
                    }
                    if (buildDict.TryGetValue("dockerfile", out var dfObj) && dfObj is string df && !string.IsNullOrEmpty(df))
                    {
                        if (!Path.IsPathRooted(df))
                        {
                            var context = buildDict.TryGetValue("context", out var c) ? c?.ToString() ?? workingDir : workingDir;
                            buildDict["dockerfile"] = Path.GetFullPath(Path.Combine(context, df));
                        }
                    }
                }

                // Resolve env_file paths
                if (svcDict.TryGetValue("env_file", out var efObj) && efObj is List<object?> envFiles)
                {
                    foreach (var item in envFiles)
                    {
                        if (item is Dictionary<string, object?> efDict)
                        {
                            if (efDict.TryGetValue("path", out var pathObj) && pathObj is string p && !string.IsNullOrEmpty(p))
                            {
                                if (!Path.IsPathRooted(p))
                                    efDict["path"] = Path.GetFullPath(Path.Combine(workingDir, p));
                            }
                        }
                    }
                }

                // Resolve label_file paths
                if (svcDict.TryGetValue("label_file", out var lfObj) && lfObj is List<object?> labelFiles)
                {
                    for (int i = 0; i < labelFiles.Count; i++)
                    {
                        if (labelFiles[i] is string lfPath && !string.IsNullOrEmpty(lfPath))
                        {
                            if (!Path.IsPathRooted(lfPath))
                                labelFiles[i] = Path.GetFullPath(Path.Combine(workingDir, lfPath));
                        }
                    }
                }

                // Resolve volume bind source paths
                if (svcDict.TryGetValue("volumes", out var volObj) && volObj is List<object?> vols)
                {
                    foreach (var item in vols)
                    {
                        if (item is Dictionary<string, object?> volDict)
                        {
                            var type = GetString(volDict, "type");
                            if (type == "bind")
                            {
                                if (volDict.TryGetValue("source", out var srcObj) && srcObj is string src && !string.IsNullOrEmpty(src))
                                {
                                    if (!Path.IsPathRooted(src))
                                        volDict["source"] = Path.GetFullPath(Path.Combine(workingDir, src));
                                }
                            }
                        }
                    }
                }
            }

            // Resolve top-level secret/config file paths
            ResolveFileObjectPaths(config, "secrets", workingDir);
            ResolveFileObjectPaths(config, "configs", workingDir);
        }

        private static void ResolveFileObjectPaths(Dictionary<string, object?> config, string key, string workingDir)
        {
            if (!config.TryGetValue(key, out var obj) || obj is not Dictionary<string, object?> items)
                return;

            foreach (var kvp in items)
            {
                if (kvp.Value is Dictionary<string, object?> itemDict)
                {
                    if (itemDict.TryGetValue("file", out var fileObj) && fileObj is string f && !string.IsNullOrEmpty(f))
                    {
                        if (!Path.IsPathRooted(f))
                            itemDict["file"] = Path.GetFullPath(Path.Combine(workingDir, f));
                    }
                }
            }
        }

        private static void ResolveEnvironment(
            Dictionary<string, object?> config,
            Dictionary<string, string> hostEnv,
            string workingDir)
        {
            if (!config.TryGetValue("services", out var servicesObj) ||
                servicesObj is not Dictionary<string, object?> services)
                return;

            foreach (var kvp in services)
            {
                if (kvp.Value is not Dictionary<string, object?> svcDict) continue;

                // Resolve env_file entries into environment
                if (svcDict.TryGetValue("env_file", out var efObj) && efObj is List<object?> envFiles)
                {
                    var envFromFiles = new Dictionary<string, string?>(StringComparer.Ordinal);
                    // Accumulate parsed vars so later env_files can reference earlier ones
                    var accumulatedVars = new Dictionary<string, string>(StringComparer.Ordinal);

                    // Build host env lookup
                    VariableLookup? envLookup = hostEnv != null
                        ? key => hostEnv.TryGetValue(key, out var v) ? (v ?? string.Empty, true) : (string.Empty, false)
                        : null;

                    foreach (var item in envFiles)
                    {
                        if (item is Dictionary<string, object?> efDict)
                        {
                            var path = GetString(efDict, "path");
                            var required = GetBool(efDict, "required", true);
                            var format = GetString(efDict, "format");
                            if (!string.IsNullOrEmpty(path))
                            {
                                if (File.Exists(path))
                                {
                                    try
                                    {
                                        var parsed = DotEnv.EnvFormatRegistry.ParseFile(
                                            path, format, accumulatedVars, envLookup);
                                        foreach (var kv in parsed)
                                        {
                                            envFromFiles[kv.Key] = kv.Value;
                                            accumulatedVars[kv.Key] = kv.Value;
                                        }
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        // Unknown format â€” re-throw to surface to the user
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new ComposeInvalidException(
                                            $"Failed to parse env_file '{path}'.", ex);
                                    }
                                }
                                else if (required)
                                {
                                    throw new FileNotFoundException($"env_file '{path}' not found.");
                                }
                            }
                        }
                    }

                    // Merge env_file values under environment (env_file has lower priority)
                    if (envFromFiles.Count > 0)
                    {
                        if (!svcDict.TryGetValue("environment", out var envObj) || envObj is not Dictionary<string, object?> envDict)
                        {
                            envDict = new Dictionary<string, object?>(StringComparer.Ordinal);
                            svcDict["environment"] = envDict;
                        }

                        foreach (var ef in envFromFiles)
                        {
                            if (!envDict.ContainsKey(ef.Key))
                            {
                                envDict[ef.Key] = ef.Value;
                            }
                        }
                    }
                }

                // Resolve null environment values from host env
                if (svcDict.TryGetValue("environment", out var envO) && envO is Dictionary<string, object?> env)
                {
                    foreach (var key in env.Keys.ToList())
                    {
                        if (env[key] == null && hostEnv != null && hostEnv.TryGetValue(key, out var val))
                        {
                            env[key] = val;
                        }
                    }
                }

                // Resolve label_file entries into labels
                ResolveLabelFiles(svcDict);
            }
        }

        private static void ResolveLabelFiles(Dictionary<string, object?> svcDict)
        {
            if (!svcDict.TryGetValue("label_file", out var lfObj) || lfObj is not List<object?> labelFiles)
                return;

            if (labelFiles.Count == 0)
                return;

            // Collect labels from all label_file entries; later files override earlier ones
            var labelsFromFiles = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var item in labelFiles)
            {
                var path = item as string;
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"label_file '{path}' not found.");
                }

                try
                {
                    var lines = File.ReadAllLines(path);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        var eqIdx = trimmed.IndexOf('=');
                        if (eqIdx < 0)
                        {
                            // Key without value â€” treat as key with empty value for labels
                            labelsFromFiles[trimmed] = "";
                        }
                        else
                        {
                            labelsFromFiles[trimmed.Substring(0, eqIdx)] = trimmed.Substring(eqIdx + 1);
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to read label_file '{path}'.", ex);
                }
            }

            if (labelsFromFiles.Count == 0)
                return;

            // Merge label_file values into labels (explicit labels have higher priority)
            if (!svcDict.TryGetValue("labels", out var lblObj) || lblObj is not Dictionary<string, object?> labelsDict)
            {
                labelsDict = new Dictionary<string, object?>(StringComparer.Ordinal);
                svcDict["labels"] = labelsDict;
            }

            foreach (var lf in labelsFromFiles)
            {
                if (!labelsDict.ContainsKey(lf.Key))
                {
                    labelsDict[lf.Key] = lf.Value;
                }
            }
        }

        private static void Normalize(
            Dictionary<string, object?> config,
            string projectName,
            Dictionary<string, string>? hostEnv = null)
        {
            // Ensure project name is stored in the dict (needed for resource name prefixing)
            config["name"] = projectName;

            // Step 1: Normalize networks â€” inject default network for services
            NormalizeNetworks(config);

            // Step 2: Per-service normalization
            if (config.TryGetValue("services", out var servicesObj) &&
                servicesObj is Dictionary<string, object?> services)
            {
                foreach (var kvp in services)
                {
                    if (kvp.Value is not Dictionary<string, object?> svcDict) continue;
                    NormalizeService(svcDict, hostEnv);
                }
            }

            // Step 3: Set resource names with project prefix
            SetNameFromKey(config, projectName);
        }

        /// <summary>
        /// Ensures all services have a network assignment.
        /// If no top-level networks exist, creates a default.
        /// Services without network_mode or explicit networks get assigned to "default".
        /// If any service uses "default" but it's not declared, auto-creates it.
        /// </summary>
        private static void NormalizeNetworks(Dictionary<string, object?> config)
        {
            bool hasTopLevelNetworks = config.TryGetValue("networks", out var netsObj) &&
                                       netsObj is Dictionary<string, object?> topNets && topNets.Count > 0;

            bool defaultNetworkUsed = false;

            if (config.TryGetValue("services", out var servicesObj) &&
                servicesObj is Dictionary<string, object?> services)
            {
                foreach (var kvp in services)
                {
                    if (kvp.Value is not Dictionary<string, object?> svcDict) continue;

                    // Services with provider don't need networks
                    if (svcDict.ContainsKey("provider")) continue;

                    // If no network_mode and no networks, assign default
                    if (!svcDict.ContainsKey("network_mode"))
                    {
                        if (!svcDict.TryGetValue("networks", out var svcNetsObj) ||
                            svcNetsObj is not Dictionary<string, object?> svcNets ||
                            svcNets.Count == 0)
                        {
                            svcDict["networks"] = new Dictionary<string, object?>
                            {
                                ["default"] = null
                            };
                            defaultNetworkUsed = true;
                        }
                        else if (svcNets.ContainsKey("default"))
                        {
                            defaultNetworkUsed = true;
                        }
                    }
                }
            }

            // Ensure top-level networks dict exists
            if (!hasTopLevelNetworks)
            {
                config["networks"] = new Dictionary<string, object?>
                {
                    ["default"] = new Dictionary<string, object?>()
                };
            }
            else if (defaultNetworkUsed)
            {
                // If any service uses "default" but it's not in top-level, create it
                var topNetworks = (Dictionary<string, object?>)config["networks"]!;
                if (!topNetworks.ContainsKey("default"))
                {
                    topNetworks["default"] = new Dictionary<string, object?>();
                }
            }
        }

        /// <summary>
        /// Normalizes a single service dictionary:
        /// - Standardize pull_policy
        /// - Inject build defaults
        /// - Convert links to depends_on
        /// - Create implicit depends_on from namespace refs and volumes_from
        /// - Clean volume target paths
        /// - Resolve build args from environment
        /// </summary>
        private static void NormalizeService(
            Dictionary<string, object?> svcDict,
            Dictionary<string, string>? hostEnv)
        {
            // 1. pull_policy: "if_not_present" -> "missing"
            if (svcDict.TryGetValue("pull_policy", out var ppObj) && ppObj is string pp)
            {
                if (string.Equals(pp, PullPolicies.IfNotPresent, StringComparison.Ordinal))
                {
                    svcDict["pull_policy"] = PullPolicies.Missing;
                }
            }

            // 2. Build defaults
            if (svcDict.TryGetValue("build", out var buildObj) && buildObj is Dictionary<string, object?> build)
            {
                if (!build.ContainsKey("context") || build["context"] == null)
                {
                    build["context"] = ".";
                }
                if ((!build.ContainsKey("dockerfile") || build["dockerfile"] == null) &&
                    (!build.ContainsKey("dockerfile_inline") || build["dockerfile_inline"] == null))
                {
                    build["dockerfile"] = "Dockerfile";
                }

                // Resolve build args from environment
                if (build.TryGetValue("args", out var argsObj))
                {
                    build["args"] = ResolveNullMappingValues(argsObj, hostEnv, keepEmpty: false);
                }
            }

            // 3. Resolve environment null values from host env
            if (svcDict.TryGetValue("environment", out var envObj))
            {
                svcDict["environment"] = ResolveNullMappingValues(envObj, hostEnv, keepEmpty: true);
            }

            // 4. Ensure depends_on dict exists for link/namespace resolution
            Dictionary<string, object?> dependsOn;
            if (svcDict.TryGetValue("depends_on", out var doObj) &&
                doObj is Dictionary<string, object?> existingDo)
            {
                dependsOn = existingDo;
            }
            else
            {
                dependsOn = new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            bool dependsOnModified = false;

            // 5. links -> depends_on
            if (svcDict.TryGetValue("links", out var linksObj) && linksObj is List<object?> links)
            {
                foreach (var item in links)
                {
                    if (item is not string link) continue;

                    // "service:alias" -> take service part
                    var parts = link.Split(':');
                    var target = parts.Length >= 2 ? parts[0] : link;

                    if (!dependsOn.ContainsKey(target))
                    {
                        dependsOn[target] = new Dictionary<string, object?>
                        {
                            ["condition"] = ServiceConditions.Started,
                            ["restart"] = true,
                            ["required"] = true
                        };
                        dependsOnModified = true;
                    }
                }
            }

            // 6. Namespace refs (network_mode, ipc, pid, uts, cgroup) -> implicit depends_on
            foreach (var ns in new[] { "network_mode", "ipc", "pid", "uts", "cgroup" })
            {
                if (svcDict.TryGetValue(ns, out var nsObj) && nsObj is string nsVal)
                {
                    if (nsVal.StartsWith(ServicePrefixes.Service, StringComparison.Ordinal))
                    {
                        var shared = nsVal.Substring(ServicePrefixes.Service.Length);
                        if (!dependsOn.ContainsKey(shared))
                        {
                            dependsOn[shared] = new Dictionary<string, object?>
                            {
                                ["condition"] = ServiceConditions.Started,
                                ["restart"] = true,
                                ["required"] = true
                            };
                            dependsOnModified = true;
                        }
                    }
                }
            }

            // 7. volumes_from -> implicit depends_on
            if (svcDict.TryGetValue("volumes_from", out var vfObj) && vfObj is List<object?> volumesFrom)
            {
                foreach (var item in volumesFrom)
                {
                    if (item is not string vol) continue;

                    // Skip container: references
                    if (vol.StartsWith(ServicePrefixes.Container, StringComparison.Ordinal))
                        continue;

                    var spec = vol.Split(':');
                    var target = spec[0];

                    if (!dependsOn.ContainsKey(target))
                    {
                        dependsOn[target] = new Dictionary<string, object?>
                        {
                            ["condition"] = ServiceConditions.Started,
                            ["restart"] = false, // volumes_from uses restart=false
                            ["required"] = true
                        };
                        dependsOnModified = true;
                    }
                }
            }

            // Store back depends_on if modified
            if (dependsOnModified && dependsOn.Count > 0)
            {
                svcDict["depends_on"] = dependsOn;
            }

            // 8. Volume target path cleanup
            if (svcDict.TryGetValue("volumes", out var volsObj) && volsObj is List<object?> volumes)
            {
                foreach (var item in volumes)
                {
                    if (item is Dictionary<string, object?> volDict &&
                        volDict.TryGetValue("target", out var tObj) && tObj is string target)
                    {
                        // Normalize path: remove trailing slashes, resolve /../ etc.
                        var cleaned = NormalizePosixPath(target);
                        if (cleaned != target)
                        {
                            volDict["target"] = cleaned;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resolves null values in a mapping (dict or list of KEY=VALUE) from the host environment.
        /// For build args (keepEmpty=false): removes entries that can't be resolved.
        /// For environment (keepEmpty=true): keeps entries with null values.
        /// </summary>
        private static object? ResolveNullMappingValues(
            object? value,
            Dictionary<string, string>? hostEnv,
            bool keepEmpty)
        {
            if (value is Dictionary<string, object?> dict)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in dict)
                {
                    if (kvp.Value == null)
                    {
                        if (hostEnv != null && hostEnv.TryGetValue(kvp.Key, out var envVal))
                        {
                            result[kvp.Key] = envVal;
                        }
                        else if (keepEmpty)
                        {
                            result[kvp.Key] = null;
                        }
                        // else: drop the entry (build args behavior)
                    }
                    else
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
                return result;
            }

            if (value is List<object?> list)
            {
                var result = new List<object?>();
                foreach (var item in list)
                {
                    if (item is string s)
                    {
                        if (!s.Contains('='))
                        {
                            // Key-only entry: resolve from host env
                            if (hostEnv != null && hostEnv.TryGetValue(s, out var envVal))
                            {
                                result.Add($"{s}={envVal}");
                            }
                            else if (keepEmpty)
                            {
                                result.Add(s);
                            }
                        }
                        else
                        {
                            result.Add(s);
                        }
                    }
                    else
                    {
                        result.Add(item);
                    }
                }
                return result;
            }

            return value;
        }

        /// <summary>
        /// Sets resource names with project prefix for networks, volumes, configs, secrets.
        /// External resources use the key name directly.
        /// Non-external resources without an explicit name get "{projectName}_{key}".
        /// </summary>
        private static void SetNameFromKey(Dictionary<string, object?> config, string projectName)
        {
            foreach (var resourceType in new[] { "networks", "volumes", "configs", "secrets" })
            {
                if (!config.TryGetValue(resourceType, out var rtObj) ||
                    rtObj is not Dictionary<string, object?> resources)
                    continue;

                foreach (var kvp in resources)
                {
                    var key = kvp.Key;
                    var resource = kvp.Value as Dictionary<string, object?>;
                    if (resource == null)
                    {
                        // null resource (e.g. `networks: { default: }`) â€” create dict and set name
                        resource = new Dictionary<string, object?>(StringComparer.Ordinal);
                        resources[key] = resource;
                    }

                    if (!resource.ContainsKey("name") || resource["name"] == null)
                    {
                        if (resource.TryGetValue("external", out var extObj) && IsTrue(extObj))
                        {
                            // External resources use the key name directly
                            resource["name"] = key;
                        }
                        else
                        {
                            // Prefix with project name
                            resource["name"] = $"{projectName}_{key}";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a value represents a truthy boolean.
        /// </summary>
        private static bool IsTrue(object? value)
        {
            if (value is bool b) return b;
            if (value is string s) return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        /// <summary>
        /// Normalizes a POSIX-style path by cleaning up redundant separators and resolving "." and "..".
        /// Equivalent to Go's path.Clean().
        /// </summary>
        private static string NormalizePosixPath(string p)
        {
            if (string.IsNullOrEmpty(p)) return ".";

            var rooted = p[0] == '/';
            var n = p.Length;
            var segments = new List<string>();

            int i = 0;
            while (i < n)
            {
                // Skip slashes
                if (p[i] == '/')
                {
                    i++;
                    continue;
                }

                // Find end of segment
                int j = i;
                while (j < n && p[j] != '/')
                    j++;

                var segment = p.Substring(i, j - i);
                i = j;

                if (segment == ".")
                {
                    continue;
                }
                else if (segment == "..")
                {
                    if (segments.Count > 0 && segments[segments.Count - 1] != "..")
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }
                    else if (!rooted)
                    {
                        segments.Add("..");
                    }
                }
                else
                {
                    segments.Add(segment);
                }
            }

            var result = string.Join("/", segments);
            if (rooted) result = "/" + result;
            if (string.IsNullOrEmpty(result)) result = rooted ? "/" : ".";

            return result;
        }

        #endregion

        #region Consistency checks

        /// <summary>
        /// Performs post-binding consistency checks on the fully resolved project.
        /// Mirrors compose-go's checkConsistency function.
        /// Returns a list of error messages (empty if consistent).
        /// </summary>
        private static List<string> CheckConsistency(Project project)
        {
            var errors = new List<string>();

            foreach (var kvp in project.Services)
            {
                var name = kvp.Key;
                var s = kvp.Value;

                // 1. Service must have image, build, or provider
                if (s.Build == null && string.IsNullOrEmpty(s.Image) && s.Provider == null)
                {
                    errors.Add($"service \"{name}\" has neither an image nor a build context specified: invalid compose project");
                }

                // 2. dockerfile and dockerfile_inline are mutually exclusive
                if (s.Build != null &&
                    !string.IsNullOrEmpty(s.Build.DockerfileInline) &&
                    !string.IsNullOrEmpty(s.Build.Dockerfile))
                {
                    errors.Add($"service \"{name}\" declares mutually exclusive 'dockerfile' and 'dockerfile_inline'.");
                }

                // 3. build.additional_contexts referencing services must exist and have build
                if (s.Build != null && s.Build.AdditionalContexts.Count > 0)
                {
                    foreach (var acKvp in s.Build.AdditionalContexts)
                    {
                        var target = acKvp.Value;
                        if (target.StartsWith(ServicePrefixes.Service, StringComparison.Ordinal))
                        {
                            var refName = target.Substring(ServicePrefixes.Service.Length);
                            if (!project.Services.ContainsKey(refName))
                            {
                                errors.Add($"service \"{name}\": build.additional_contexts references undefined service \"{refName}\".");
                            }
                            else if (project.Services[refName].Build == null)
                            {
                                errors.Add($"service \"{name}\": build.additional_contexts references service \"{refName}\" which has no build context.");
                            }
                        }
                    }
                }

                // 4. build.platforms must contain service.platform
                if (s.Build != null &&
                    s.Build.Platforms.Count > 0 &&
                    !string.IsNullOrEmpty(s.Platform))
                {
                    if (!s.Build.Platforms.Contains(s.Platform))
                    {
                        errors.Add($"service \"{name}\": platform \"{s.Platform}\" is not included in build.platforms.");
                    }
                }

                // 5. network_mode and networks are mutually exclusive
                if (!string.IsNullOrEmpty(s.NetworkMode) && s.Networks.Count > 0)
                {
                    errors.Add($"service \"{name}\" declares mutually exclusive 'network_mode' and 'networks'.");
                }

                // 6. Referenced networks must exist in project
                foreach (var netName in s.Networks.Keys)
                {
                    if (!project.Networks.ContainsKey(netName))
                    {
                        errors.Add($"service \"{name}\" refers to undefined network \"{netName}\".");
                    }
                }

                // 7. healthcheck.test format validation
                if (s.HealthCheck != null && s.HealthCheck.Test.Count > 0)
                {
                    var first = s.HealthCheck.Test[0];
                    if (first != "CMD" && first != "CMD-SHELL" && first != "NONE")
                    {
                        errors.Add($"service \"{name}\": healthcheck.test must start with \"CMD\", \"CMD-SHELL\", or \"NONE\".");
                    }
                }

                // 8. depends_on references must exist
                foreach (var dep in s.DependsOn)
                {
                    var depName = dep.Key;
                    var depCfg = dep.Value;

                    if (!project.Services.ContainsKey(depName))
                    {
                        // Check if it's a disabled service
                        if (project.DisabledServices.ContainsKey(depName))
                        {
                            if (depCfg.Required)
                            {
                                errors.Add($"service \"{name}\" depends on disabled service \"{depName}\".");
                            }
                            // Non-required dependency on disabled service is OK
                        }
                        else
                        {
                            errors.Add($"service \"{name}\" depends on undefined service \"{depName}\".");
                        }
                    }
                }

                // 9. network_mode: service:xxx must reference existing service
                if (!string.IsNullOrEmpty(s.NetworkMode) &&
                    s.NetworkMode.StartsWith(ServicePrefixes.Service, StringComparison.Ordinal))
                {
                    var refName = s.NetworkMode.Substring(ServicePrefixes.Service.Length);
                    if (!project.Services.ContainsKey(refName) &&
                        !project.DisabledServices.ContainsKey(refName))
                    {
                        errors.Add($"service \"{name}\": service \"{refName}\" not found for network_mode 'service:{refName}'.");
                    }
                }

                // 10. Volume references must exist in project volumes
                foreach (var vol in s.Volumes)
                {
                    if (string.Equals(vol.Type, VolumeTypes.Volume, StringComparison.Ordinal) &&
                        !string.IsNullOrEmpty(vol.Source))
                    {
                        if (!project.Volumes.ContainsKey(vol.Source))
                        {
                            errors.Add($"service \"{name}\" refers to undefined volume \"{vol.Source}\".");
                        }
                    }
                }

                // 11. build.secrets references must exist
                if (s.Build?.Secrets != null)
                {
                    foreach (var secret in s.Build.Secrets)
                    {
                        if (!project.Secrets.ContainsKey(secret.Source))
                        {
                            errors.Add($"service \"{name}\" refers to undefined build secret \"{secret.Source}\".");
                        }
                    }
                }

                // 12. config references must exist
                foreach (var cfg in s.Configs)
                {
                    if (!project.Configs.ContainsKey(cfg.Source))
                    {
                        errors.Add($"service \"{name}\" refers to undefined config \"{cfg.Source}\".");
                    }
                }

                // 13. model references must exist
                if (s.ServiceModels != null)
                {
                    foreach (var modelName in s.ServiceModels.Keys)
                    {
                        if (!project.Models.ContainsKey(modelName))
                        {
                            errors.Add($"service \"{name}\" refers to undefined model \"{modelName}\".");
                        }
                    }
                }

                // 14. secret references must exist
                foreach (var secret in s.Secrets)
                {
                    if (!project.Secrets.ContainsKey(secret.Source))
                    {
                        errors.Add($"service \"{name}\" refers to undefined secret \"{secret.Source}\".");
                    }
                }

                // 15. scale and deploy.replicas consistency
                if (s.Scale.HasValue && s.Deploy != null && s.Deploy.Replicas.HasValue)
                {
                    if (s.Scale.Value != s.Deploy.Replicas.Value)
                    {
                        errors.Add($"service \"{name}\": can't set distinct values on 'scale' ({s.Scale.Value}) and 'deploy.replicas' ({s.Deploy.Replicas.Value}).");
                    }
                }

                // 16. scale/replicas non-negative
                if (s.Scale.HasValue && s.Scale.Value < 0)
                {
                    errors.Add($"service \"{name}\": 'scale' must be non-negative.");
                }
                if (s.Deploy?.Replicas != null && s.Deploy.Replicas.Value < 0)
                {
                    errors.Add($"service \"{name}\": 'deploy.replicas' must be non-negative.");
                }

                // 17. Resource limit consistency: cpus vs deploy.resources.limits.cpus
                if (s.CPUS > 0 && s.Deploy?.Resources.Limits != null && s.Deploy.Resources.Limits.NanoCPUs > 0)
                {
                    if (Math.Abs(s.CPUS - s.Deploy.Resources.Limits.NanoCPUs) > 0.001f)
                    {
                        errors.Add($"service \"{name}\": can't set distinct values for 'cpus' and 'deploy.resources.limits.cpus'.");
                    }
                }

                // mem_limit vs deploy.resources.limits.memory
                if (s.MemLimit.Bytes > 0 && s.Deploy?.Resources.Limits != null && s.Deploy.Resources.Limits.MemoryBytes.Bytes > 0)
                {
                    if (s.MemLimit.Bytes != s.Deploy.Resources.Limits.MemoryBytes.Bytes)
                    {
                        errors.Add($"service \"{name}\": can't set distinct values for 'mem_limit' and 'deploy.resources.limits.memory'.");
                    }
                }

                // mem_reservation vs deploy.resources.reservations.memory
                if (s.MemReservation.Bytes > 0 && s.Deploy?.Resources.Reservations != null && s.Deploy.Resources.Reservations.MemoryBytes.Bytes > 0)
                {
                    if (s.MemReservation.Bytes != s.Deploy.Resources.Reservations.MemoryBytes.Bytes)
                    {
                        errors.Add($"service \"{name}\": can't set distinct values for 'mem_reservation' and 'deploy.resources.reservations.memory'.");
                    }
                }

                // pids_limit vs deploy.resources.limits.pids
                if (s.PidsLimit > 0 && s.Deploy?.Resources.Limits != null && s.Deploy.Resources.Limits.Pids > 0)
                {
                    if (s.PidsLimit != s.Deploy.Resources.Limits.Pids)
                    {
                        errors.Add($"service \"{name}\": can't set distinct values for 'pids_limit' and 'deploy.resources.limits.pids'.");
                    }
                }

                // 18. container_name conflicts with scale > 1
                if (s.GetScale() > 1 && !string.IsNullOrEmpty(s.ContainerName))
                {
                    errors.Add($"service \"{name}\": can't set 'container_name' and scale > 1 as container name must be unique.");
                }

                // 19. develop.watch target validation
                if (s.Develop != null)
                {
                    foreach (var watch in s.Develop.Watch)
                    {
                        if (string.IsNullOrEmpty(watch.Target) &&
                            watch.Action != WatchActions.Rebuild &&
                            watch.Action != WatchActions.Restart)
                        {
                            errors.Add($"service \"{name}\": develop.watch 'target' is required for non-rebuild/restart actions (action=\"{watch.Action}\").");
                        }
                    }
                }

                // 20. Mount conflict detection (tmpfs + volumes)
                var mounts = new Dictionary<string, string>(StringComparer.Ordinal);

                for (int i = 0; i < s.Tmpfs.Count; i++)
                {
                    var tmpfs = s.Tmpfs[i];
                    var mountPath = tmpfs;
                    var colonIdx = tmpfs.IndexOf(':');
                    if (colonIdx >= 0)
                        mountPath = tmpfs.Substring(0, colonIdx);

                    var loc = $"tmpfs[{i}]";
                    if (mounts.TryGetValue(mountPath, out var existing))
                    {
                        errors.Add($"service \"{name}\": target \"{mountPath}\" is already mounted by {existing}; conflicting mount at {loc}.");
                    }
                    else
                    {
                        mounts[mountPath] = loc;
                    }
                }

                for (int i = 0; i < s.Volumes.Count; i++)
                {
                    var vol = s.Volumes[i];
                    var loc = $"volumes[{i}]";
                    if (mounts.TryGetValue(vol.Target, out var existing))
                    {
                        errors.Add($"service \"{name}\": target \"{vol.Target}\" is already mounted by {existing}; conflicting mount at {loc}.");
                    }
                    else
                    {
                        mounts[vol.Target] = loc;
                    }
                }
            }

            // 21. Top-level secret validation
            foreach (var kvp in project.Secrets)
            {
                var secret = kvp.Value;
                if (secret.External) continue;
                if (string.IsNullOrEmpty(secret.File) && string.IsNullOrEmpty(secret.Environment) && string.IsNullOrEmpty(secret.Content))
                {
                    errors.Add($"secret \"{kvp.Key}\" must declare either 'file', 'environment', or 'content'.");
                }
            }

            // 22. Dependency cycle detection
            var cycleError = DetectDependencyCycles(project);
            if (cycleError != null)
            {
                errors.Add(cycleError);
            }

            return errors;
        }

        /// <summary>
        /// Detects cycles in the service dependency graph.
        /// Returns an error message if a cycle is found, null otherwise.
        /// </summary>
        private static string? DetectDependencyCycles(Project project)
        {
            // Build adjacency list
            var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var kvp in project.Services)
            {
                var deps = new List<string>();
                foreach (var dep in kvp.Value.DependsOn)
                {
                    if (project.Services.ContainsKey(dep.Key))
                    {
                        deps.Add(dep.Key);
                    }
                }
                children[kvp.Key] = deps;
            }

            // DFS cycle detection
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var inStack = new HashSet<string>(StringComparer.Ordinal);
            var path = new List<string>();

            foreach (var node in children.Keys)
            {
                var cycle = DfsCycleCheck(node, children, visited, inStack, path);
                if (cycle != null)
                {
                    return cycle;
                }
            }

            return null;
        }

        private static string? DfsCycleCheck(
            string node,
            Dictionary<string, List<string>> children,
            HashSet<string> visited,
            HashSet<string> inStack,
            List<string> path)
        {
            if (inStack.Contains(node))
            {
                // Found cycle â€” find where the cycle starts in the path
                var idx = path.IndexOf(node);
                var cyclePath = path.GetRange(idx, path.Count - idx);
                cyclePath.Add(node);
                return $"dependency cycle detected: {string.Join(" -> ", cyclePath)}";
            }

            if (visited.Contains(node))
                return null;

            visited.Add(node);
            inStack.Add(node);
            path.Add(node);

            if (children.TryGetValue(node, out var deps))
            {
                foreach (var dep in deps)
                {
                    var result = DfsCycleCheck(dep, children, visited, inStack, path);
                    if (result != null)
                        return result;
                }
            }

            path.RemoveAt(path.Count - 1);
            inStack.Remove(node);
            return null;
        }

        #endregion

        #region Binding: dict -> Project

        private static Project BindProject(Dictionary<string, object?> dict, string projectName, string workingDir)
        {
            var project = new Project
            {
                Name = projectName,
                WorkingDir = workingDir
            };

            // Bind services
            if (dict.TryGetValue("services", out var servicesObj) && servicesObj is Dictionary<string, object?> services)
            {
                foreach (var kvp in services)
                {
                    if (kvp.Value is Dictionary<string, object?> svcDict)
                    {
                        var svc = BindService(kvp.Key, svcDict);
                        project.Services[kvp.Key] = svc;
                    }
                }
            }

            // Bind networks
            if (dict.TryGetValue("networks", out var networksObj) && networksObj is Dictionary<string, object?> networks)
            {
                foreach (var kvp in networks)
                {
                    project.Networks[kvp.Key] = BindNetworkConfig(kvp.Value);
                }
            }

            // Bind volumes
            if (dict.TryGetValue("volumes", out var volumesObj) && volumesObj is Dictionary<string, object?> volumes)
            {
                foreach (var kvp in volumes)
                {
                    project.Volumes[kvp.Key] = BindVolumeConfig(kvp.Value);
                }
            }

            // Bind secrets
            if (dict.TryGetValue("secrets", out var secretsObj) && secretsObj is Dictionary<string, object?> secrets)
            {
                foreach (var kvp in secrets)
                {
                    project.Secrets[kvp.Key] = BindSecretConfig(kvp.Value);
                }
            }

            // Bind configs
            if (dict.TryGetValue("configs", out var configsObj) && configsObj is Dictionary<string, object?> configs)
            {
                foreach (var kvp in configs)
                {
                    project.Configs[kvp.Key] = BindConfigObjConfig(kvp.Value);
                }
            }

            // Bind models
            if (dict.TryGetValue("models", out var modelsObj) && modelsObj is Dictionary<string, object?> models)
            {
                foreach (var kvp in models)
                {
                    project.Models[kvp.Key] = BindModelConfig(kvp.Value);
                }
            }

            // Bind extensions
            project.Extensions = BindExtensions(dict);

            return project;
        }

        #endregion

        #region Service binding

        private static ServiceConfig BindService(string name, Dictionary<string, object?> dict)
        {
            var svc = new ServiceConfig
            {
                Name = name,
                Image = GetString(dict, "image"),
                ContainerName = GetString(dict, "container_name"),
                Hostname = GetString(dict, "hostname"),
                DomainName = GetString(dict, "domainname"),
                User = GetString(dict, "user"),
                WorkingDir = GetString(dict, "working_dir"),
                MacAddress = GetString(dict, "mac_address"),
                Pid = GetString(dict, "pid"),
                Ipc = GetString(dict, "ipc"),
                Isolation = GetString(dict, "isolation"),
                Restart = GetString(dict, "restart"),
                Runtime = GetString(dict, "runtime"),
                Platform = GetString(dict, "platform"),
                PullPolicy = GetString(dict, "pull_policy"),
                StopSignal = GetString(dict, "stop_signal"),
                NetworkMode = GetString(dict, "network_mode"),
                CgroupParent = GetString(dict, "cgroup_parent"),
                Cgroup = GetString(dict, "cgroup"),
                UserNSMode = GetString(dict, "userns_mode"),
                Uts = GetString(dict, "uts"),
                VolumeDriver = GetString(dict, "volume_driver"),
                Dockerfile = GetString(dict, "dockerfile"),
                Privileged = GetBool(dict, "privileged"),
                ReadOnly = GetBool(dict, "read_only"),
                StdinOpen = GetBool(dict, "stdin_open"),
                Tty = GetBool(dict, "tty"),
                OomKillDisable = GetBool(dict, "oom_kill_disable"),
                Init = GetNullableBool(dict, "init"),
                Attach = GetNullableBool(dict, "attach"),
                Scale = GetNullableInt(dict, "scale"),
                CPUCount = GetLong(dict, "cpu_count"),
                CPUPeriod = GetLong(dict, "cpu_period"),
                CPUQuota = GetLong(dict, "cpu_quota"),
                CPURTPeriod = GetLong(dict, "cpu_rt_period"),
                CPURTRuntime = GetLong(dict, "cpu_rt_runtime"),
                CPUShares = GetLong(dict, "cpu_shares"),
                OomScoreAdj = GetLong(dict, "oom_score_adj"),
                PidsLimit = GetLong(dict, "pids_limit"),
                CPUS = GetFloat(dict, "cpus"),
                CPUPercent = GetFloat(dict, "cpu_percent"),
            };

            // CPUSet
            svc.CPUSet = GetString(dict, "cpuset");

            // Profiles
            svc.Profiles = GetStringList(dict, "profiles");

            // Command
            svc.Command = BindShellCommand(dict, "command");

            // Entrypoint
            svc.Entrypoint = BindShellCommand(dict, "entrypoint");

            // StopGracePeriod
            var sgp = GetString(dict, "stop_grace_period");
            if (!string.IsNullOrEmpty(sgp) && Duration.TryParse(sgp, out var sgpDuration))
                svc.StopGracePeriod = sgpDuration;

            // Memory fields
            svc.MemLimit = BindUnitBytes(dict, "mem_limit");
            svc.MemReservation = BindUnitBytes(dict, "mem_reservation");
            svc.MemSwapLimit = BindUnitBytes(dict, "memswap_limit");
            svc.MemSwappiness = GetNullableInt(dict, "mem_swappiness");
            svc.ShmSize = BindUnitBytes(dict, "shm_size");

            // Lists of strings
            svc.CapAdd = GetStringList(dict, "cap_add");
            svc.CapDrop = GetStringList(dict, "cap_drop");
            svc.GroupAdd = GetStringList(dict, "group_add");
            svc.SecurityOpt = GetStringList(dict, "security_opt");
            svc.ExternalLinks = GetStringList(dict, "external_links");
            svc.Links = GetStringList(dict, "links");
            svc.VolumesFrom = GetStringList(dict, "volumes_from");
            svc.DeviceCgroupRules = GetStringList(dict, "device_cgroup_rules");
            svc.LabelFiles = GetStringList(dict, "label_file");

            // DNS
            svc.DNS = new StringList(GetStringList(dict, "dns"));
            svc.DNSOpts = GetStringList(dict, "dns_opt");
            svc.DNSSearch = new StringList(GetStringList(dict, "dns_search"));

            // Tmpfs
            svc.Tmpfs = new StringList(GetStringList(dict, "tmpfs"));

            // Expose
            svc.Expose = BindStringOrNumberList(dict, "expose");

            // Environment
            svc.Environment = BindMappingWithEquals(dict, "environment");

            // Labels
            svc.Labels = BindLabels(dict, "labels");

            // Annotations
            svc.Annotations = BindMapping(dict, "annotations");

            // Sysctls
            svc.Sysctls = BindMapping(dict, "sysctls");

            // StorageOpt
            svc.StorageOpt = BindStringDict(dict, "storage_opt");

            // Extra hosts
            svc.ExtraHosts = BindExtraHosts(dict, "extra_hosts");

            // Ports
            svc.Ports = BindPorts(dict);

            // Volumes
            svc.Volumes = BindVolumes(dict);

            // Depends on
            svc.DependsOn = BindDependsOn(dict);

            // Networks
            svc.Networks = BindServiceNetworks(dict);

            // Secrets
            svc.Secrets = BindServiceSecrets(dict, "secrets");

            // Configs
            svc.Configs = BindServiceConfigs(dict, "configs");

            // Build
            svc.Build = BindBuildConfig(dict);

            // Healthcheck
            svc.HealthCheck = BindHealthCheck(dict);

            // Logging
            svc.Logging = BindLogging(dict);

            // Deploy
            svc.Deploy = BindDeploy(dict);

            // Ulimits
            svc.Ulimits = BindUlimits(dict);

            // Env files
            svc.EnvFiles = BindEnvFiles(dict);

            // Extends
            if (dict.TryGetValue("extends", out var extendsObj) && extendsObj is Dictionary<string, object?> extDict)
            {
                svc.Extends = new ExtendsConfig
                {
                    Service = GetString(extDict, "service") ?? "",
                    File = GetString(extDict, "file")
                };
            }

            // Devices
            svc.Devices = BindDevices(dict);

            // BlkioConfig
            svc.BlkioConfig = BindBlkioConfig(dict);

            // CredentialSpec
            if (dict.TryGetValue("credential_spec", out var credObj) && credObj is Dictionary<string, object?> credDict)
            {
                svc.CredentialSpec = new CredentialSpecConfig
                {
                    Config = GetString(credDict, "config"),
                    File = GetString(credDict, "file"),
                    Registry = GetString(credDict, "registry")
                };
            }

            // Gpus
            svc.Gpus = BindGpus(dict);

            // Develop
            svc.Develop = BindDevelopConfig(dict);

            // Provider
            svc.Provider = BindProviderConfig(dict);

            // Service models
            svc.ServiceModels = BindServiceModels(dict);

            // Lifecycle hooks
            svc.PostStart = BindServiceHooks(dict, "post_start");
            svc.PreStop = BindServiceHooks(dict, "pre_stop");

            // Extensions
            svc.Extensions = BindExtensions(dict);

            return svc;
        }

        #endregion

        #region Complex type binding

        private static List<ServicePortConfig> BindPorts(Dictionary<string, object?> dict)
        {
            var result = new List<ServicePortConfig>();
            if (!dict.TryGetValue("ports", out var portsObj) || portsObj is not List<object?> ports)
                return result;

            foreach (var item in ports)
            {
                if (item is Dictionary<string, object?> portDict)
                {
                    var cfg = new ServicePortConfig
                    {
                        Target = (uint)GetLong(portDict, "target"),
                        Published = GetString(portDict, "published"),
                        HostIP = GetString(portDict, "host_ip"),
                        Protocol = GetString(portDict, "protocol") ?? "tcp",
                        Mode = GetString(portDict, "mode") ?? "ingress",
                        AppProtocol = GetString(portDict, "app_protocol"),
                        Name = GetString(portDict, "name"),
                    };
                    result.Add(cfg);
                }
            }

            return result;
        }

        private static List<ServiceVolumeConfig> BindVolumes(Dictionary<string, object?> dict)
        {
            var result = new List<ServiceVolumeConfig>();
            if (!dict.TryGetValue("volumes", out var volsObj) || volsObj is not List<object?> vols)
                return result;

            foreach (var item in vols)
            {
                if (item is Dictionary<string, object?> volDict)
                {
                    var cfg = new ServiceVolumeConfig
                    {
                        Type = GetString(volDict, "type") ?? "",
                        Source = GetString(volDict, "source"),
                        Target = GetString(volDict, "target") ?? "",
                        ReadOnly = GetBool(volDict, "read_only"),
                        Consistency = GetString(volDict, "consistency"),
                    };

                    if (volDict.TryGetValue("bind", out var bindObj) && bindObj is Dictionary<string, object?> bindDict)
                    {
                        cfg.Bind = new ServiceVolumeBind
                        {
                            CreateHostPath = GetBool(bindDict, "create_host_path", true),
                            Propagation = GetString(bindDict, "propagation"),
                            SELinux = GetString(bindDict, "selinux"),
                            Recursive = GetString(bindDict, "recursive"),
                        };
                    }

                    if (volDict.TryGetValue("volume", out var vvObj) && vvObj is Dictionary<string, object?> vvDict)
                    {
                        cfg.Volume = new ServiceVolumeVolume
                        {
                            NoCopy = GetBool(vvDict, "nocopy"),
                            Subpath = GetString(vvDict, "subpath"),
                        };
                    }

                    if (volDict.TryGetValue("tmpfs", out var tmpfsObj) && tmpfsObj is Dictionary<string, object?> tmpfsDict)
                    {
                        cfg.Tmpfs = new ServiceVolumeTmpfs
                        {
                            Size = GetUnitBytes(tmpfsDict, "size"),
                            Mode = (uint)GetLong(tmpfsDict, "mode"),
                        };
                    }

                    result.Add(cfg);
                }
            }

            return result;
        }

        private static DependsOnConfig BindDependsOn(Dictionary<string, object?> dict)
        {
            var result = new DependsOnConfig();
            if (!dict.TryGetValue("depends_on", out var depObj))
                return result;

            if (depObj is Dictionary<string, object?> deps)
            {
                foreach (var kvp in deps)
                {
                    if (kvp.Value is Dictionary<string, object?> depDict)
                    {
                        result[kvp.Key] = new ServiceDependency
                        {
                            Condition = GetString(depDict, "condition") ?? ServiceConditions.Started,
                            Restart = GetBool(depDict, "restart"),
                            Required = GetBool(depDict, "required", true),
                        };
                    }
                    else
                    {
                        result[kvp.Key] = new ServiceDependency();
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, ServiceNetworkConfig?> BindServiceNetworks(Dictionary<string, object?> dict)
        {
            var result = new Dictionary<string, ServiceNetworkConfig?>();
            if (!dict.TryGetValue("networks", out var netsObj))
                return result;

            if (netsObj is Dictionary<string, object?> nets)
            {
                foreach (var kvp in nets)
                {
                    if (kvp.Value is Dictionary<string, object?> netDict)
                    {
                        result[kvp.Key] = new ServiceNetworkConfig
                        {
                            Aliases = GetStringList(netDict, "aliases"),
                            Ipv4Address = GetString(netDict, "ipv4_address"),
                            Ipv6Address = GetString(netDict, "ipv6_address"),
                            MacAddress = GetString(netDict, "mac_address"),
                            InterfaceName = GetString(netDict, "interface_name"),
                            Priority = GetInt(netDict, "priority"),
                            GatewayPriority = GetInt(netDict, "gateway_priority"),
                            LinkLocalIPs = GetStringList(netDict, "link_local_ips"),
                            DriverOpts = BindStringDict(netDict, "driver_opts"),
                        };
                    }
                    else
                    {
                        result[kvp.Key] = null;
                    }
                }
            }

            return result;
        }

        private static List<ServiceSecretConfig> BindServiceSecrets(Dictionary<string, object?> dict, string key)
        {
            var result = new List<ServiceSecretConfig>();
            if (!dict.TryGetValue(key, out var obj) || obj is not List<object?> list)
                return result;

            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> sDict)
                {
                    result.Add(new ServiceSecretConfig
                    {
                        Source = GetString(sDict, "source") ?? "",
                        Target = GetString(sDict, "target"),
                        UID = GetString(sDict, "uid"),
                        GID = GetString(sDict, "gid"),
                        Mode = GetNullableLong(sDict, "mode"),
                    });
                }
                else if (item is string s)
                {
                    result.Add(new ServiceSecretConfig { Source = s });
                }
            }

            return result;
        }

        private static List<ServiceConfigObjConfig> BindServiceConfigs(Dictionary<string, object?> dict, string key)
        {
            var result = new List<ServiceConfigObjConfig>();
            if (!dict.TryGetValue(key, out var obj) || obj is not List<object?> list)
                return result;

            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> cDict)
                {
                    result.Add(new ServiceConfigObjConfig
                    {
                        Source = GetString(cDict, "source") ?? "",
                        Target = GetString(cDict, "target"),
                        UID = GetString(cDict, "uid"),
                        GID = GetString(cDict, "gid"),
                        Mode = GetNullableLong(cDict, "mode"),
                    });
                }
                else if (item is string s)
                {
                    result.Add(new ServiceConfigObjConfig { Source = s });
                }
            }

            return result;
        }

        private static BuildConfig? BindBuildConfig(Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("build", out var buildObj) || buildObj is not Dictionary<string, object?> buildDict)
                return null;

            var cfg = new BuildConfig
            {
                Context = GetString(buildDict, "context"),
                Dockerfile = GetString(buildDict, "dockerfile"),
                DockerfileInline = GetString(buildDict, "dockerfile_inline"),
                Target = GetString(buildDict, "target"),
                Network = GetString(buildDict, "network"),
                Isolation = GetString(buildDict, "isolation"),
                NoCache = GetBool(buildDict, "no_cache"),
                Pull = GetBool(buildDict, "pull"),
                Privileged = GetBool(buildDict, "privileged"),
                Tags = GetStringList(buildDict, "tags"),
                Platforms = GetStringList(buildDict, "platforms"),
                CacheFrom = GetStringList(buildDict, "cache_from"),
                CacheTo = GetStringList(buildDict, "cache_to"),
                ExtraHosts = BindExtraHosts(buildDict, "extra_hosts"),
                Labels = BindLabels(buildDict, "labels"),
            };

            // Args
            if (buildDict.TryGetValue("args", out var argsObj))
            {
                cfg.Args = BindStringNullableDict(argsObj);
            }

            // SSH
            if (buildDict.TryGetValue("ssh", out var sshObj))
            {
                if (sshObj is Dictionary<string, object?> sshDict)
                {
                    cfg.SSH = SSHConfig.FromDictionary(sshDict);
                }
                else if (sshObj is List<object?> sshList)
                {
                    foreach (var item in sshList)
                        if (item != null) cfg.SSH.Add(SSHKey.Parse(item.ToString()!));
                }
            }

            // Additional contexts
            if (buildDict.TryGetValue("additional_contexts", out var acObj))
            {
                if (acObj is Dictionary<string, object?> acDict)
                {
                    foreach (var kvp in acDict)
                    {
                        cfg.AdditionalContexts[kvp.Key] = kvp.Value?.ToString() ?? "";
                    }
                }
                else if (acObj is List<object?> acList)
                {
                    foreach (var item in acList)
                    {
                        if (item != null)
                        {
                            var str = item.ToString()!;
                            var eqIdx = str.IndexOf('=');
                            if (eqIdx >= 0)
                            {
                                cfg.AdditionalContexts[str.Substring(0, eqIdx)] = str.Substring(eqIdx + 1);
                            }
                            else
                            {
                                cfg.AdditionalContexts[str] = str;
                            }
                        }
                    }
                }
            }

            // ShmSize
            if (buildDict.TryGetValue("shm_size", out var shmObj))
            {
                var shmStr = shmObj?.ToString();
                if (!string.IsNullOrEmpty(shmStr))
                {
                    if (UnitBytes.TryParse(shmStr, out var ub))
                        cfg.ShmSize = ub;
                    else if (long.TryParse(shmStr, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out var shmLong))
                        cfg.ShmSize = new UnitBytes(shmLong);
                }
            }

            // Secrets for build
            cfg.Secrets = BindServiceSecrets(buildDict, "secrets");

            // New build fields
            cfg.Entitlements = GetStringList(buildDict, "entitlements");
            cfg.Provenance = GetString(buildDict, "provenance");
            cfg.SBOM = GetString(buildDict, "sbom");
            cfg.NoCacheFilter = GetStringList(buildDict, "no_cache_filter");

            // Build ulimits
            if (buildDict.TryGetValue("ulimits", out var bUlObj) && bUlObj is Dictionary<string, object?> bUlDict)
            {
                foreach (var kvp in bUlDict)
                {
                    if (kvp.Value is Dictionary<string, object?> limDict)
                    {
                        cfg.Ulimits[kvp.Key] = new UlimitsConfig
                        {
                            Soft = GetInt(limDict, "soft"),
                            Hard = GetInt(limDict, "hard"),
                        };
                    }
                    else
                    {
                        var single = ToInt(kvp.Value);
                        cfg.Ulimits[kvp.Key] = new UlimitsConfig { Single = single };
                    }
                }
            }

            return cfg;
        }

        private static HealthCheckConfig? BindHealthCheck(Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("healthcheck", out var hcObj) || hcObj is not Dictionary<string, object?> hcDict)
                return null;

            var cfg = new HealthCheckConfig
            {
                Disable = GetBool(hcDict, "disable"),
            };

            // Retries as ulong?
            var retriesVal = GetNullableInt(hcDict, "retries");
            if (retriesVal.HasValue)
                cfg.Retries = (ulong)retriesVal.Value;

            // Test
            if (hcDict.TryGetValue("test", out var testObj))
            {
                if (testObj is string testStr)
                {
                    cfg.Test = new List<string> { "CMD-SHELL", testStr };
                }
                else if (testObj is List<object?> testList)
                {
                    cfg.Test = testList.Select(x => x?.ToString() ?? "").ToList();
                }
            }

            // Intervals
            var interval = GetString(hcDict, "interval");
            if (!string.IsNullOrEmpty(interval) && Duration.TryParse(interval, out var iv))
                cfg.Interval = iv;

            var timeout = GetString(hcDict, "timeout");
            if (!string.IsNullOrEmpty(timeout) && Duration.TryParse(timeout, out var to))
                cfg.Timeout = to;

            var startPeriod = GetString(hcDict, "start_period");
            if (!string.IsNullOrEmpty(startPeriod) && Duration.TryParse(startPeriod, out var sp))
                cfg.StartPeriod = sp;

            var startInterval = GetString(hcDict, "start_interval");
            if (!string.IsNullOrEmpty(startInterval) && Duration.TryParse(startInterval, out var si))
                cfg.StartInterval = si;

            return cfg;
        }

        private static LoggingConfig? BindLogging(Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("logging", out var logObj) || logObj is not Dictionary<string, object?> logDict)
                return null;

            return new LoggingConfig
            {
                Driver = GetString(logDict, "driver"),
                Options = BindStringDict(logDict, "options"),
            };
        }

        private static DeployConfig? BindDeploy(Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("deploy", out var deployObj) || deployObj is not Dictionary<string, object?> deployDict)
                return null;

            var cfg = new DeployConfig
            {
                Mode = GetString(deployDict, "mode"),
                Replicas = GetNullableInt(deployDict, "replicas"),
                EndpointMode = GetString(deployDict, "endpoint_mode"),
            };

            // Labels
            cfg.Labels = BindLabels(deployDict, "labels");

            // UpdateConfig
            cfg.UpdateConfig = BindUpdateConfig(deployDict, "update_config");
            cfg.RollbackConfig = BindUpdateConfig(deployDict, "rollback_config");

            // Resources
            if (deployDict.TryGetValue("resources", out var resObj) && resObj is Dictionary<string, object?> resDict)
            {
                cfg.Resources = new Resources();
                if (resDict.TryGetValue("limits", out var limObj) && limObj is Dictionary<string, object?> limDict)
                {
                    cfg.Resources.Limits = BindResource(limDict);
                }
                if (resDict.TryGetValue("reservations", out var revObj) && revObj is Dictionary<string, object?> revDict)
                {
                    cfg.Resources.Reservations = BindResource(revDict);
                }
            }

            // RestartPolicy
            if (deployDict.TryGetValue("restart_policy", out var rpObj) && rpObj is Dictionary<string, object?> rpDict)
            {
                cfg.RestartPolicy = new RestartPolicy
                {
                    Condition = GetString(rpDict, "condition"),
                };
                var delay = GetString(rpDict, "delay");
                if (!string.IsNullOrEmpty(delay) && Duration.TryParse(delay, out var d))
                    cfg.RestartPolicy.Delay = d;

                var maxAttempts = GetNullableLong(rpDict, "max_attempts");
                if (maxAttempts.HasValue)
                    cfg.RestartPolicy.MaxAttempts = (ulong)maxAttempts.Value;

                var window = GetString(rpDict, "window");
                if (!string.IsNullOrEmpty(window) && Duration.TryParse(window, out var w))
                    cfg.RestartPolicy.Window = w;
            }

            // Placement
            if (deployDict.TryGetValue("placement", out var plObj) && plObj is Dictionary<string, object?> plDict)
            {
                cfg.Placement = new Placement
                {
                    Constraints = GetStringList(plDict, "constraints"),
                };
                var maxReplicas = GetLong(plDict, "max_replicas_per_node");
                if (maxReplicas > 0)
                    cfg.Placement.MaxReplicas = (ulong)maxReplicas;

                if (plDict.TryGetValue("preferences", out var prefsObj) && prefsObj is List<object?> prefsList)
                {
                    foreach (var item in prefsList)
                    {
                        if (item is Dictionary<string, object?> prefDict)
                        {
                            cfg.Placement.Preferences.Add(new PlacementPreferences
                            {
                                Spread = GetString(prefDict, "spread"),
                            });
                        }
                    }
                }
            }

            return cfg;
        }

        private static UpdateConfig? BindUpdateConfig(Dictionary<string, object?> parent, string key)
        {
            if (!parent.TryGetValue(key, out var obj) || obj is not Dictionary<string, object?> dict)
                return null;

            var cfg = new UpdateConfig
            {
                FailureAction = GetString(dict, "failure_action"),
                Order = GetString(dict, "order"),
                MaxFailureRatio = GetFloat(dict, "max_failure_ratio"),
            };

            var parallelism = GetNullableLong(dict, "parallelism");
            if (parallelism.HasValue)
                cfg.Parallelism = (ulong)parallelism.Value;

            var delay = GetString(dict, "delay");
            if (!string.IsNullOrEmpty(delay) && Duration.TryParse(delay, out var d))
                cfg.Delay = d;

            var monitor = GetString(dict, "monitor");
            if (!string.IsNullOrEmpty(monitor) && Duration.TryParse(monitor, out var m))
                cfg.Monitor = m;

            return cfg;
        }

        private static Resource BindResource(Dictionary<string, object?> dict)
        {
            var res = new Resource
            {
                NanoCPUs = GetFloat(dict, "cpus"),
                Pids = GetLong(dict, "pids"),
            };

            var mem = GetString(dict, "memory");
            if (!string.IsNullOrEmpty(mem))
            {
                if (UnitBytes.TryParse(mem, out var ub))
                    res.MemoryBytes = ub;
            }

            // Devices (GPU requests)
            if (dict.TryGetValue("devices", out var devObj) && devObj is List<object?> devList)
            {
                foreach (var item in devList)
                {
                    if (item is Dictionary<string, object?> devDict)
                    {
                        var dr = new DeviceRequest
                        {
                            Driver = GetString(devDict, "driver"),
                            Count = GetLong(devDict, "count"),
                            DeviceIDs = GetStringList(devDict, "device_ids"),
                            Options = BindStringDict(devDict, "options"),
                        };
                        if (devDict.TryGetValue("capabilities", out var capObj) && capObj is List<object?> capList)
                        {
                            dr.Capabilities = capList.Select(c => c?.ToString() ?? "").ToList();
                        }
                        res.Devices.Add(dr);
                    }
                }
            }

            return res;
        }

        private static Dictionary<string, UlimitsConfig> BindUlimits(Dictionary<string, object?> dict)
        {
            var result = new Dictionary<string, UlimitsConfig>();
            if (!dict.TryGetValue("ulimits", out var ulObj) || ulObj is not Dictionary<string, object?> ulDict)
                return result;

            foreach (var kvp in ulDict)
            {
                if (kvp.Value is Dictionary<string, object?> limDict)
                {
                    result[kvp.Key] = new UlimitsConfig
                    {
                        Soft = GetInt(limDict, "soft"),
                        Hard = GetInt(limDict, "hard"),
                    };
                }
                else
                {
                    var single = ToInt(kvp.Value);
                    result[kvp.Key] = new UlimitsConfig { Single = single };
                }
            }

            return result;
        }

        private static List<EnvFile> BindEnvFiles(Dictionary<string, object?> dict)
        {
            var result = new List<EnvFile>();
            if (!dict.TryGetValue("env_file", out var efObj))
                return result;

            if (efObj is List<object?> list)
            {
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object?> efDict)
                    {
                        result.Add(new EnvFile
                        {
                            Path = GetString(efDict, "path") ?? "",
                            Required = GetBool(efDict, "required", true),
                            Format = GetString(efDict, "format"),
                        });
                    }
                    else if (item is string s)
                    {
                        result.Add(new EnvFile { Path = s });
                    }
                }
            }
            else if (efObj is string single)
            {
                result.Add(new EnvFile { Path = single });
            }

            return result;
        }

        private static List<DeviceMapping> BindDevices(Dictionary<string, object?> dict)
        {
            var result = new List<DeviceMapping>();
            if (!dict.TryGetValue("devices", out var devObj) || devObj is not List<object?> list)
                return result;

            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> devDict)
                {
                    result.Add(new DeviceMapping
                    {
                        Source = GetString(devDict, "source") ?? "",
                        Target = GetString(devDict, "target") ?? "",
                        Permissions = GetString(devDict, "permissions"),
                    });
                }
                else if (item is string s)
                {
                    // Should already be transformed to dict, but handle string fallback
                    var parts = s.Split(':');
                    var dm = new DeviceMapping();
                    if (parts.Length >= 1) dm.Source = parts[0];
                    if (parts.Length >= 2) dm.Target = parts[1];
                    if (parts.Length >= 3) dm.Permissions = parts[2];
                    else dm.Permissions = "rwm";
                    if (string.IsNullOrEmpty(dm.Target)) dm.Target = dm.Source;
                    result.Add(dm);
                }
            }

            return result;
        }

        private static BlkioConfig? BindBlkioConfig(Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("blkio_config", out var blkObj) || blkObj is not Dictionary<string, object?> blkDict)
                return null;

            var cfg = new BlkioConfig
            {
                Weight = (ushort)GetInt(blkDict, "weight"),
            };

            cfg.WeightDevice = BindWeightDevices(blkDict, "weight_device");
            cfg.DeviceReadBps = BindThrottleDevices(blkDict, "device_read_bps");
            cfg.DeviceReadIOps = BindThrottleDevices(blkDict, "device_read_iops");
            cfg.DeviceWriteBps = BindThrottleDevices(blkDict, "device_write_bps");
            cfg.DeviceWriteIOps = BindThrottleDevices(blkDict, "device_write_iops");

            return cfg;
        }

        private static List<WeightDevice> BindWeightDevices(Dictionary<string, object?> dict, string key)
        {
            var result = new List<WeightDevice>();
            if (!dict.TryGetValue(key, out var obj) || obj is not List<object?> list)
                return result;

            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> d)
                {
                    result.Add(new WeightDevice
                    {
                        Path = GetString(d, "path") ?? "",
                        Weight = (ushort)GetInt(d, "weight"),
                    });
                }
            }

            return result;
        }

        private static List<ThrottleDevice> BindThrottleDevices(Dictionary<string, object?> dict, string key)
        {
            var result = new List<ThrottleDevice>();
            if (!dict.TryGetValue(key, out var obj) || obj is not List<object?> list)
                return result;

            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> d)
                {
                    result.Add(new ThrottleDevice
                    {
                        Path = GetString(d, "path") ?? "",
                        Rate = BindUnitBytes(d, "rate"),
                    });
                }
            }

            return result;
        }

        #endregion

        #region Develop, Provider, Models, Hooks, Gpus binding

        private static DevelopConfig? BindDevelopConfig(Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("develop", out var devObj) || devObj is not Dictionary<string, object?> devDict)
                return null;

            var cfg = new DevelopConfig
            {
                Extensions = BindExtensions(devDict),
            };

            if (devDict.TryGetValue("watch", out var watchObj) && watchObj is List<object?> watchList)
            {
                foreach (var item in watchList)
                {
                    if (item is Dictionary<string, object?> trigDict)
                    {
                        var trigger = new Trigger
                        {
                            Path = GetString(trigDict, "path") ?? "",
                            Action = GetString(trigDict, "action") ?? "",
                            Target = GetString(trigDict, "target"),
                            InitialSync = GetBool(trigDict, "initial_sync"),
                            Include = GetStringList(trigDict, "include"),
                            Ignore = GetStringList(trigDict, "ignore"),
                            Extensions = BindExtensions(trigDict),
                        };

                        // Exec hook
                        if (trigDict.TryGetValue("exec", out var execObj) && execObj is Dictionary<string, object?> execDict)
                        {
                            trigger.Exec = BindSingleServiceHook(execDict);
                        }

                        cfg.Watch.Add(trigger);
                    }
                }
            }

            return cfg;
        }

        private static ServiceProviderConfig? BindProviderConfig(Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("provider", out var provObj) || provObj is not Dictionary<string, object?> provDict)
                return null;

            var cfg = new ServiceProviderConfig
            {
                Type = GetString(provDict, "type"),
                Extensions = BindExtensions(provDict),
            };

            if (provDict.TryGetValue("options", out var optsObj) && optsObj is Dictionary<string, object?> optsDict)
            {
                foreach (var kvp in optsDict)
                {
                    var values = new List<string>();
                    if (kvp.Value is List<object?> list)
                    {
                        foreach (var item in list)
                        {
                            if (item != null) values.Add(item.ToString()!);
                        }
                    }
                    else if (kvp.Value != null)
                    {
                        values.Add(kvp.Value.ToString()!);
                    }
                    cfg.Options[kvp.Key] = values;
                }
            }

            return cfg;
        }

        private static Dictionary<string, ServiceModelConfig?> BindServiceModels(Dictionary<string, object?> dict)
        {
            var result = new Dictionary<string, ServiceModelConfig?>();
            if (!dict.TryGetValue("models", out var modObj))
                return result;

            if (modObj is Dictionary<string, object?> modDict)
            {
                foreach (var kvp in modDict)
                {
                    if (kvp.Value is Dictionary<string, object?> mDict)
                    {
                        result[kvp.Key] = new ServiceModelConfig
                        {
                            EndpointVariable = GetString(mDict, "endpoint_variable"),
                            ModelVariable = GetString(mDict, "model_variable"),
                            Extensions = BindExtensions(mDict),
                        };
                    }
                    else
                    {
                        result[kvp.Key] = null;
                    }
                }
            }

            return result;
        }

        private static List<ServiceHook> BindServiceHooks(Dictionary<string, object?> dict, string key)
        {
            var result = new List<ServiceHook>();
            if (!dict.TryGetValue(key, out var obj) || obj is not List<object?> list)
                return result;

            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> hookDict)
                {
                    result.Add(BindSingleServiceHook(hookDict));
                }
            }

            return result;
        }

        private static ServiceHook BindSingleServiceHook(Dictionary<string, object?> hookDict)
        {
            return new ServiceHook
            {
                Command = BindShellCommand(hookDict, "command"),
                User = GetString(hookDict, "user"),
                Privileged = GetBool(hookDict, "privileged"),
                WorkingDir = GetString(hookDict, "working_dir"),
                Environment = BindMappingWithEquals(hookDict, "environment"),
                Extensions = BindExtensions(hookDict),
            };
        }

        private static List<DeviceRequest> BindGpus(Dictionary<string, object?> dict)
        {
            var result = new List<DeviceRequest>();
            if (!dict.TryGetValue("gpus", out var gpuObj) || gpuObj is not List<object?> gpuList)
                return result;

            foreach (var item in gpuList)
            {
                if (item is Dictionary<string, object?> gpuDict)
                {
                    var dr = new DeviceRequest
                    {
                        Driver = GetString(gpuDict, "driver"),
                        Count = GetLong(gpuDict, "count"),
                        DeviceIDs = GetStringList(gpuDict, "device_ids"),
                        Options = BindStringDict(gpuDict, "options"),
                    };
                    if (gpuDict.TryGetValue("capabilities", out var capObj) && capObj is List<object?> capList)
                    {
                        dr.Capabilities = capList.Select(c => c?.ToString() ?? "").ToList();
                    }
                    result.Add(dr);
                }
            }

            return result;
        }

        private static ModelConfig BindModelConfig(object? value)
        {
            if (value is not Dictionary<string, object?> dict)
                return new ModelConfig();

            return new ModelConfig
            {
                Name = GetString(dict, "name"),
                Model = GetString(dict, "model"),
                ContextSize = GetInt(dict, "context_size"),
                RuntimeFlags = GetStringList(dict, "runtime_flags"),
                Extensions = BindExtensions(dict),
            };
        }

        #endregion

        #region Top-level resource binding

        private static NetworkConfig BindNetworkConfig(object? value)
        {
            if (value is not Dictionary<string, object?> dict)
                return new NetworkConfig();

            var cfg = new NetworkConfig
            {
                Name = GetString(dict, "name"),
                Driver = GetString(dict, "driver"),
                External = GetBool(dict, "external"),
                Internal = GetBool(dict, "internal"),
                Attachable = GetBool(dict, "attachable"),
                EnableIPv4 = GetNullableBool(dict, "enable_ipv4"),
                EnableIPv6 = GetNullableBool(dict, "enable_ipv6"),
                Labels = BindLabels(dict, "labels"),
                DriverOpts = BindStringDict(dict, "driver_opts"),
                Extensions = BindExtensions(dict),
            };

            // IPAM
            if (dict.TryGetValue("ipam", out var ipamObj) && ipamObj is Dictionary<string, object?> ipamDict)
            {
                cfg.Ipam = new IPAMConfig
                {
                    Driver = GetString(ipamDict, "driver"),
                };

                if (ipamDict.TryGetValue("config", out var ipamCfgObj) && ipamCfgObj is List<object?> ipamCfgList)
                {
                    foreach (var item in ipamCfgList)
                    {
                        if (item is Dictionary<string, object?> poolDict)
                        {
                            cfg.Ipam.Config.Add(new IPAMPool
                            {
                                Subnet = GetString(poolDict, "subnet"),
                                Gateway = GetString(poolDict, "gateway"),
                                IPRange = GetString(poolDict, "ip_range"),
                            });
                        }
                    }
                }
            }

            return cfg;
        }

        private static VolumeConfig BindVolumeConfig(object? value)
        {
            if (value is not Dictionary<string, object?> dict)
                return new VolumeConfig();

            return new VolumeConfig
            {
                Name = GetString(dict, "name"),
                Driver = GetString(dict, "driver"),
                External = GetBool(dict, "external"),
                Labels = BindLabels(dict, "labels"),
                DriverOpts = BindStringDict(dict, "driver_opts"),
                Extensions = BindExtensions(dict),
            };
        }

        private static SecretConfig BindSecretConfig(object? value)
        {
            if (value is not Dictionary<string, object?> dict)
                return new SecretConfig();

            return new SecretConfig
            {
                Name = GetString(dict, "name"),
                File = GetString(dict, "file"),
                Environment = GetString(dict, "environment"),
                Content = GetString(dict, "content"),
                External = GetBool(dict, "external"),
                Driver = GetString(dict, "driver"),
                TemplateDriver = GetString(dict, "template_driver"),
                Labels = BindLabels(dict, "labels"),
                DriverOpts = BindStringDict(dict, "driver_opts"),
                Extensions = BindExtensions(dict),
            };
        }

        private static ConfigObjConfig BindConfigObjConfig(object? value)
        {
            if (value is not Dictionary<string, object?> dict)
                return new ConfigObjConfig();

            return new ConfigObjConfig
            {
                Name = GetString(dict, "name"),
                File = GetString(dict, "file"),
                Environment = GetString(dict, "environment"),
                Content = GetString(dict, "content"),
                External = GetBool(dict, "external"),
                Driver = GetString(dict, "driver"),
                TemplateDriver = GetString(dict, "template_driver"),
                Labels = BindLabels(dict, "labels"),
                DriverOpts = BindStringDict(dict, "driver_opts"),
                Extensions = BindExtensions(dict),
            };
        }

        #endregion

        #region Helper binding methods

        private static ShellCommand? BindShellCommand(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return null;

            if (obj is string s)
                return ShellCommand.FromShellForm(s);

            if (obj is List<object?> list)
            {
                var parts = list.Where(x => x != null).Select(x => x!.ToString()!).ToList();
                if (parts.Count > 0)
                    return ShellCommand.FromExecForm(parts);
            }

            return null;
        }

        private static UnitBytes BindUnitBytes(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return UnitBytes.Zero;

            if (obj is int i) return new UnitBytes(i);
            if (obj is long l) return new UnitBytes(l);

            var str = obj.ToString();
            if (!string.IsNullOrEmpty(str))
            {
                if (UnitBytes.TryParse(str, out var ub))
                    return ub;
                if (long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longVal))
                    return new UnitBytes(longVal);
            }

            return UnitBytes.Zero;
        }

        private static MappingWithEquals BindMappingWithEquals(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return new MappingWithEquals();

            if (obj is Dictionary<string, object?> d)
            {
                var result = new MappingWithEquals();
                foreach (var kvp in d)
                {
                    result[kvp.Key] = kvp.Value?.ToString();
                }
                return result;
            }

            if (obj is List<object?> list)
            {
                var strings = list.Where(x => x != null).Select(x => x!.ToString()!);
                return MappingWithEquals.NewMappingWithEquals(strings);
            }

            return new MappingWithEquals();
        }

        private static Mapping BindMapping(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return new Mapping();

            if (obj is Dictionary<string, object?> d)
            {
                var result = new Mapping();
                foreach (var kvp in d)
                {
                    result[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
                return result;
            }

            if (obj is List<object?> list)
            {
                var strings = list.Where(x => x != null).Select(x => x!.ToString()!);
                return Mapping.NewMapping(strings);
            }

            return new Mapping();
        }

        private static Labels BindLabels(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return new Labels();

            if (obj is Dictionary<string, object?> d)
            {
                var labels = new Labels();
                foreach (var kvp in d)
                {
                    labels[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
                return labels;
            }

            if (obj is List<object?> list)
            {
                var strings = list.Where(x => x != null).Select(x => x!.ToString()!);
                return Labels.NewLabels(strings);
            }

            return new Labels();
        }

        private static HostsList BindExtraHosts(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return new HostsList();

            if (obj is Dictionary<string, object?> d)
            {
                var mapping = new Dictionary<string, string>();
                foreach (var kvp in d)
                {
                    mapping[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
                return HostsList.FromMapping(mapping);
            }

            if (obj is List<object?> list)
            {
                var strings = list.Where(x => x != null).Select(x => x!.ToString()!);
                return HostsList.FromStrings(strings);
            }

            return new HostsList();
        }

        private static StringOrNumberList BindStringOrNumberList(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return new StringOrNumberList();

            if (obj is List<object?> list)
            {
                var result = new StringOrNumberList();
                foreach (var item in list)
                {
                    if (item != null) result.AddObject(item);
                }
                return result;
            }

            return new StringOrNumberList();
        }

        private static Dictionary<string, string> BindStringDict(Dictionary<string, object?> dict, string key)
        {
            var result = new Dictionary<string, string>();
            if (!dict.TryGetValue(key, out var obj) || obj is not Dictionary<string, object?> d)
                return result;

            foreach (var kvp in d)
            {
                result[kvp.Key] = kvp.Value?.ToString() ?? "";
            }

            return result;
        }

        private static Dictionary<string, string?> BindStringNullableDict(object? obj)
        {
            var result = new Dictionary<string, string?>();
            if (obj is Dictionary<string, object?> d)
            {
                foreach (var kvp in d)
                {
                    result[kvp.Key] = kvp.Value?.ToString();
                }
            }
            else if (obj is List<object?> list)
            {
                foreach (var item in list)
                {
                    var s = item?.ToString() ?? "";
                    var eqIdx = s.IndexOf('=');
                    if (eqIdx < 0)
                        result[s] = null;
                    else
                        result[s.Substring(0, eqIdx)] = s.Substring(eqIdx + 1);
                }
            }

            return result;
        }

        private static Extensions BindExtensions(Dictionary<string, object?> dict)
        {
            var ext = new Extensions();
            foreach (var kvp in dict)
            {
                if (kvp.Key.StartsWith("x-", StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                {
                    ext[kvp.Key] = kvp.Value;
                }
            }
            return ext;
        }

        #endregion

        #region Primitive helpers

        private static string? GetString(Dictionary<string, object?> dict, string key)
        {
            if (dict.TryGetValue(key, out var obj) && obj != null)
                return obj.ToString();
            return null;
        }

        private static bool GetBool(Dictionary<string, object?> dict, string key, bool defaultValue = false)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return defaultValue;

            if (obj is bool b) return b;

            var str = obj.ToString();
            if (str != null)
            {
                switch (str.ToLowerInvariant())
                {
                    case "true": case "yes": case "1": case "on": return true;
                    case "false": case "no": case "0": case "off": return false;
                }
            }

            return defaultValue;
        }

        private static bool? GetNullableBool(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return null;

            if (obj is bool b) return b;

            var str = obj.ToString();
            if (str != null)
            {
                switch (str.ToLowerInvariant())
                {
                    case "true": case "yes": case "1": case "on": return true;
                    case "false": case "no": case "0": case "off": return false;
                }
            }

            return null;
        }

        private static int GetInt(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return 0;

            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is double d) return (int)d;

            if (int.TryParse(obj.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
            return 0;
        }

        private static int? GetNullableInt(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return null;

            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is double d) return (int)d;

            if (int.TryParse(obj.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
            return null;
        }

        private static long GetLong(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return 0;

            if (obj is long l) return l;
            if (obj is int i) return i;
            if (obj is double d) return (long)d;

            if (long.TryParse(obj.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
            return 0;
        }

        private static UnitBytes GetUnitBytes(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return UnitBytes.Zero;

            if (obj is long l) return new UnitBytes(l);
            if (obj is int i) return new UnitBytes(i);
            if (obj is double d) return new UnitBytes((long)d);

            var str = obj.ToString();
            if (str != null && UnitBytes.TryParse(str, out var result))
                return result;

            return UnitBytes.Zero;
        }

        private static long? GetNullableLong(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return null;

            if (obj is long l) return l;
            if (obj is int i) return i;
            if (obj is double d) return (long)d;

            if (long.TryParse(obj.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
            return null;
        }

        private static float GetFloat(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return 0;

            if (obj is float f) return f;
            if (obj is double d) return (float)d;
            if (obj is int i) return i;
            if (obj is long l) return l;

            if (float.TryParse(obj.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;
            return 0;
        }

        private static List<string> GetStringList(Dictionary<string, object?> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj) || obj == null)
                return new List<string>();

            if (obj is List<object?> list)
            {
                return list.Where(x => x != null).Select(x => x!.ToString()!).ToList();
            }

            if (obj is string s)
            {
                return new List<string> { s };
            }

            return new List<string>();
        }

        private static int ToInt(object? obj)
        {
            if (obj == null) return 0;
            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is double d) return (int)d;
            if (int.TryParse(obj.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
            return 0;
        }

        #endregion
    }
}
