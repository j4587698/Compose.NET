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

using Xunit;
using Compose.NET.Types;

namespace Compose.NET.Tests;

public class ExtensionsDecodeTests
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Helper types for testing
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public class SimpleConfig
    {
        public string Name { get; set; } = string.Empty;
        public int MaxRetries { get; set; }
        public bool Enabled { get; set; }
    }

    public class SnakeCaseConfig
    {
        public string ServerUrl { get; set; } = string.Empty;
        public int MaxConnections { get; set; }
        public double TimeoutSeconds { get; set; }
    }

    public class NestedConfig
    {
        public string Name { get; set; } = string.Empty;
        public DatabaseConfig Database { get; set; } = new();
    }

    public class DatabaseConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
    }

    public class ConfigWithList
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class ConfigWithDict
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class ConfigWithNullable
    {
        public int? Port { get; set; }
        public bool? Debug { get; set; }
        public string? Description { get; set; }
    }

    public class ConfigWithArray
    {
        public string Name { get; set; } = string.Empty;
        public string[] Hosts { get; set; } = Array.Empty<string>();
    }

    public class ConfigWithEnum
    {
        public LogLevel Level { get; set; }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public class DeeplyNestedConfig
    {
        public string Name { get; set; } = string.Empty;
        public MiddleLayer Middle { get; set; } = new();
    }

    public class MiddleLayer
    {
        public string Value { get; set; } = string.Empty;
        public InnerLayer Inner { get; set; } = new();
    }

    public class InnerLayer
    {
        public int Count { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class ConfigWithNumericTypes
    {
        public long BigNumber { get; set; }
        public float Ratio { get; set; }
        public double Precision { get; set; }
        public decimal Cost { get; set; }
    }

    public class ConfigWithBoolVariants
    {
        public bool Flag1 { get; set; }
        public bool Flag2 { get; set; }
        public bool Flag3 { get; set; }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Extensions.Decode<T> â€” basic decoding
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Decode_SimpleProperties()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "test-service",
                ["max_retries"] = 3,
                ["enabled"] = true
            }
        };

        var config = extensions.Decode<SimpleConfig>("x-config");

        Assert.Equal("test-service", config.Name);
        Assert.Equal(3, config.MaxRetries);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void Decode_SnakeCaseKeys()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["server_url"] = "https://example.com",
                ["max_connections"] = 100,
                ["timeout_seconds"] = 30.5
            }
        };

        var config = extensions.Decode<SnakeCaseConfig>("x-config");

        Assert.Equal("https://example.com", config.ServerUrl);
        Assert.Equal(100, config.MaxConnections);
        Assert.Equal(30.5, config.TimeoutSeconds);
    }

    [Fact]
    public void Decode_NestedObject()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "my-app",
                ["database"] = new Dictionary<string, object>
                {
                    ["host"] = "localhost",
                    ["port"] = 5432,
                    ["username"] = "admin"
                }
            }
        };

        var config = extensions.Decode<NestedConfig>("x-config");

        Assert.Equal("my-app", config.Name);
        Assert.Equal("localhost", config.Database.Host);
        Assert.Equal(5432, config.Database.Port);
        Assert.Equal("admin", config.Database.Username);
    }

    [Fact]
    public void Decode_WithList()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "tagged-service",
                ["tags"] = new List<object> { "web", "production", "v2" }
            }
        };

        var config = extensions.Decode<ConfigWithList>("x-config");

        Assert.Equal("tagged-service", config.Name);
        Assert.Equal(3, config.Tags.Count);
        Assert.Equal("web", config.Tags[0]);
        Assert.Equal("production", config.Tags[1]);
        Assert.Equal("v2", config.Tags[2]);
    }

    [Fact]
    public void Decode_WithDictionary()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "meta-service",
                ["metadata"] = new Dictionary<string, object>
                {
                    ["env"] = "prod",
                    ["region"] = "us-east-1"
                }
            }
        };

        var config = extensions.Decode<ConfigWithDict>("x-config");

        Assert.Equal("meta-service", config.Name);
        Assert.Equal(2, config.Metadata.Count);
        Assert.Equal("prod", config.Metadata["env"]);
        Assert.Equal("us-east-1", config.Metadata["region"]);
    }

    [Fact]
    public void Decode_NullableProperties_WithValues()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["port"] = 8080,
                ["debug"] = true,
                ["description"] = "test"
            }
        };

        var config = extensions.Decode<ConfigWithNullable>("x-config");

        Assert.Equal(8080, config.Port);
        Assert.True(config.Debug);
        Assert.Equal("test", config.Description);
    }

    [Fact]
    public void Decode_NullableProperties_WithNull()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["port"] = 9090
                // debug and description omitted
            }
        };

        var config = extensions.Decode<ConfigWithNullable>("x-config");

        Assert.Equal(9090, config.Port);
        Assert.Null(config.Debug);
        Assert.Null(config.Description);
    }

    [Fact]
    public void Decode_WithArray()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "multi-host",
                ["hosts"] = new List<object> { "host1.example.com", "host2.example.com" }
            }
        };

        var config = extensions.Decode<ConfigWithArray>("x-config");

        Assert.Equal("multi-host", config.Name);
        Assert.Equal(2, config.Hosts.Length);
        Assert.Equal("host1.example.com", config.Hosts[0]);
        Assert.Equal("host2.example.com", config.Hosts[1]);
    }

    [Fact]
    public void Decode_WithEnum()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["level"] = "Warning"
            }
        };

        var config = extensions.Decode<ConfigWithEnum>("x-config");

        Assert.Equal(LogLevel.Warning, config.Level);
    }

    [Fact]
    public void Decode_EnumCaseInsensitive()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["level"] = "error"
            }
        };

        var config = extensions.Decode<ConfigWithEnum>("x-config");

        Assert.Equal(LogLevel.Error, config.Level);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Extensions.Decode<T> â€” type conversion edge cases
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Decode_StringToInt()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "svc",
                ["max_retries"] = "5",
                ["enabled"] = "true"
            }
        };

        var config = extensions.Decode<SimpleConfig>("x-config");

        Assert.Equal(5, config.MaxRetries);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void Decode_BoolFromVariousStrings()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["flag1"] = "yes",
                ["flag2"] = "1",
                ["flag3"] = "on"
            }
        };

        var config = extensions.Decode<ConfigWithBoolVariants>("x-config");

        Assert.True(config.Flag1);
        Assert.True(config.Flag2);
        Assert.True(config.Flag3);
    }

    [Fact]
    public void Decode_NumericConversions()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["big_number"] = 9999999999L,
                ["ratio"] = 0.75,
                ["precision"] = 3.14159265,
                ["cost"] = 19.99m
            }
        };

        var config = extensions.Decode<ConfigWithNumericTypes>("x-config");

        Assert.Equal(9999999999L, config.BigNumber);
        Assert.Equal(3.14159265, config.Precision);
        Assert.Equal(19.99m, config.Cost);
    }

    [Fact]
    public void Decode_DeeplyNested()
    {
        var extensions = new Extensions
        {
            ["x-deep"] = new Dictionary<string, object>
            {
                ["name"] = "root",
                ["middle"] = new Dictionary<string, object>
                {
                    ["value"] = "mid",
                    ["inner"] = new Dictionary<string, object>
                    {
                        ["count"] = 42,
                        ["label"] = "deep"
                    }
                }
            }
        };

        var config = extensions.Decode<DeeplyNestedConfig>("x-deep");

        Assert.Equal("root", config.Name);
        Assert.Equal("mid", config.Middle.Value);
        Assert.Equal(42, config.Middle.Inner.Count);
        Assert.Equal("deep", config.Middle.Inner.Label);
    }

    [Fact]
    public void Decode_IgnoresUnknownKeys()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "test",
                ["max_retries"] = 3,
                ["unknown_key"] = "should be ignored",
                ["another_unknown"] = 42
            }
        };

        var config = extensions.Decode<SimpleConfig>("x-config");

        Assert.Equal("test", config.Name);
        Assert.Equal(3, config.MaxRetries);
    }

    [Fact]
    public void Decode_MissingKeysUseDefaults()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "partial"
                // max_retries and enabled omitted
            }
        };

        var config = extensions.Decode<SimpleConfig>("x-config");

        Assert.Equal("partial", config.Name);
        Assert.Equal(0, config.MaxRetries); // default int
        Assert.False(config.Enabled); // default bool
    }

    [Fact]
    public void Decode_EmptyDictionary()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>()
        };

        var config = extensions.Decode<SimpleConfig>("x-config");

        Assert.Equal(string.Empty, config.Name);
        Assert.Equal(0, config.MaxRetries);
        Assert.False(config.Enabled);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Extensions.Decode<T> â€” from Extensions (not dict)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Decode_FromExtensionsObject()
    {
        var inner = new Extensions
        {
            ["name"] = "from-extensions",
            ["max_retries"] = 7,
            ["enabled"] = true
        };

        var extensions = new Extensions
        {
            ["x-config"] = inner
        };

        var config = extensions.Decode<SimpleConfig>("x-config");

        Assert.Equal("from-extensions", config.Name);
        Assert.Equal(7, config.MaxRetries);
        Assert.True(config.Enabled);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Extensions.TryDecode<T>
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void TryDecode_Success()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "try-test",
                ["max_retries"] = 5,
                ["enabled"] = true
            }
        };

        var success = extensions.TryDecode<SimpleConfig>("x-config", out var config);

        Assert.True(success);
        Assert.Equal("try-test", config.Name);
        Assert.Equal(5, config.MaxRetries);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void TryDecode_KeyNotFound_ReturnsFalse()
    {
        var extensions = new Extensions();

        var success = extensions.TryDecode<SimpleConfig>("x-missing", out var config);

        Assert.False(success);
        Assert.Equal(default, config);
    }

    [Fact]
    public void TryDecode_InvalidValue_ReturnsFalse()
    {
        var extensions = new Extensions
        {
            ["x-config"] = "not a dictionary"
        };

        var success = extensions.TryDecode<SimpleConfig>("x-config", out var config);

        Assert.False(success);
        Assert.Equal(default, config);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Extensions.Decode<T> â€” error cases
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Decode_KeyNotFound_Throws()
    {
        var extensions = new Extensions();

        Assert.Throws<KeyNotFoundException>(() => extensions.Decode<SimpleConfig>("x-missing"));
    }

    [Fact]
    public void Decode_NotADictionary_Throws()
    {
        var extensions = new Extensions
        {
            ["x-config"] = "just a string"
        };

        Assert.Throws<InvalidOperationException>(() => extensions.Decode<SimpleConfig>("x-config"));
    }

    [Fact]
    public void Decode_IntValue_Throws()
    {
        var extensions = new Extensions
        {
            ["x-config"] = 42
        };

        Assert.Throws<InvalidOperationException>(() => extensions.Decode<SimpleConfig>("x-config"));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // PascalCase / camelCase / kebab-case matching
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Decode_PascalCaseKeys()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["Name"] = "pascal",
                ["MaxRetries"] = 10,
                ["Enabled"] = true
            }
        };

        var config = extensions.Decode<SimpleConfig>("x-config");

        Assert.Equal("pascal", config.Name);
        Assert.Equal(10, config.MaxRetries);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void Decode_CamelCaseKeys()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["name"] = "camel",
                ["maxRetries"] = 8,
                ["enabled"] = false
            }
        };

        var config = extensions.Decode<SimpleConfig>("x-config");

        Assert.Equal("camel", config.Name);
        Assert.Equal(8, config.MaxRetries);
        Assert.False(config.Enabled);
    }

    [Fact]
    public void Decode_KebabCaseKeys()
    {
        var extensions = new Extensions
        {
            ["x-config"] = new Dictionary<string, object>
            {
                ["server-url"] = "https://kebab.example.com",
                ["max-connections"] = 50,
                ["timeout-seconds"] = 15.0
            }
        };

        var config = extensions.Decode<SnakeCaseConfig>("x-config");

        Assert.Equal("https://kebab.example.com", config.ServerUrl);
        Assert.Equal(50, config.MaxConnections);
        Assert.Equal(15.0, config.TimeoutSeconds);
    }
}
