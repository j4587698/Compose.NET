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
using System.Text;
using Xunit;
using Compose.NET.Loader;
using Compose.NET.Types;

namespace Compose.NET.Tests;

/// <summary>
/// Tests for <c>!reset</c> and <c>!override</c> YAML tags in Docker Compose override files.
/// These tags allow override files to remove or completely replace values from the base
/// configuration rather than merging with them.
/// </summary>
public class ResetOverrideTests
{
    private static readonly LoadOptions SkipValidationOptions = new LoadOptions
    {
        SkipValidation = true,
        SkipNormalization = true,
        SkipConsistencyCheck = true,
        ResolvePaths = false
    };

    /// <summary>
    /// Helper to load a base + override YAML pair through the full Loader pipeline.
    /// </summary>
    private static LoadResult LoadWithOverride(string baseYaml, string overrideYaml, LoadOptions? options = null)
    {
        options ??= SkipValidationOptions;
        var details = new ConfigDetails
        {
            WorkingDir = System.IO.Directory.GetCurrentDirectory(),
            ConfigFiles = new List<ConfigFile>
            {
                new ConfigFile
                {
                    Filename = "compose.yaml",
                    Content = Encoding.UTF8.GetBytes(baseYaml)
                },
                new ConfigFile
                {
                    Filename = "compose.override.yaml",
                    Content = Encoding.UTF8.GetBytes(overrideYaml)
                }
            }
        };
        return ComposeLoader.Load(details, options);
    }

    // â”€â”€ !reset on scalar â”€â”€

    [Fact]
    public void Reset_Scalar_RemovesFromMergedOutput()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    hostname: myhostname
";
        var overrideYaml = @"
services:
  web:
    hostname: !reset """"
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // image should still be present from base
        Assert.Equal("nginx", svc.Image);

        // hostname should be reset (removed)
        Assert.True(string.IsNullOrEmpty(svc.Hostname));
    }

    [Fact]
    public void Reset_Scalar_OtherFieldsUnaffected()
    {
        var baseYaml = @"
services:
  web:
    image: nginx:latest
    container_name: myweb
    hostname: myhost
";
        var overrideYaml = @"
services:
  web:
    hostname: !reset """"
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // container_name and image should survive
        Assert.Equal("nginx:latest", svc.Image);
        Assert.Equal("myweb", svc.ContainerName);
        Assert.True(string.IsNullOrEmpty(svc.Hostname));
    }

    // â”€â”€ !reset on mapping â”€â”€

    [Fact]
    public void Reset_Mapping_RemovesEntireSubtree()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    labels:
      com.example.foo: bar
      com.example.baz: qux
";
        var overrideYaml = @"
services:
  web:
    labels: !reset {}
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // labels should be reset â€” empty
        Assert.True(svc.Labels == null || svc.Labels.Count == 0);
    }

    // â”€â”€ !reset on list (sequence) â”€â”€

    [Fact]
    public void Reset_List_RemovesEntireList()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    ports:
      - ""8080:80""
      - ""8443:443""
";
        var overrideYaml = @"
services:
  web:
    ports: !reset []
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // ports should be reset â€” empty
        Assert.True(svc.Ports == null || svc.Ports.Count == 0);
    }

    // â”€â”€ !override on mapping â”€â”€

    [Fact]
    public void Override_Mapping_ReplacesInsteadOfMerging()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    labels:
      com.example.foo: bar
      com.example.baz: qux
";
        var overrideYaml = @"
services:
  web:
    labels: !override
      com.example.new: value
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // labels should be COMPLETELY replaced, not merged
        Assert.NotNull(svc.Labels);
        Assert.Single(svc.Labels);
        Assert.Equal("value", svc.Labels["com.example.new"]);
    }

    // â”€â”€ !override on list â”€â”€

    [Fact]
    public void Override_List_ReplacesInsteadOfAppending()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    dns:
      - 8.8.8.8
      - 8.8.4.4
";
        var overrideYaml = @"
services:
  web:
    dns: !override
      - 1.1.1.1
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // dns should be completely replaced, not appended
        Assert.NotNull(svc.DNS);
        Assert.Single(svc.DNS);
        Assert.Equal("1.1.1.1", svc.DNS[0]);
    }

    // â”€â”€ !override on scalar â”€â”€

    [Fact]
    public void Override_Scalar_ReplacesValue()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    hostname: original
";
        var overrideYaml = @"
services:
  web:
    hostname: !override newhost
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        Assert.Equal("newhost", svc.Hostname);
    }

    // â”€â”€ No reset/override â€” normal merge behavior preserved â”€â”€

    [Fact]
    public void NoTags_NormalMerge_Preserved()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    labels:
      com.example.foo: bar
";
        var overrideYaml = @"
services:
  web:
    labels:
      com.example.baz: qux
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // Normal merge: both labels should be present
        Assert.NotNull(svc.Labels);
        Assert.Equal(2, svc.Labels.Count);
        Assert.Equal("bar", svc.Labels["com.example.foo"]);
        Assert.Equal("qux", svc.Labels["com.example.baz"]);
    }

    // â”€â”€ ResetProcessor unit tests â”€â”€

    [Fact]
    public void ResetProcessor_ParsesSimpleYaml()
    {
        var processor = new ResetProcessor();
        var result = processor.ParseWithResetDetection(@"
services:
  web:
    image: nginx
");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("services"));
        var services = result["services"] as Dictionary<string, object?>;
        Assert.NotNull(services);
        Assert.True(services!.ContainsKey("web"));
    }

    [Fact]
    public void ResetProcessor_DetectsResetTag()
    {
        var processor = new ResetProcessor();
        var result = processor.ParseWithResetDetection(@"
services:
  web:
    image: nginx
    hostname: !reset """"
");
        Assert.NotNull(result);
        Assert.True(processor.HasResets);
        Assert.True(processor.ResetPaths.Count > 0);

        // The reset node should be removed from the parsed output
        var services = result["services"] as Dictionary<string, object?>;
        var web = services!["web"] as Dictionary<string, object?>;
        Assert.False(web!.ContainsKey("hostname"));
    }

    [Fact]
    public void ResetProcessor_DetectsOverrideTag()
    {
        var processor = new ResetProcessor();
        var result = processor.ParseWithResetDetection(@"
services:
  web:
    labels: !override
      new_label: value
");
        Assert.NotNull(result);
        Assert.True(processor.HasResets);

        // Override nodes should still be in the output (unlike reset)
        var services = result["services"] as Dictionary<string, object?>;
        var web = services!["web"] as Dictionary<string, object?>;
        Assert.True(web!.ContainsKey("labels"));
    }

    [Fact]
    public void ResetProcessor_ApplyNullOverrides_RemovesMatchingPaths()
    {
        var processor = new ResetProcessor();
        // Parse a YAML with !reset to record paths
        processor.ParseWithResetDetection(@"
services:
  web:
    hostname: !reset """"
");

        // Create a target dict simulating the base config
        var target = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>
            {
                ["web"] = new Dictionary<string, object?>
                {
                    ["image"] = "nginx",
                    ["hostname"] = "original"
                }
            }
        };

        processor.ApplyNullOverrides(target);

        var services = target["services"] as Dictionary<string, object?>;
        var web = services!["web"] as Dictionary<string, object?>;
        // hostname should be removed
        Assert.False(web!.ContainsKey("hostname"));
        // image should remain
        Assert.Equal("nginx", web["image"]);
    }

    [Fact]
    public void ResetProcessor_EmptyYaml_ReturnsEmptyDict()
    {
        var processor = new ResetProcessor();
        var result = processor.ParseWithResetDetection("");
        Assert.NotNull(result);
        Assert.Empty(result);
        Assert.False(processor.HasResets);
    }

    [Fact]
    public void ResetProcessor_HandlesAnchorsAndAliases()
    {
        var processor = new ResetProcessor();
        var result = processor.ParseWithResetDetection(@"
x-common: &common
  image: nginx
  environment:
    FOO: bar

services:
  web:
    <<: *common
    ports:
      - ""80:80""
");
        Assert.NotNull(result);
        var services = result["services"] as Dictionary<string, object?>;
        var web = services!["web"] as Dictionary<string, object?>;
        // web should have inherited image from anchor
        Assert.Equal("nginx", web!["image"]);
        // web should have its own ports
        Assert.True(web.ContainsKey("ports"));
    }

    // â”€â”€ Multi-file merge with !reset and anchors â”€â”€

    [Fact]
    public void Reset_WithAnchors_WorksCorrectly()
    {
        var baseYaml = @"
x-common: &common
  environment:
    SHARED: value

services:
  web:
    <<: *common
    image: nginx
    hostname: myhost
";
        var overrideYaml = @"
services:
  web:
    hostname: !reset """"
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // Image and inherited environment should survive
        Assert.Equal("nginx", svc.Image);
        Assert.NotNull(svc.Environment);
        Assert.Equal("value", svc.Environment["SHARED"]);

        // hostname should be reset
        Assert.True(string.IsNullOrEmpty(svc.Hostname));
    }

    // â”€â”€ Environment override â”€â”€

    [Fact]
    public void Override_Environment_ReplacesEntireMapping()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    environment:
      FOO: bar
      BAZ: qux
";
        var overrideYaml = @"
services:
  web:
    environment: !override
      NEW_VAR: new_value
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // environment should be completely replaced
        Assert.NotNull(svc.Environment);
        Assert.Single(svc.Environment);
        Assert.Equal("new_value", svc.Environment["NEW_VAR"]);
    }

    // â”€â”€ Reset a nested key within build config â”€â”€

    [Fact]
    public void Reset_NestedKey_InBuildConfig()
    {
        var baseYaml = @"
services:
  web:
    image: nginx
    build:
      context: .
      dockerfile: Dockerfile.custom
      args:
        ARG1: val1
";
        var overrideYaml = @"
services:
  web:
    build:
      args: !reset {}
";
        var result = LoadWithOverride(baseYaml, overrideYaml);
        var svc = result.Project.Services["web"];

        // build should still exist with context and dockerfile
        Assert.NotNull(svc.Build);
        Assert.Equal(".", svc.Build!.Context);
        Assert.Equal("Dockerfile.custom", svc.Build.Dockerfile);

        // args should be reset
        Assert.True(svc.Build.Args == null || svc.Build.Args.Count == 0);
    }
}
