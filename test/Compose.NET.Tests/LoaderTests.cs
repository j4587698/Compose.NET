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
using Compose.NET.Errors;
using Compose.NET.Loader;
using Compose.NET.Types;

namespace Compose.NET.Tests;

public class LoaderTests
{
    // â”€â”€ Basic loading from YAML content â”€â”€

    [Fact]
    public void LoadFromContent_MinimalCompose()
    {
        var yaml = @"
services:
  web:
    image: nginx:latest
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.NotNull(result.Project);
        Assert.True(result.Project.Services.ContainsKey("web"));
        Assert.Equal("nginx:latest", result.Project.Services["web"].Image);
    }

    [Fact]
    public void LoadFromContent_MultipleServices()
    {
        var yaml = @"
services:
  web:
    image: nginx
  db:
    image: postgres:15
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal(2, result.Project.Services.Count);
        Assert.True(result.Project.Services.ContainsKey("web"));
        Assert.True(result.Project.Services.ContainsKey("db"));
    }

    // â”€â”€ Ports â”€â”€

    [Fact]
    public void LoadFromContent_Ports_ShortSyntax()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - ""8080:80""
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Ports);
        Assert.Single(svc.Ports);
        Assert.Equal(80u, svc.Ports[0].Target);
        Assert.Equal("8080", svc.Ports[0].Published);
    }

    // â”€â”€ Volumes â”€â”€

    [Fact]
    public void LoadFromContent_Volumes_ShortSyntax()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - ""./html:/usr/share/nginx/html:ro""
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Volumes);
        Assert.Single(svc.Volumes);
        Assert.Equal("./html", svc.Volumes[0].Source);
        Assert.Equal("/usr/share/nginx/html", svc.Volumes[0].Target);
        Assert.True(svc.Volumes[0].ReadOnly);
    }

    // â”€â”€ Environment â”€â”€

    [Fact]
    public void LoadFromContent_Environment_Mapping()
    {
        var yaml = @"
services:
  web:
    image: nginx
    environment:
      FOO: bar
      BAZ: qux
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Environment);
        Assert.Equal("bar", svc.Environment["FOO"]);
        Assert.Equal("qux", svc.Environment["BAZ"]);
    }

    [Fact]
    public void LoadFromContent_Environment_ListSyntax()
    {
        var yaml = @"
services:
  web:
    image: nginx
    environment:
      - FOO=bar
      - BAZ=qux
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Environment);
        Assert.Equal("bar", svc.Environment["FOO"]);
        Assert.Equal("qux", svc.Environment["BAZ"]);
    }

    // â”€â”€ DependsOn â”€â”€

    [Fact]
    public void LoadFromContent_DependsOn_ListSyntax()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      - db
  db:
    image: postgres
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.DependsOn);
        Assert.True(svc.DependsOn.ContainsKey("db"));
        Assert.Equal("service_started", svc.DependsOn["db"].Condition);
    }

    [Fact]
    public void LoadFromContent_DependsOn_LongSyntax()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      db:
        condition: service_healthy
  db:
    image: postgres
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Equal("service_healthy", svc.DependsOn["db"].Condition);
    }

    // â”€â”€ Networks â”€â”€

    [Fact]
    public void LoadFromContent_Networks_TopLevel()
    {
        var yaml = @"
services:
  web:
    image: nginx
    networks:
      - frontend
networks:
  frontend:
    driver: bridge
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.True(result.Project.Networks.ContainsKey("frontend"));
        Assert.Equal("bridge", result.Project.Networks["frontend"].Driver);
    }

    // â”€â”€ Volumes top-level â”€â”€

    [Fact]
    public void LoadFromContent_Volumes_TopLevel()
    {
        var yaml = @"
services:
  db:
    image: postgres
    volumes:
      - dbdata:/var/lib/postgresql/data
volumes:
  dbdata:
    driver: local
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.True(result.Project.Volumes.ContainsKey("dbdata"));
        Assert.Equal("local", result.Project.Volumes["dbdata"].Driver);
    }

    // â”€â”€ Build config â”€â”€

    [Fact]
    public void LoadFromContent_Build_StringSyntax()
    {
        var yaml = @"
services:
  web:
    build: ./app
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Build);
        Assert.Equal("./app", svc.Build.Context);
    }

    [Fact]
    public void LoadFromContent_Build_ObjectSyntax()
    {
        var yaml = @"
services:
  web:
    build:
      context: ./app
      dockerfile: Dockerfile.dev
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Build);
        Assert.Equal("./app", svc.Build.Context);
        Assert.Equal("Dockerfile.dev", svc.Build.Dockerfile);
    }

    // â”€â”€ Healthcheck â”€â”€

    [Fact]
    public void LoadFromContent_Healthcheck()
    {
        var yaml = @"
services:
  web:
    image: nginx
    healthcheck:
      test: [""CMD"", ""curl"", ""-f"", ""http://localhost""]
      interval: 30s
      timeout: 10s
      retries: 3
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.HealthCheck);
        Assert.Equal(3UL, svc.HealthCheck.Retries);
    }

    // â”€â”€ Command â”€â”€

    [Fact]
    public void LoadFromContent_Command_String()
    {
        var yaml = @"
services:
  web:
    image: nginx
    command: echo hello
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Command);
        Assert.Contains("echo hello", string.Join(" ", svc.Command));
    }

    [Fact]
    public void LoadFromContent_Command_List()
    {
        var yaml = @"
services:
  web:
    image: nginx
    command: [""echo"", ""hello""]
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Command);
        Assert.Equal(2, svc.Command.Parts.Length);
        Assert.Equal("echo", svc.Command.Parts[0]);
        Assert.Equal("hello", svc.Command.Parts[1]);
    }

    // â”€â”€ Variable interpolation â”€â”€

    [Fact]
    public void LoadFromContent_VariableInterpolation()
    {
        var yaml = @"
services:
  web:
    image: nginx:${TAG:-latest}
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Equal("nginx:latest", svc.Image);
    }

    // â”€â”€ Skip interpolation option â”€â”€

    [Fact]
    public void LoadFromContent_SkipInterpolation()
    {
        var yaml = @"
services:
  web:
    image: nginx:${TAG:-latest}
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipInterpolation = true });
        var svc = result.Project.Services["web"];
        Assert.Equal("nginx:${TAG:-latest}", svc.Image);
    }

    // â”€â”€ Project name â”€â”€

    [Fact]
    public void LoadFromContent_ProjectName_FromOption()
    {
        var yaml = @"
services:
  web:
    image: nginx
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ProjectName = "myproject" });
        Assert.Equal("myproject", result.Project.Name);
    }

    [Fact]
    public void LoadFromContent_ProjectName_FromTopLevel()
    {
        var yaml = @"
name: myapp
services:
  web:
    image: nginx
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal("myapp", result.Project.Name);
    }

    // â”€â”€ Labels â”€â”€

    [Fact]
    public void LoadFromContent_Labels()
    {
        var yaml = @"
services:
  web:
    image: nginx
    labels:
      com.example.description: ""My web app""
      com.example.env: ""production""
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.NotNull(svc.Labels);
        Assert.Equal("My web app", svc.Labels["com.example.description"]);
    }

    // â”€â”€ Restart policy â”€â”€

    [Fact]
    public void LoadFromContent_RestartPolicy()
    {
        var yaml = @"
services:
  web:
    image: nginx
    restart: unless-stopped
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Equal("unless-stopped", svc.Restart);
    }

    // â”€â”€ Extends â”€â”€

    [Fact]
    public void LoadFromContent_Extends_SameFile()
    {
        var yaml = @"
services:
  base:
    image: nginx
    environment:
      FOO: bar
  web:
    extends:
      service: base
    ports:
      - ""8080:80""
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Equal("nginx", svc.Image);
        Assert.NotNull(svc.Environment);
        Assert.Equal("bar", svc.Environment["FOO"]);
    }

    // â”€â”€ Cross-file Extends â”€â”€

    [Fact]
    public void LoadFromFile_Extends_CrossFile()
    {
        // Create a temp directory with two compose files
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // Write the base file
            var baseYaml = @"
services:
  base-svc:
    image: nginx:alpine
    environment:
      BASE_VAR: hello
    labels:
      com.example.base: ""true""
";
            File.WriteAllText(Path.Combine(tempDir, "base.yaml"), baseYaml);

            // Write the main file that extends from base
            var mainYaml = @"
services:
  web:
    extends:
      file: base.yaml
      service: base-svc
    ports:
      - ""8080:80""
    environment:
      EXTRA_VAR: world
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var result = ComposeLoader.LoadFromFile(mainPath);
            var svc = result.Project.Services["web"];

            // Should inherit image from base
            Assert.Equal("nginx:alpine", svc.Image);
            // Should inherit environment from base, plus own override
            Assert.NotNull(svc.Environment);
            Assert.Equal("hello", svc.Environment["BASE_VAR"]);
            Assert.Equal("world", svc.Environment["EXTRA_VAR"]);
            // Should inherit labels from base
            Assert.NotNull(svc.Labels);
            Assert.Equal("true", svc.Labels["com.example.base"]);
            // Should have own ports
            Assert.NotNull(svc.Ports);
            Assert.Single(svc.Ports);
            Assert.Equal(80u, svc.Ports[0].Target);
            // extends should be removed from final result
            Assert.Null(svc.Extends);
            // No warnings about unsupported extends
            Assert.DoesNotContain(result.Warnings, w => w.Contains("not yet supported"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Extends_CrossFile_ChainedExtends()
    {
        // Test: A extends from B (in file b.yaml), and B extends from C (in same file b.yaml)
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var baseYaml = @"
services:
  base:
    image: alpine
    environment:
      LEVEL: base
  mid:
    extends:
      service: base
    environment:
      LEVEL: mid
      MID_VAR: present
";
            File.WriteAllText(Path.Combine(tempDir, "base.yaml"), baseYaml);

            var mainYaml = @"
services:
  app:
    extends:
      file: base.yaml
      service: mid
    environment:
      APP_VAR: yes
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var result = ComposeLoader.LoadFromFile(mainPath);
            var svc = result.Project.Services["app"];

            // Should inherit image from base through mid
            Assert.Equal("alpine", svc.Image);
            // mid overrides LEVEL, app doesn't override it
            Assert.Equal("mid", svc.Environment["LEVEL"]);
            Assert.Equal("present", svc.Environment["MID_VAR"]);
            Assert.Equal("yes", svc.Environment["APP_VAR"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Extends_CrossFile_FileNotFound_Warning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mainYaml = @"
services:
  web:
    extends:
      file: nonexistent.yaml
      service: base
    image: nginx
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var ex = Assert.Throws<ComposeNotFoundException>(() => ComposeLoader.LoadFromFile(mainPath));
            Assert.Contains("not found", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Extends_CrossFile_ServiceNotFound_Warning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var baseYaml = @"
services:
  other:
    image: alpine
";
            File.WriteAllText(Path.Combine(tempDir, "base.yaml"), baseYaml);

            var mainYaml = @"
services:
  web:
    extends:
      file: base.yaml
      service: nonexistent
    image: nginx
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var ex = Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromFile(mainPath));
            Assert.Contains("unknown service", ex.Message);
            Assert.Contains("nonexistent", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // â”€â”€ Include â”€â”€

    [Fact]
    public void LoadFromFile_Include_BasicInclude()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var includedYaml = @"
services:
  db:
    image: postgres:15
    environment:
      POSTGRES_DB: mydb
";
            File.WriteAllText(Path.Combine(tempDir, "db.yaml"), includedYaml);

            var mainYaml = @"
include:
  - db.yaml
services:
  web:
    image: nginx
    ports:
      - ""8080:80""
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var result = ComposeLoader.LoadFromFile(mainPath);
            // Should have both services
            Assert.Equal(2, result.Project.Services.Count);
            Assert.True(result.Project.Services.ContainsKey("web"));
            Assert.True(result.Project.Services.ContainsKey("db"));
            Assert.Equal("nginx", result.Project.Services["web"].Image);
            Assert.Equal("postgres:15", result.Project.Services["db"].Image);
            Assert.Equal("mydb", result.Project.Services["db"].Environment["POSTGRES_DB"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Include_LongSyntaxWithProjectDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "subproject");
        Directory.CreateDirectory(subDir);
        try
        {
            var includedYaml = @"
services:
  cache:
    image: redis:7
";
            File.WriteAllText(Path.Combine(subDir, "redis.yaml"), includedYaml);

            var mainYaml = @"
include:
  - path: subproject/redis.yaml
    project_directory: subproject
services:
  web:
    image: nginx
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var result = ComposeLoader.LoadFromFile(mainPath);
            Assert.Equal(2, result.Project.Services.Count);
            Assert.True(result.Project.Services.ContainsKey("cache"));
            Assert.Equal("redis:7", result.Project.Services["cache"].Image);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Include_MainTakesPrecedence()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // Included file defines 'web' with image alpine
            var includedYaml = @"
services:
  web:
    image: alpine
";
            File.WriteAllText(Path.Combine(tempDir, "base.yaml"), includedYaml);

            // Main file also defines 'web' with image nginx â€” main should win
            var mainYaml = @"
include:
  - base.yaml
services:
  web:
    image: nginx
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var result = ComposeLoader.LoadFromFile(mainPath);
            Assert.Equal("nginx", result.Project.Services["web"].Image);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Include_MergesNetworksAndVolumes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var includedYaml = @"
services:
  db:
    image: postgres
networks:
  backend:
    driver: bridge
volumes:
  dbdata:
    driver: local
";
            File.WriteAllText(Path.Combine(tempDir, "infra.yaml"), includedYaml);

            var mainYaml = @"
include:
  - infra.yaml
services:
  web:
    image: nginx
networks:
  frontend:
    driver: bridge
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var result = ComposeLoader.LoadFromFile(mainPath);
            // Services merged
            Assert.Equal(2, result.Project.Services.Count);
            // Networks merged
            Assert.True(result.Project.Networks.ContainsKey("frontend"));
            Assert.True(result.Project.Networks.ContainsKey("backend"));
            // Volumes from included file
            Assert.True(result.Project.Volumes.ContainsKey("dbdata"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Include_FileNotFound_Warning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mainYaml = @"
include:
  - nonexistent.yaml
services:
  web:
    image: nginx
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var ex = Assert.Throws<ComposeNotFoundException>(() => ComposeLoader.LoadFromFile(mainPath));
            Assert.Contains("not found", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // â”€â”€ Schema Validation â”€â”€

    [Fact]
    public void LoadFromFile_Include_CircularInclude_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var aYaml = @"
include:
  - b.yaml
services:
  a:
    image: alpine
";
            var bYaml = @"
include:
  - a.yaml
services:
  b:
    image: busybox
";
            var aPath = Path.Combine(tempDir, "a.yaml");
            File.WriteAllText(aPath, aYaml);
            File.WriteAllText(Path.Combine(tempDir, "b.yaml"), bYaml);

            var ex = Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromFile(aPath));
            Assert.Contains("Circular include", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Include_EnvFileNotFound_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "db.yaml"), @"
services:
  db:
    image: postgres
");

            var mainYaml = @"
include:
  - path: db.yaml
    env_file: missing.env
services:
  web:
    image: nginx
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var ex = Assert.Throws<ComposeNotFoundException>(() => ComposeLoader.LoadFromFile(mainPath));
            Assert.Contains("env_file", ex.Message);
            Assert.Contains("not found", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_Include_EnvFileParseError_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "db.yaml"), @"
services:
  db:
    image: postgres
");
            File.WriteAllText(Path.Combine(tempDir, "broken.env"), "A=\"unterminated");

            var mainYaml = @"
include:
  - path: db.yaml
    env_file: broken.env
services:
  web:
    image: nginx
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var ex = Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromFile(mainPath));
            Assert.Contains("Failed to load env_file", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFile_StandardInputWithoutRedirect_ThrowsUnsupported()
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        var ex = Assert.Throws<ComposeUnsupportedException>(() => ComposeLoader.LoadFromFile("-"));
        Assert.Contains("redirected standard input", ex.Message);
    }

    [Fact]
    public void LoadFromFile_ServiceEnvFileParseError_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "compose_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "broken.env"), "A=\"unterminated");

            var mainYaml = @"
services:
  app:
    image: alpine
    env_file:
      - broken.env
";
            var mainPath = Path.Combine(tempDir, "compose.yaml");
            File.WriteAllText(mainPath, mainYaml);

            var ex = Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromFile(mainPath));
            Assert.Contains("Failed to parse env_file", ex.Message);
            Assert.Contains("broken.env", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromContent_Validation_UnknownTopLevelKey_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
unknown_key: something
";
        var ex = Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromContent(yaml));
        Assert.Contains("Unknown top-level key", ex.Message);
        Assert.Contains("unknown_key", ex.Message);
    }

    [Fact]
    public void LoadFromContent_Validation_ExtensionKeyAllowed()
    {
        var yaml = @"
services:
  web:
    image: nginx
    x-custom: value
x-global: stuff
";
        var result = ComposeLoader.LoadFromContent(yaml);
        // Extension keys should NOT produce warnings
        Assert.DoesNotContain(result.Warnings, w => w.Contains("x-custom"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("x-global"));
    }

    [Fact]
    public void LoadFromContent_Validation_ServiceMissingImageAndBuild_ThrowsOnConsistencyCheck()
    {
        var yaml = @"
services:
  web:
    ports:
      - ""8080:80""
";
        var ex = Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromContent(yaml));
        Assert.Contains("has neither an image nor a build context", ex.Message);
    }

    [Fact]
    public void LoadFromContent_Validation_DependsOnUnknownService_ThrowsOnConsistencyCheck()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      - nonexistent
";
        var ex = Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromContent(yaml));
        Assert.Contains("depends on undefined service", ex.Message);
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void LoadFromContent_Validation_SkipValidation_NoWarningsNoException()
    {
        var yaml = @"
services:
  web:
    ports:
      - ""8080:80""
unknown_key: something
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipValidation = true });
        // With SkipValidation, no schema/logic warnings should appear, and no consistency exception
        Assert.DoesNotContain(result.Warnings, w => w.Contains("Unknown top-level key"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("must define either"));
    }

    // â”€â”€ Version field warning â”€â”€

    [Fact]
    public void LoadFromContent_VersionField_EmitsWarningAndRemoved()
    {
        var yaml = @"
version: '3.8'
services:
  web:
    image: nginx
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Contains(result.Warnings, w => w.Contains("version") && w.Contains("obsolete"));
        // Version field should not cause loading to fail
        Assert.True(result.Project.Services.ContainsKey("web"));
    }

    [Fact]
    public void LoadFromContent_VersionField_SkipValidation_NoWarning()
    {
        var yaml = @"
version: '3.8'
services:
  web:
    image: nginx
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipValidation = true });
        Assert.DoesNotContain(result.Warnings, w => w.Contains("version") && w.Contains("obsolete"));
    }

    [Fact]
    public void LoadFromContent_NoVersionField_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("version") && w.Contains("obsolete"));
    }

    // â”€â”€ ConvertWindowsPaths â”€â”€

    [Fact]
    public void ConvertVolumePath_DriveLetter_Converted()
    {
        var vol = new ServiceVolumeConfig { Source = @"C:\my\path", Target = "/app", Type = "bind" };
        var result = ComposeLoader.ConvertVolumePath(vol);
        Assert.Equal("/c/my/path", result.Source);
    }

    [Fact]
    public void ConvertVolumePath_LowerDriveLetter_Converted()
    {
        var vol = new ServiceVolumeConfig { Source = @"d:\data\db", Target = "/data", Type = "bind" };
        var result = ComposeLoader.ConvertVolumePath(vol);
        Assert.Equal("/d/data/db", result.Source);
    }

    [Fact]
    public void ConvertVolumePath_ForwardSlashDrive_Converted()
    {
        var vol = new ServiceVolumeConfig { Source = "C:/Users/me", Target = "/app", Type = "bind" };
        var result = ComposeLoader.ConvertVolumePath(vol);
        Assert.Equal("/c/Users/me", result.Source);
    }

    [Fact]
    public void ConvertVolumePath_UnixPath_Unchanged()
    {
        var vol = new ServiceVolumeConfig { Source = "/var/data", Target = "/data", Type = "bind" };
        var result = ComposeLoader.ConvertVolumePath(vol);
        Assert.Equal("/var/data", result.Source);
    }

    [Fact]
    public void ConvertVolumePath_NamedVolume_Unchanged()
    {
        var vol = new ServiceVolumeConfig { Source = "mydata", Target = "/data", Type = "volume" };
        var result = ComposeLoader.ConvertVolumePath(vol);
        Assert.Equal("mydata", result.Source);
    }

    [Fact]
    public void ConvertVolumePath_EmptySource_Unchanged()
    {
        var vol = new ServiceVolumeConfig { Source = "", Target = "/data", Type = "bind" };
        var result = ComposeLoader.ConvertVolumePath(vol);
        Assert.Equal("", result.Source);
    }

    [Fact]
    public void GetDriveLetter_ValidDrive_ReturnsLetter()
    {
        Assert.Equal('C', ComposeLoader.GetDriveLetter(@"C:\foo"));
        Assert.Equal('d', ComposeLoader.GetDriveLetter("d:/bar"));
    }

    [Fact]
    public void GetDriveLetter_NoDrive_ReturnsNull()
    {
        Assert.Null(ComposeLoader.GetDriveLetter("/unix/path"));
        Assert.Null(ComposeLoader.GetDriveLetter("relative/path"));
        Assert.Null(ComposeLoader.GetDriveLetter(""));
        Assert.Null(ComposeLoader.GetDriveLetter("c:"));  // no separator after colon
    }
}
