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

using Compose.NET.Types;
using Xunit;

namespace Compose.NET.Tests;

/// <summary>
/// Tests for <see cref="NanoCPUs"/> and <see cref="SSHConfig"/> distinct types.
/// </summary>
public class DistinctTypeTests
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  NanoCPUs struct tests
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void NanoCPUs_DefaultIsZero()
    {
        var cpus = new NanoCPUs();
        Assert.Equal(0f, cpus.Value);
    }

    [Fact]
    public void NanoCPUs_ConstructWithFloat()
    {
        var cpus = new NanoCPUs(0.5f);
        Assert.Equal(0.5f, cpus.Value);
    }

    [Fact]
    public void NanoCPUs_ImplicitFromFloat()
    {
        NanoCPUs cpus = 2.5f;
        Assert.Equal(2.5f, cpus.Value);
    }

    [Fact]
    public void NanoCPUs_ImplicitToFloat()
    {
        var cpus = new NanoCPUs(1.5f);
        float value = cpus;
        Assert.Equal(1.5f, value);
    }

    [Fact]
    public void NanoCPUs_Equality()
    {
        var a = new NanoCPUs(0.5f);
        var b = new NanoCPUs(0.5f);
        var c = new NanoCPUs(1.0f);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
    }

    [Fact]
    public void NanoCPUs_ComparisonOperators()
    {
        var small = new NanoCPUs(0.25f);
        var large = new NanoCPUs(2.0f);
        var sameAsSmall = new NanoCPUs(0.25f);

        Assert.True(small < large);
        Assert.True(small <= large);
        Assert.True(large > small);
        Assert.True(large >= small);
        Assert.True(small <= sameAsSmall);
        Assert.True(small >= sameAsSmall);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(2.5f)]
    public void NanoCPUs_TryParse_Float(float input)
    {
        Assert.True(NanoCPUs.TryParse(input, out var result));
        Assert.Equal(input, result.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void NanoCPUs_TryParse_Int(int input)
    {
        Assert.True(NanoCPUs.TryParse(input, out var result));
        Assert.Equal((float)input, result.Value);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void NanoCPUs_TryParse_Double(double input)
    {
        Assert.True(NanoCPUs.TryParse(input, out var result));
        Assert.Equal((float)input, result.Value);
    }

    [Theory]
    [InlineData("0.5", 0.5f)]
    [InlineData("2", 2.0f)]
    [InlineData("0.25", 0.25f)]
    public void NanoCPUs_TryParse_String(string input, float expected)
    {
        Assert.True(NanoCPUs.TryParse(input, out var result));
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void NanoCPUs_TryParse_Null_ReturnsFalse()
    {
        Assert.False(NanoCPUs.TryParse(null, out _));
    }

    [Fact]
    public void NanoCPUs_TryParse_InvalidString_ReturnsFalse()
    {
        Assert.False(NanoCPUs.TryParse("abc", out _));
    }

    [Fact]
    public void NanoCPUs_Parse_ValidValue()
    {
        var cpus = NanoCPUs.Parse("0.5");
        Assert.Equal(0.5f, cpus.Value);
    }

    [Fact]
    public void NanoCPUs_Parse_InvalidValue_Throws()
    {
        Assert.Throws<FormatException>(() => NanoCPUs.Parse("invalid"));
    }

    [Fact]
    public void NanoCPUs_ToString_ReturnsInvariantString()
    {
        var cpus = new NanoCPUs(0.5f);
        Assert.Equal("0.5", cpus.ToString());
    }

    [Fact]
    public void NanoCPUs_GetHashCode_SameForEqual()
    {
        var a = new NanoCPUs(0.5f);
        var b = new NanoCPUs(0.5f);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void NanoCPUs_Equals_Object()
    {
        var cpus = new NanoCPUs(1.0f);
        object other = new NanoCPUs(1.0f);
        Assert.True(cpus.Equals(other));
        Assert.False(cpus.Equals("not a NanoCPUs"));
        Assert.False(cpus.Equals(null));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  SSHKey tests
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void SSHKey_Parse_IdOnly()
    {
        var key = SSHKey.Parse("default");
        Assert.Equal("default", key.ID);
        Assert.Equal("", key.Path);
    }

    [Fact]
    public void SSHKey_Parse_IdWithPath()
    {
        var key = SSHKey.Parse("mykey=/home/user/.ssh/id_rsa");
        Assert.Equal("mykey", key.ID);
        Assert.Equal("/home/user/.ssh/id_rsa", key.Path);
    }

    [Fact]
    public void SSHKey_Parse_Empty()
    {
        var key = SSHKey.Parse("");
        Assert.Equal("", key.ID);
        Assert.Equal("", key.Path);
    }

    [Fact]
    public void SSHKey_ToString_IdOnly()
    {
        var key = new SSHKey { ID = "default" };
        Assert.Equal("default", key.ToString());
    }

    [Fact]
    public void SSHKey_ToString_WithPath()
    {
        var key = new SSHKey { ID = "mykey", Path = "/path/to/key" };
        Assert.Equal("mykey=/path/to/key", key.ToString());
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  SSHConfig tests
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void SSHConfig_FromDictionary()
    {
        var dict = new Dictionary<string, object?>
        {
            ["default"] = null,
            ["mykey"] = "/path/to/key"
        };

        var config = SSHConfig.FromDictionary(dict);

        Assert.Equal(2, config.Count);

        var defaultKey = config.Get("default");
        Assert.NotNull(defaultKey);
        Assert.Equal("default", defaultKey!.ID);
        Assert.Equal("", defaultKey.Path);

        var myKey = config.Get("mykey");
        Assert.NotNull(myKey);
        Assert.Equal("mykey", myKey!.ID);
        Assert.Equal("/path/to/key", myKey.Path);
    }

    [Fact]
    public void SSHConfig_FromStringList()
    {
        var items = new[] { "default", "mykey=/path/to/key" };
        var config = SSHConfig.FromStringList(items);

        Assert.Equal(2, config.Count);
        Assert.Equal("default", config[0].ID);
        Assert.Equal("", config[0].Path);
        Assert.Equal("mykey", config[1].ID);
        Assert.Equal("/path/to/key", config[1].Path);
    }

    [Fact]
    public void SSHConfig_Get_ReturnsNull_WhenNotFound()
    {
        var config = new SSHConfig
        {
            new SSHKey { ID = "default" }
        };

        Assert.Null(config.Get("nonexistent"));
    }

    [Fact]
    public void SSHConfig_CopyConstructor()
    {
        var original = new SSHConfig
        {
            new SSHKey { ID = "default" },
            new SSHKey { ID = "mykey", Path = "/path" }
        };

        var copy = new SSHConfig(original);
        Assert.Equal(2, copy.Count);
        // Note: SSHKey is a reference type so entries are shared (shallow copy)
        Assert.Same(original[0], copy[0]);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  Integration: NanoCPUs via loader
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Loader_NanoCPUs_ServiceLevel()
    {
        var yaml = @"
services:
  web:
    image: nginx
    cpus: 0.5
";
        var result = Loader.ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Equal(0.5f, svc.CPUS.Value);
    }

    [Fact]
    public void Loader_NanoCPUs_DeployResourceLevel()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      resources:
        limits:
          cpus: '0.75'
          memory: 256M
";
        var result = Loader.ComposeLoader.LoadFromContent(yaml);
        var limits = result.Project.Services["web"].Deploy!.Resources.Limits;
        Assert.NotNull(limits);
        Assert.Equal(0.75f, limits!.NanoCPUs.Value);
    }

    [Fact]
    public void Loader_NanoCPUs_ImplicitConversion_InComparison()
    {
        // Verify that NanoCPUs can be compared using implicit float conversion
        var cpus = new NanoCPUs(0.5f);
        Assert.True(cpus > 0);
        Assert.True(cpus < 1);
        float f = cpus;
        Assert.Equal(0.5f, f);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  Integration: SSHConfig via loader
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Loader_SSHConfig_MapForm()
    {
        var yaml = @"
services:
  web:
    image: nginx
    build:
      context: .
      ssh:
        default: null
        mykey: /path/to/key
";
        var result = Loader.ComposeLoader.LoadFromContent(yaml);
        var ssh = result.Project.Services["web"].Build!.SSH;
        Assert.Equal(2, ssh.Count);

        var defaultKey = ssh.Get("default");
        Assert.NotNull(defaultKey);
        Assert.Equal("", defaultKey!.Path);

        var myKey = ssh.Get("mykey");
        Assert.NotNull(myKey);
        Assert.Equal("/path/to/key", myKey!.Path);
    }

    [Fact]
    public void Loader_SSHConfig_ListForm()
    {
        var yaml = @"
services:
  web:
    image: nginx
    build:
      context: .
      ssh:
        - default
        - mykey=/path/to/key
";
        var result = Loader.ComposeLoader.LoadFromContent(yaml);
        var ssh = result.Project.Services["web"].Build!.SSH;
        Assert.Equal(2, ssh.Count);

        // After canonical transform, list form becomes map form which then gets parsed
        // The actual format depends on canonical transformer behavior
        Assert.True(ssh.Count >= 2);
    }

    [Fact]
    public void DeepCopy_SSHConfig_IsIndependent()
    {
        var original = new SSHConfig
        {
            new SSHKey { ID = "default" },
            new SSHKey { ID = "mykey", Path = "/path" }
        };

        var copy = DeepCopyHelper.CopySSHConfig(original);

        Assert.Equal(2, copy.Count);
        Assert.Equal("default", copy[0].ID);
        Assert.Equal("mykey", copy[1].ID);

        // Modify copy â€” original should be unaffected
        copy[0].ID = "changed";
        Assert.Equal("default", original[0].ID);
    }

    [Fact]
    public void DeepCopy_NanoCPUs_IsValueType()
    {
        // NanoCPUs is a struct (value type), so copy is automatic
        var original = new NanoCPUs(0.5f);
        var copy = original;
        // Structs are always copied by value, so modification doesn't affect original
        Assert.Equal(original, copy);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  Serialization: NanoCPUs and SSHConfig
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Serializer_NanoCPUs_InResource()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 256M
";
        var result = Loader.ComposeLoader.LoadFromContent(yaml);
        var yamlOutput = Serialization.ProjectSerializer.ToYaml(result.Project);
        Assert.Contains("cpus: 0.5", yamlOutput);
    }

    [Fact]
    public void Serializer_SSHConfig_AsMap()
    {
        var yaml = @"
services:
  web:
    image: nginx
    build:
      context: .
      ssh:
        default: null
        mykey: /path/to/key
";
        var result = Loader.ComposeLoader.LoadFromContent(yaml);
        var yamlOutput = Serialization.ProjectSerializer.ToYaml(result.Project);
        Assert.Contains("ssh:", yamlOutput);
        // SSH should be serialized as a map with keys
        Assert.Contains("default:", yamlOutput);
        Assert.Contains("mykey:", yamlOutput);
    }
}
