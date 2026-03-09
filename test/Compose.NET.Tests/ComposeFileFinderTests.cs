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
using Compose.NET.Loader;
using Xunit;

namespace Compose.NET.Tests;

/// <summary>
/// Tests for <see cref="ComposeFileFinder"/>.
/// Uses temporary directories with known file layouts to exercise all discovery paths.
/// </summary>
public class ComposeFileFinderTests : IDisposable
{
    private readonly string _tempRoot;

    public ComposeFileFinderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"compose_finder_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    /// <summary>
    /// Creates a file with minimal content in the temp hierarchy.
    /// </summary>
    private string CreateFile(string relativePath, string content = "services:\n  web:\n    image: nginx\n")
    {
        var fullPath = Path.GetFullPath(Path.Combine(_tempRoot, relativePath));
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Constants
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void DefaultFileNames_HasCorrectPriorityOrder()
    {
        Assert.Equal(4, ComposeFileFinder.DefaultFileNames.Count);
        Assert.Equal("compose.yaml", ComposeFileFinder.DefaultFileNames[0]);
        Assert.Equal("compose.yml", ComposeFileFinder.DefaultFileNames[1]);
        Assert.Equal("docker-compose.yml", ComposeFileFinder.DefaultFileNames[2]);
        Assert.Equal("docker-compose.yaml", ComposeFileFinder.DefaultFileNames[3]);
    }

    [Fact]
    public void DefaultOverrideFileNames_HasCorrectPriorityOrder()
    {
        Assert.Equal(4, ComposeFileFinder.DefaultOverrideFileNames.Count);
        Assert.Equal("compose.override.yml", ComposeFileFinder.DefaultOverrideFileNames[0]);
        Assert.Equal("compose.override.yaml", ComposeFileFinder.DefaultOverrideFileNames[1]);
        Assert.Equal("docker-compose.override.yml", ComposeFileFinder.DefaultOverrideFileNames[2]);
        Assert.Equal("docker-compose.override.yaml", ComposeFileFinder.DefaultOverrideFileNames[3]);
    }

    [Fact]
    public void EnvVarConstants_CorrectNames()
    {
        Assert.Equal("COMPOSE_FILE", ComposeFileFinder.ComposeFileEnvVar);
        Assert.Equal("COMPOSE_PATH_SEPARATOR", ComposeFileFinder.ComposePathSeparatorEnvVar);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // FindFiles
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FindFiles_NoFilesExist_ReturnsEmpty()
    {
        var dir = Path.Combine(_tempRoot, "empty");
        Directory.CreateDirectory(dir);
        var found = ComposeFileFinder.FindFiles(ComposeFileFinder.DefaultFileNames, dir);
        Assert.Empty(found);
    }

    [Fact]
    public void FindFiles_SingleMatch_ReturnsSinglePath()
    {
        var dir = Path.Combine(_tempRoot, "single");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "compose.yaml"), "services: {}");

        var found = ComposeFileFinder.FindFiles(ComposeFileFinder.DefaultFileNames, dir);
        Assert.Single(found);
        Assert.EndsWith("compose.yaml", found[0]);
    }

    [Fact]
    public void FindFiles_MultipleMatches_ReturnsInPriorityOrder()
    {
        var dir = Path.Combine(_tempRoot, "multi");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "docker-compose.yml"), "services: {}");
        File.WriteAllText(Path.Combine(dir, "compose.yaml"), "services: {}");

        var found = ComposeFileFinder.FindFiles(ComposeFileFinder.DefaultFileNames, dir);
        Assert.Equal(2, found.Count);
        // compose.yaml has higher priority
        Assert.EndsWith("compose.yaml", found[0]);
        Assert.EndsWith("docker-compose.yml", found[1]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // DiscoverDefaultFiles â€” basic discovery
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void DiscoverDefaultFiles_ComposeYaml_Found()
    {
        var file = CreateFile("project/compose.yaml");
        var dir = Path.GetDirectoryName(file)!;

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
        Assert.Equal(dir, result.WorkingDir);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void DiscoverDefaultFiles_ComposeYml_Found()
    {
        var file = CreateFile("project2/compose.yml");
        var dir = Path.GetDirectoryName(file)!;

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
    }

    [Fact]
    public void DiscoverDefaultFiles_DockerComposeYml_Found()
    {
        var file = CreateFile("project3/docker-compose.yml");
        var dir = Path.GetDirectoryName(file)!;

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
    }

    [Fact]
    public void DiscoverDefaultFiles_DockerComposeYaml_Found()
    {
        var file = CreateFile("project4/docker-compose.yaml");
        var dir = Path.GetDirectoryName(file)!;

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Priority â€” compose.yaml wins over docker-compose.yml
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void DiscoverDefaultFiles_MultipleFiles_UsesHighestPriority()
    {
        CreateFile("priority/compose.yaml");
        CreateFile("priority/docker-compose.yml");
        var dir = Path.Combine(_tempRoot, "priority");

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Single(result.ConfigPaths);
        Assert.EndsWith("compose.yaml", result.ConfigPaths[0]);
    }

    [Fact]
    public void DiscoverDefaultFiles_MultipleFiles_EmitsWarning()
    {
        CreateFile("warn/compose.yaml");
        CreateFile("warn/docker-compose.yml");
        var dir = Path.Combine(_tempRoot, "warn");

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("Found multiple config files"));
        Assert.Contains(result.Warnings, w => w.Contains("Using compose.yaml"));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Parent directory traversal
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void DiscoverDefaultFiles_WalksUpToParentDirectory()
    {
        var file = CreateFile("parent_project/compose.yaml");
        var childDir = Path.Combine(_tempRoot, "parent_project", "sub", "deep");
        Directory.CreateDirectory(childDir);

        var result = ComposeFileFinder.DiscoverDefaultFiles(childDir);
        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
        Assert.Equal(Path.Combine(_tempRoot, "parent_project"), result.WorkingDir);
    }

    [Fact]
    public void DiscoverDefaultFiles_NoFileFound_ReturnsEmpty()
    {
        var dir = Path.Combine(_tempRoot, "empty_tree", "sub", "deep");
        Directory.CreateDirectory(dir);

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Empty(result.ConfigPaths);
        // WorkingDir should be set to the original startDir
        Assert.Equal(dir, result.WorkingDir);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Override file discovery
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void DiscoverDefaultFiles_FindsOverrideFile()
    {
        CreateFile("override_project/compose.yaml");
        CreateFile("override_project/compose.override.yml");
        var dir = Path.Combine(_tempRoot, "override_project");

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Equal(2, result.ConfigPaths.Count);
        Assert.EndsWith("compose.yaml", result.ConfigPaths[0]);
        Assert.EndsWith("compose.override.yml", result.ConfigPaths[1]);
    }

    [Fact]
    public void DiscoverDefaultFiles_DockerComposeOverride()
    {
        CreateFile("dc_override/docker-compose.yml");
        CreateFile("dc_override/docker-compose.override.yml");
        var dir = Path.Combine(_tempRoot, "dc_override");

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Equal(2, result.ConfigPaths.Count);
        Assert.EndsWith("docker-compose.yml", result.ConfigPaths[0]);
        Assert.EndsWith("docker-compose.override.yml", result.ConfigPaths[1]);
    }

    [Fact]
    public void DiscoverDefaultFiles_OverrideNotInParent()
    {
        // Override file is in the parent, but main file is also in the parent.
        // Override files should ONLY be found in the same directory as the main file.
        CreateFile("parent_override/compose.yaml");
        // Override in child directory should be ignored
        CreateFile("parent_override/child/compose.override.yml");
        var childDir = Path.Combine(_tempRoot, "parent_override", "child");

        var result = ComposeFileFinder.DiscoverDefaultFiles(childDir);
        // Should find compose.yaml from parent, but NOT the override from child
        // because the child has no main compose file
        Assert.Single(result.ConfigPaths);
        Assert.EndsWith("compose.yaml", result.ConfigPaths[0]);
    }

    [Fact]
    public void DiscoverDefaultFiles_OverridePriority_ComposeTakesPrecedence()
    {
        CreateFile("override_prio/compose.yaml");
        CreateFile("override_prio/compose.override.yml");
        CreateFile("override_prio/compose.override.yaml");
        var dir = Path.Combine(_tempRoot, "override_prio");

        var result = ComposeFileFinder.DiscoverDefaultFiles(dir);
        Assert.Equal(2, result.ConfigPaths.Count);
        // compose.override.yml has higher priority than compose.override.yaml
        Assert.EndsWith("compose.override.yml", result.ConfigPaths[1]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Discover() â€” explicit paths
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Discover_ExplicitPaths_UsesThemDirectly()
    {
        var file = CreateFile("explicit/my-compose.yaml");

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            explicitPaths: new[] { file });

        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
    }

    [Fact]
    public void Discover_ExplicitRelativePath_ResolvesToAbsolute()
    {
        CreateFile("explicit_rel/compose.yaml");
        var dir = Path.Combine(_tempRoot, "explicit_rel");

        var result = ComposeFileFinder.Discover(
            workingDir: dir,
            explicitPaths: new[] { "compose.yaml" });

        Assert.Single(result.ConfigPaths);
        Assert.True(Path.IsPathRooted(result.ConfigPaths[0]));
        Assert.EndsWith("compose.yaml", result.ConfigPaths[0]);
    }

    [Fact]
    public void Discover_ExplicitPath_NotFound_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ComposeFileFinder.Discover(
                workingDir: _tempRoot,
                explicitPaths: new[] { "nonexistent.yaml" }));
    }

    [Fact]
    public void Discover_MultipleExplicitPaths()
    {
        var file1 = CreateFile("multi_explicit/compose.yaml");
        var file2 = CreateFile("multi_explicit/compose.prod.yaml");

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            explicitPaths: new[] { file1, file2 });

        Assert.Equal(2, result.ConfigPaths.Count);
        Assert.Equal(file1, result.ConfigPaths[0]);
        Assert.Equal(file2, result.ConfigPaths[1]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Discover() â€” COMPOSE_FILE env var
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Discover_ComposeFileEnvVar_SingleFile()
    {
        var file = CreateFile("envvar/compose.yaml");
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = file,
        };

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            environment: env);

        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
    }

    [Fact]
    public void Discover_ComposeFileEnvVar_MultipleFiles()
    {
        var file1 = CreateFile("envvar_multi/compose.yaml");
        var file2 = CreateFile("envvar_multi/compose.prod.yaml");
        var sep = Path.PathSeparator.ToString();
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = $"{file1}{sep}{file2}",
        };

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            environment: env);

        Assert.Equal(2, result.ConfigPaths.Count);
        Assert.Equal(file1, result.ConfigPaths[0]);
        Assert.Equal(file2, result.ConfigPaths[1]);
    }

    [Fact]
    public void Discover_ComposeFileEnvVar_CustomSeparator()
    {
        var file1 = CreateFile("envvar_sep/compose.yaml");
        var file2 = CreateFile("envvar_sep/compose.prod.yaml");
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = $"{file1}|{file2}",
            [ComposeFileFinder.ComposePathSeparatorEnvVar] = "|",
        };

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            environment: env);

        Assert.Equal(2, result.ConfigPaths.Count);
    }

    [Fact]
    public void Discover_ComposeFileEnvVar_RelativePath()
    {
        CreateFile("envvar_rel/compose.yaml");
        var dir = Path.Combine(_tempRoot, "envvar_rel");
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = "compose.yaml",
        };

        var result = ComposeFileFinder.Discover(
            workingDir: dir,
            environment: env);

        Assert.Single(result.ConfigPaths);
        Assert.True(Path.IsPathRooted(result.ConfigPaths[0]));
    }

    [Fact]
    public void Discover_ComposeFileEnvVar_NotFound_Throws()
    {
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = "nonexistent.yaml",
        };

        Assert.Throws<FileNotFoundException>(() =>
            ComposeFileFinder.Discover(
                workingDir: _tempRoot,
                environment: env));
    }

    [Fact]
    public void Discover_ComposeFileEnvVar_StdinMarker()
    {
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = "-",
        };

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            environment: env);

        Assert.Single(result.ConfigPaths);
        Assert.Equal("-", result.ConfigPaths[0]);
    }

    [Fact]
    public void Discover_ComposeFileEnvVar_NoOverrideDiscovery()
    {
        // When COMPOSE_FILE is set, override files should NOT be discovered
        var file = CreateFile("envvar_no_override/compose.yaml");
        CreateFile("envvar_no_override/compose.override.yml");
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = file,
        };

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            environment: env);

        // Only the explicitly specified file, no override
        Assert.Single(result.ConfigPaths);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Discover() â€” priority: explicit > env > default
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Discover_ExplicitPaths_TakesPriorityOverEnvVar()
    {
        var explicitFile = CreateFile("priority_test/explicit.yaml");
        var envFile = CreateFile("priority_test/env.yaml");
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = envFile,
        };

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            explicitPaths: new[] { explicitFile },
            environment: env);

        Assert.Single(result.ConfigPaths);
        Assert.Equal(explicitFile, result.ConfigPaths[0]);
    }

    [Fact]
    public void Discover_EnvVar_TakesPriorityOverDefault()
    {
        var defaultFile = CreateFile("priority_test2/compose.yaml");
        var envFile = CreateFile("priority_test2/custom.yaml");
        var dir = Path.Combine(_tempRoot, "priority_test2");
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = envFile,
        };

        var result = ComposeFileFinder.Discover(
            workingDir: dir,
            environment: env);

        Assert.Single(result.ConfigPaths);
        Assert.Equal(envFile, result.ConfigPaths[0]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Discover() â€” fallback to default discovery
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Discover_NoExplicitNoEnv_FallsBackToDefault()
    {
        var file = CreateFile("fallback/compose.yaml");
        var dir = Path.Combine(_tempRoot, "fallback");
        var env = new Dictionary<string, string>(); // empty, no COMPOSE_FILE

        var result = ComposeFileFinder.Discover(
            workingDir: dir,
            environment: env);

        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
    }

    [Fact]
    public void Discover_NoFileAnywhere_ReturnsEmpty()
    {
        var dir = Path.Combine(_tempRoot, "nothing_here");
        Directory.CreateDirectory(dir);
        var env = new Dictionary<string, string>();

        var result = ComposeFileFinder.Discover(
            workingDir: dir,
            environment: env);

        Assert.Empty(result.ConfigPaths);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // WorkingDir resolution
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Discover_WorkingDir_SetToConfigFileDirectory()
    {
        CreateFile("workdir_test/compose.yaml");
        var dir = Path.Combine(_tempRoot, "workdir_test", "sub");
        Directory.CreateDirectory(dir);
        var env = new Dictionary<string, string>();

        var result = ComposeFileFinder.Discover(
            workingDir: dir,
            environment: env);

        Assert.Equal(Path.Combine(_tempRoot, "workdir_test"), result.WorkingDir);
    }

    [Fact]
    public void Discover_ExplicitPath_WorkingDir_FromFirstFile()
    {
        var file = CreateFile("workdir_explicit/my-compose.yaml");
        var expectedDir = Path.GetDirectoryName(file)!;

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            explicitPaths: new[] { file });

        Assert.Equal(expectedDir, result.WorkingDir);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Edge cases
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Discover_NullWorkingDir_UsesCurrentDirectory()
    {
        // This test just verifies it doesn't throw when workingDir is null
        var env = new Dictionary<string, string>();
        var result = ComposeFileFinder.Discover(
            workingDir: null,
            environment: env);

        // May or may not find files depending on current dir, but should not throw
        Assert.NotNull(result);
    }

    [Fact]
    public void DiscoverDefaultFiles_StopsAtSameDirectory_DoesNotInfiniteLoop()
    {
        // Test with the root of a drive or similar edge case
        // Create a deeply nested empty dir to ensure traversal terminates
        var deepDir = Path.Combine(_tempRoot, "a", "b", "c", "d", "e", "f");
        Directory.CreateDirectory(deepDir);

        // No compose files anywhere in this tree
        var result = ComposeFileFinder.DiscoverDefaultFiles(deepDir);
        Assert.Empty(result.ConfigPaths);
    }

    [Fact]
    public void Discover_EmptyComposeFileEnvVar_FallsBackToDefault()
    {
        CreateFile("empty_env/compose.yaml");
        var dir = Path.Combine(_tempRoot, "empty_env");
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = "",
        };

        var result = ComposeFileFinder.Discover(
            workingDir: dir,
            environment: env);

        // Empty COMPOSE_FILE should fall back to default discovery
        Assert.Single(result.ConfigPaths);
        Assert.EndsWith("compose.yaml", result.ConfigPaths[0]);
    }

    [Fact]
    public void Discover_ComposeFileEnvVar_WithSpaces_Trimmed()
    {
        var file = CreateFile("trim_test/compose.yaml");
        var env = new Dictionary<string, string>
        {
            [ComposeFileFinder.ComposeFileEnvVar] = $"  {file}  ",
        };

        var result = ComposeFileFinder.Discover(
            workingDir: _tempRoot,
            environment: env);

        Assert.Single(result.ConfigPaths);
        Assert.Equal(file, result.ConfigPaths[0]);
    }
}
