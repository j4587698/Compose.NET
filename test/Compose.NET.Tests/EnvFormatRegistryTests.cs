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
using Compose.NET.DotEnv;
using Compose.NET.Interpolation;
using Compose.NET.Loader;
using Xunit;

namespace Compose.NET.Tests;

public class EnvFormatRegistryTests : IDisposable
{
    public EnvFormatRegistryTests()
    {
        // Reset registry before each test to avoid cross-test contamination
        EnvFormatRegistry.Reset();
    }

    public void Dispose()
    {
        EnvFormatRegistry.Reset();
    }

    #region Registration

    [Fact]
    public void DefaultFormat_IsRegistered()
    {
        Assert.True(EnvFormatRegistry.IsRegistered(null));
        Assert.True(EnvFormatRegistry.IsRegistered(""));
    }

    [Fact]
    public void Register_CustomFormat_IsRegistered()
    {
        EnvFormatRegistry.Register("raw", (content, filename, vars, lookup) =>
            new Dictionary<string, string>());

        Assert.True(EnvFormatRegistry.IsRegistered("raw"));
    }

    [Fact]
    public void Register_NullParser_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EnvFormatRegistry.Register("test", null!));
    }

    [Fact]
    public void Register_CaseInsensitive()
    {
        EnvFormatRegistry.Register("RAW", (content, filename, vars, lookup) =>
            new Dictionary<string, string>());

        Assert.True(EnvFormatRegistry.IsRegistered("raw"));
        Assert.True(EnvFormatRegistry.IsRegistered("RAW"));
        Assert.True(EnvFormatRegistry.IsRegistered("Raw"));
    }

    [Fact]
    public void Unregister_ExistingFormat_ReturnsTrue()
    {
        EnvFormatRegistry.Register("custom", (content, filename, vars, lookup) =>
            new Dictionary<string, string>());

        Assert.True(EnvFormatRegistry.Unregister("custom"));
        Assert.False(EnvFormatRegistry.IsRegistered("custom"));
    }

    [Fact]
    public void Unregister_NonExistentFormat_ReturnsFalse()
    {
        Assert.False(EnvFormatRegistry.Unregister("nonexistent"));
    }

    [Fact]
    public void GetRegisteredFormats_ContainsDefault()
    {
        var formats = new List<string>(EnvFormatRegistry.GetRegisteredFormats());
        Assert.Contains("", formats);
    }

    [Fact]
    public void GetRegisteredFormats_IncludesCustom()
    {
        EnvFormatRegistry.Register("raw", (content, filename, vars, lookup) =>
            new Dictionary<string, string>());
        EnvFormatRegistry.Register("json", (content, filename, vars, lookup) =>
            new Dictionary<string, string>());

        var formats = new List<string>(EnvFormatRegistry.GetRegisteredFormats());
        Assert.Contains("raw", formats);
        Assert.Contains("json", formats);
    }

    [Fact]
    public void Reset_RestoresOnlyDefault()
    {
        EnvFormatRegistry.Register("custom1", (content, filename, vars, lookup) =>
            new Dictionary<string, string>());
        EnvFormatRegistry.Register("custom2", (content, filename, vars, lookup) =>
            new Dictionary<string, string>());

        EnvFormatRegistry.Reset();

        Assert.True(EnvFormatRegistry.IsRegistered(""));
        Assert.False(EnvFormatRegistry.IsRegistered("custom1"));
        Assert.False(EnvFormatRegistry.IsRegistered("custom2"));
    }

    #endregion

    #region ParseContent

    [Fact]
    public void ParseContent_DefaultFormat_ParsesEnvSyntax()
    {
        var result = EnvFormatRegistry.ParseContent(
            "FOO=bar\nBAZ=qux", format: null);

        Assert.Equal("bar", result["FOO"]);
        Assert.Equal("qux", result["BAZ"]);
    }

    [Fact]
    public void ParseContent_DefaultFormat_SupportsInterpolation()
    {
        var result = EnvFormatRegistry.ParseContent(
            "BASE=hello\nDERIVED=${BASE}_world", format: null);

        Assert.Equal("hello", result["BASE"]);
        Assert.Equal("hello_world", result["DERIVED"]);
    }

    [Fact]
    public void ParseContent_CustomFormat_Used()
    {
        EnvFormatRegistry.Register("raw", (content, filename, vars, lookup) =>
        {
            // A "raw" format that treats each line as key=literal (no interpolation, no comments)
            var result = new Dictionary<string, string>();
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    result[trimmed.Substring(0, eq)] = trimmed.Substring(eq + 1);
                }
            }
            return result;
        });

        // In "raw" format, # is not a comment and $ is literal
        var result = EnvFormatRegistry.ParseContent(
            "# COMMENT=yes\nFOO=$BAR\nBAZ=qux",
            format: "raw");

        Assert.Equal("$BAR", result["FOO"]);
        Assert.Equal("qux", result["BAZ"]);
        Assert.True(result.ContainsKey("# COMMENT")); // Not treated as comment in raw format
    }

    [Fact]
    public void ParseContent_UnregisteredFormat_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvFormatRegistry.ParseContent("FOO=bar", format: "unknown"));

        Assert.Contains("unknown", ex.Message);
        Assert.Contains("EnvFormatRegistry.Register()", ex.Message);
    }

    [Fact]
    public void ParseContent_CustomFormat_ReceivesCurrentVars()
    {
        IDictionary<string, string>? receivedVars = null;

        EnvFormatRegistry.Register("spy", (content, filename, vars, lookup) =>
        {
            receivedVars = vars;
            return new Dictionary<string, string> { { "RESULT", "ok" } };
        });

        var existing = new Dictionary<string, string> { { "EXISTING", "value" } };
        EnvFormatRegistry.ParseContent("test", format: "spy",
            currentVars: existing);

        Assert.NotNull(receivedVars);
        Assert.True(receivedVars!.ContainsKey("EXISTING"));
        Assert.Equal("value", receivedVars["EXISTING"]);
    }

    [Fact]
    public void ParseContent_CustomFormat_ReceivesLookup()
    {
        VariableLookup? receivedLookup = null;

        EnvFormatRegistry.Register("spy", (content, filename, vars, lookup) =>
        {
            receivedLookup = lookup;
            return new Dictionary<string, string>();
        });

        VariableLookup myLookup = key => ("test", true);
        EnvFormatRegistry.ParseContent("test", format: "spy", lookup: myLookup);

        Assert.NotNull(receivedLookup);
        var (val, found) = receivedLookup!("anything");
        Assert.True(found);
        Assert.Equal("test", val);
    }

    [Fact]
    public void ParseContent_CustomFormat_ReceivesFilename()
    {
        string? receivedFilename = null;

        EnvFormatRegistry.Register("spy", (content, filename, vars, lookup) =>
        {
            receivedFilename = filename;
            return new Dictionary<string, string>();
        });

        EnvFormatRegistry.ParseContent("test", format: "spy", filename: "myfile.env");
        Assert.Equal("myfile.env", receivedFilename);
    }

    #endregion

    #region ParseFile

    [Fact]
    public void ParseFile_DefaultFormat_ReadsAndParses()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "HOST=localhost\nPORT=5432");

            var result = EnvFormatRegistry.ParseFile(tempFile, format: null);

            Assert.Equal("localhost", result["HOST"]);
            Assert.Equal("5432", result["PORT"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseFile_CustomFormat_Used()
    {
        EnvFormatRegistry.Register("upper", (content, filename, vars, lookup) =>
        {
            var result = new Dictionary<string, string>();
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    result[trimmed.Substring(0, eq)] = trimmed.Substring(eq + 1).ToUpperInvariant();
                }
            }
            return result;
        });

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "FOO=bar");

            var result = EnvFormatRegistry.ParseFile(tempFile, format: "upper");

            Assert.Equal("BAR", result["FOO"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseFile_FileNotFound_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            EnvFormatRegistry.ParseFile("/nonexistent/path.env", format: null));
    }

    [Fact]
    public void ParseFile_UnregisteredFormat_Throws()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "FOO=bar");

            Assert.Throws<InvalidOperationException>(() =>
                EnvFormatRegistry.ParseFile(tempFile, format: "nonexistent"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region Override existing format

    [Fact]
    public void Register_CanOverrideDefaultFormat()
    {
        // Override the default "" format
        EnvFormatRegistry.Register("", (content, filename, vars, lookup) =>
        {
            return new Dictionary<string, string> { { "OVERRIDDEN", "true" } };
        });

        var result = EnvFormatRegistry.ParseContent("FOO=bar", format: null);

        Assert.True(result.ContainsKey("OVERRIDDEN"));
        Assert.False(result.ContainsKey("FOO"));
    }

    [Fact]
    public void Register_OverrideCustomFormat_ReplacesParser()
    {
        EnvFormatRegistry.Register("custom", (content, filename, vars, lookup) =>
            new Dictionary<string, string> { { "VERSION", "1" } });

        EnvFormatRegistry.Register("custom", (content, filename, vars, lookup) =>
            new Dictionary<string, string> { { "VERSION", "2" } });

        var result = EnvFormatRegistry.ParseContent("", format: "custom");
        Assert.Equal("2", result["VERSION"]);
    }

    #endregion

    #region Loader integration

    [Fact]
    public void Loader_EnvFile_DefaultFormat_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var envFile = Path.Combine(tempDir, ".env");
            File.WriteAllText(envFile, "DB_HOST=localhost\nDB_PORT=5432");

            var yaml = $@"
services:
  web:
    image: nginx
    env_file:
      - path: {envFile.Replace("\\", "/")}
";
            var result = ComposeLoader.LoadFromContent(yaml, tempDir);
            var svc = result.Project.Services["web"];

            Assert.NotNull(svc.Environment);
            Assert.True(svc.Environment.ContainsKey("DB_HOST"));
            Assert.Equal("localhost", svc.Environment["DB_HOST"]);
            Assert.Equal("5432", svc.Environment["DB_PORT"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Loader_EnvFile_CustomFormat_Used()
    {
        EnvFormatRegistry.Register("uppercase", (content, filename, vars, lookup) =>
        {
            var result = new Dictionary<string, string>();
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    result[trimmed.Substring(0, eq)] = trimmed.Substring(eq + 1).ToUpperInvariant();
                }
            }
            return result;
        });

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var envFile = Path.Combine(tempDir, "config.env");
            File.WriteAllText(envFile, "MY_VAR=hello");

            var yaml = $@"
services:
  web:
    image: nginx
    env_file:
      - path: {envFile.Replace("\\", "/")}
        format: uppercase
";
            var result = ComposeLoader.LoadFromContent(yaml, tempDir);
            var svc = result.Project.Services["web"];

            Assert.NotNull(svc.Environment);
            Assert.Equal("HELLO", svc.Environment["MY_VAR"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Loader_EnvFile_UnregisteredFormat_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var envFile = Path.Combine(tempDir, "config.env");
            File.WriteAllText(envFile, "FOO=bar");

            var yaml = $@"
services:
  web:
    image: nginx
    env_file:
      - path: {envFile.Replace("\\", "/")}
        format: nonexistent
";
            Assert.Throws<InvalidOperationException>(() =>
                ComposeLoader.LoadFromContent(yaml, tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Loader_EnvFile_NoFormat_UsesDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var envFile = Path.Combine(tempDir, ".env");
            // Default format supports comments and variable expansion
            File.WriteAllText(envFile, "# comment\nFOO=bar\nBAZ=${FOO}_qux");

            var yaml = $@"
services:
  web:
    image: nginx
    env_file:
      - path: {envFile.Replace("\\", "/")}
";
            var result = ComposeLoader.LoadFromContent(yaml, tempDir);
            var svc = result.Project.Services["web"];

            Assert.NotNull(svc.Environment);
            Assert.Equal("bar", svc.Environment["FOO"]);
            Assert.Equal("bar_qux", svc.Environment["BAZ"]);
            Assert.False(svc.Environment.ContainsKey("# comment"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion
}
