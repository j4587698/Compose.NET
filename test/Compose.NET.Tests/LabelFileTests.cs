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
using Compose.NET.Loader;
using Compose.NET.Types;

namespace Compose.NET.Tests;

public class LabelFileTests
{
    [Fact]
    public void LabelFile_LoadsLabelsFromFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "labels.txt"), @"
com.example.app=myapp
com.example.version=1.0
");
            var mainYaml = @"
services:
  web:
    image: nginx
    label_file:
      - labels.txt
";
            File.WriteAllText(Path.Combine(tempDir, "compose.yaml"), mainYaml);
            var result = ComposeLoader.LoadFromFile(Path.Combine(tempDir, "compose.yaml"));
            var svc = result.Project.Services["web"];
            Assert.Equal("myapp", svc.Labels["com.example.app"]);
            Assert.Equal("1.0", svc.Labels["com.example.version"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LabelFile_SkipsCommentsAndBlankLines()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "labels.txt"), @"
# This is a comment
com.example.app=myapp

# Another comment

com.example.env=prod
");
            var mainYaml = @"
services:
  web:
    image: nginx
    label_file:
      - labels.txt
";
            File.WriteAllText(Path.Combine(tempDir, "compose.yaml"), mainYaml);
            var result = ComposeLoader.LoadFromFile(Path.Combine(tempDir, "compose.yaml"));
            var svc = result.Project.Services["web"];
            Assert.Equal(2, svc.Labels.Count);
            Assert.Equal("myapp", svc.Labels["com.example.app"]);
            Assert.Equal("prod", svc.Labels["com.example.env"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LabelFile_ExplicitLabelsOverrideLabelFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "labels.txt"), @"com.example.app=fromfile
com.example.env=fromfile
");
            var mainYaml = @"
services:
  web:
    image: nginx
    label_file:
      - labels.txt
    labels:
      com.example.app: fromexplicit
";
            File.WriteAllText(Path.Combine(tempDir, "compose.yaml"), mainYaml);
            var result = ComposeLoader.LoadFromFile(Path.Combine(tempDir, "compose.yaml"));
            var svc = result.Project.Services["web"];
            // Explicit labels should win over label_file values
            Assert.Equal("fromexplicit", svc.Labels["com.example.app"]);
            // But label_file values not in explicit should still be present
            Assert.Equal("fromfile", svc.Labels["com.example.env"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LabelFile_MultipleFIles_LaterOverridesEarlier()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "labels1.txt"), @"com.example.app=first
com.example.env=first
");
            File.WriteAllText(Path.Combine(tempDir, "labels2.txt"), @"com.example.app=second
");
            var mainYaml = @"
services:
  web:
    image: nginx
    label_file:
      - labels1.txt
      - labels2.txt
";
            File.WriteAllText(Path.Combine(tempDir, "compose.yaml"), mainYaml);
            var result = ComposeLoader.LoadFromFile(Path.Combine(tempDir, "compose.yaml"));
            var svc = result.Project.Services["web"];
            // Later file overrides earlier
            Assert.Equal("second", svc.Labels["com.example.app"]);
            // Key only in first file should be present
            Assert.Equal("first", svc.Labels["com.example.env"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LabelFile_MissingFile_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mainYaml = @"
services:
  web:
    image: nginx
    label_file:
      - nonexistent.txt
";
            File.WriteAllText(Path.Combine(tempDir, "compose.yaml"), mainYaml);
            // Missing label_file should always error (no required option)
            Assert.ThrowsAny<Exception>(() => ComposeLoader.LoadFromFile(Path.Combine(tempDir, "compose.yaml")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LabelFile_KeyWithoutValue_EmptyString()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "labels.txt"), @"com.example.novalue
com.example.emptyvalue=
");
            var mainYaml = @"
services:
  web:
    image: nginx
    label_file:
      - labels.txt
";
            File.WriteAllText(Path.Combine(tempDir, "compose.yaml"), mainYaml);
            var result = ComposeLoader.LoadFromFile(Path.Combine(tempDir, "compose.yaml"));
            var svc = result.Project.Services["web"];
            // Key without = gets empty string value
            Assert.Equal("", svc.Labels["com.example.novalue"]);
            // Key with = but no value gets empty string
            Assert.Equal("", svc.Labels["com.example.emptyvalue"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LabelFile_StringSyntax_ConvertedToList()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "labels.txt"), @"com.example.app=myapp
");
            var mainYaml = @"
services:
  web:
    image: nginx
    label_file: labels.txt
";
            File.WriteAllText(Path.Combine(tempDir, "compose.yaml"), mainYaml);
            var result = ComposeLoader.LoadFromFile(Path.Combine(tempDir, "compose.yaml"));
            var svc = result.Project.Services["web"];
            Assert.Equal("myapp", svc.Labels["com.example.app"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
