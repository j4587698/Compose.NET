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
using Compose.NET.Serialization;
using Compose.NET.Types;
using System.Text.Json;

namespace Compose.NET.Tests;

public class SerializerTests
{
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Round-trip: parse YAML â†’ Project â†’ ToYaml â†’ parse again â†’ compare
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_MinimalService()
    {
        var yaml = @"
services:
  web:
    image: nginx:latest
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("nginx:latest", project2.Services["web"].Image);
    }

    [Fact]
    public void RoundTrip_MultipleServicesPreserved()
    {
        var yaml = @"
services:
  api:
    image: myapi:1.0
  db:
    image: postgres:15
  cache:
    image: redis:7
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal(3, project2.Services.Count);
        Assert.Equal("myapi:1.0", project2.Services["api"].Image);
        Assert.Equal("postgres:15", project2.Services["db"].Image);
        Assert.Equal("redis:7", project2.Services["cache"].Image);
    }

    [Fact]
    public void RoundTrip_PortsLongSyntax()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - target: 80
        published: ""8080""
        protocol: tcp
        mode: ingress
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var port = project2.Services["web"].Ports[0];
        Assert.Equal(80u, port.Target);
        Assert.Equal("8080", port.Published);
    }

    [Fact]
    public void RoundTrip_PortsShortSyntax()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - ""8080:80""
      - ""9090:90/udp""
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal(2, project2.Services["web"].Ports.Count);
        Assert.Equal(80u, project2.Services["web"].Ports[0].Target);
        Assert.Equal("8080", project2.Services["web"].Ports[0].Published);
        Assert.Equal(90u, project2.Services["web"].Ports[1].Target);
        Assert.Equal("udp", project2.Services["web"].Ports[1].Protocol);
    }

    [Fact]
    public void RoundTrip_Volumes()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - type: bind
        source: ./data
        target: /app/data
      - type: volume
        source: myvolume
        target: /data
volumes:
  myvolume:
    driver: local
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal(2, project2.Services["web"].Volumes.Count);
        Assert.True(project2.Volumes.ContainsKey("myvolume"));
        Assert.Equal("local", project2.Volumes["myvolume"].Driver);
    }

    [Fact]
    public void RoundTrip_Networks()
    {
        var yaml = @"
services:
  web:
    image: nginx
    networks:
      frontend:
        aliases:
          - web-alias
      backend: {}
networks:
  frontend:
    driver: bridge
  backend:
    internal: true
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.True(project2.Networks.ContainsKey("frontend"));
        Assert.Equal("bridge", project2.Networks["frontend"].Driver);
        Assert.True(project2.Networks["backend"].Internal);

        var svcNet = project2.Services["web"].Networks["frontend"];
        Assert.NotNull(svcNet);
        Assert.Contains("web-alias", svcNet!.Aliases);
    }

    [Fact]
    public void RoundTrip_Environment()
    {
        var yaml = @"
services:
  web:
    image: nginx
    environment:
      FOO: bar
      DB_HOST: localhost
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("bar", project2.Services["web"].Environment["FOO"]);
        Assert.Equal("localhost", project2.Services["web"].Environment["DB_HOST"]);
    }

    [Fact]
    public void RoundTrip_Labels()
    {
        var yaml = @"
services:
  web:
    image: nginx
    labels:
      com.example.description: ""My web app""
      com.example.env: production
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("My web app", project2.Services["web"].Labels["com.example.description"]);
        Assert.Equal("production", project2.Services["web"].Labels["com.example.env"]);
    }

    [Fact]
    public void RoundTrip_DependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      db:
        condition: service_healthy
        restart: true
  db:
    image: postgres
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var dep = project2.Services["web"].DependsOn["db"];
        Assert.Equal("service_healthy", dep.Condition);
        Assert.True(dep.Restart);
    }

    [Fact]
    public void RoundTrip_Deploy()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: ""0.5""
          memory: 512MB
        reservations:
          cpus: ""0.25""
          memory: 256MB
      restart_policy:
        condition: on-failure
        max_attempts: 5
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var deploy = project2.Services["web"].Deploy;
        Assert.NotNull(deploy);
        Assert.Equal(3, deploy!.Replicas);
        Assert.NotNull(deploy.Resources.Limits);
        Assert.Equal(0.5f, deploy.Resources.Limits!.NanoCPUs.Value);
        Assert.NotNull(deploy.RestartPolicy);
        Assert.Equal("on-failure", deploy.RestartPolicy!.Condition);
        Assert.Equal(5UL, deploy.RestartPolicy.MaxAttempts);
    }

    [Fact]
    public void RoundTrip_HealthCheck()
    {
        var yaml = @"
services:
  web:
    image: nginx
    healthcheck:
      test:
        - CMD
        - curl
        - -f
        - http://localhost
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 5s
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var hc = project2.Services["web"].HealthCheck;
        Assert.NotNull(hc);
        Assert.Equal(4, hc!.Test.Count);
        Assert.Equal("CMD", hc.Test[0]);
        Assert.Equal(3UL, hc.Retries);
    }

    [Fact]
    public void RoundTrip_Logging()
    {
        var yaml = @"
services:
  web:
    image: nginx
    logging:
      driver: json-file
      options:
        max-size: 10m
        max-file: ""3""
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var logging = project2.Services["web"].Logging;
        Assert.NotNull(logging);
        Assert.Equal("json-file", logging!.Driver);
        Assert.Equal("10m", logging.Options["max-size"]);
    }

    [Fact]
    public void RoundTrip_Secrets()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - source: my_secret
        target: /run/secrets/my_secret
secrets:
  my_secret:
    file: ./secret.txt
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Single(project2.Services["web"].Secrets);
        Assert.Equal("my_secret", project2.Services["web"].Secrets[0].Source);
        Assert.True(project2.Secrets.ContainsKey("my_secret"));
        // File path gets resolved to absolute by the loader normalizer
        Assert.Equal(project.Secrets["my_secret"].File, project2.Secrets["my_secret"].File);
    }

    [Fact]
    public void RoundTrip_Configs()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - source: my_config
        target: /etc/my_config
configs:
  my_config:
    file: ./config.txt
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Single(project2.Services["web"].Configs);
        Assert.Equal("my_config", project2.Services["web"].Configs[0].Source);
        Assert.True(project2.Configs.ContainsKey("my_config"));
    }

    [Fact]
    public void RoundTrip_ProjectName()
    {
        var yaml = @"
name: myproject
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("myproject", project2.Name);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // ShellCommand serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_ShellCommand_ShellForm()
    {
        var yaml = @"
services:
  web:
    image: nginx
    command: echo hello world
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        // Should serialize as a single string, not a list
        Assert.Contains("command: echo hello world", output);
    }

    [Fact]
    public void Serialize_ShellCommand_ExecForm()
    {
        var yaml = @"
services:
  web:
    image: nginx
    command:
      - echo
      - hello
      - world
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        // Should serialize as a list
        Assert.Contains("- echo", output);
        Assert.Contains("- hello", output);
        Assert.Contains("- world", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Duration serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_Duration_GoStyle()
    {
        var yaml = @"
services:
  web:
    image: nginx
    healthcheck:
      test: [""CMD"", ""true""]
      interval: 30s
      timeout: 1m30s
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        Assert.Contains("interval: 30s", output);
        Assert.Contains("timeout: 1m30s", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // UnitBytes serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_UnitBytes()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      resources:
        limits:
          memory: 512MB
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        // Memory should appear as human-readable unit string
        Assert.Contains("memory: 512MB", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Ports: default values omitted
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_MemSwappiness_UsesCorrectKeyAndRoundTrips()
    {
        var project = new Project
        {
            Services = new Services
            {
                ["web"] = new ServiceConfig
                {
                    Name = "web",
                    Image = "nginx",
                    MemSwappiness = 30
                }
            }
        };

        var output = ProjectSerializer.ToYaml(project);
        Assert.Contains("mem_swappiness: 30", output);
        Assert.DoesNotContain("memswappiness", output);

        var project2 = ComposeLoader.LoadFromContent(output).Project;
        Assert.Equal(30, project2.Services["web"].MemSwappiness);
    }

    [Fact]
    public void Serialize_Ports_DefaultProtocolAndModeOmitted()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - target: 80
        published: ""8080""
        protocol: tcp
        mode: ingress
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        // protocol=tcp and mode=ingress are defaults and should NOT be emitted
        Assert.DoesNotContain("protocol:", output);
        Assert.DoesNotContain("mode:", output);
        Assert.Contains("target: 80", output);
        Assert.Contains("published: 8080", output);
    }

    [Fact]
    public void Serialize_Ports_NonDefaultProtocolEmitted()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - target: 53
        published: ""53""
        protocol: udp
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        Assert.Contains("protocol: udp", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Ulimits serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_Ulimits_SingleValue()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ulimits:
      nproc: 65535
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        // Single-value ulimit should be just the int
        Assert.Contains("nproc: 65535", output);
    }

    [Fact]
    public void Serialize_Ulimits_SoftHard()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ulimits:
      nofile:
        soft: 20000
        hard: 40000
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        Assert.Contains("soft: 20000", output);
        Assert.Contains("hard: 40000", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Extensions flattening
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_ExtensionsFlattened()
    {
        var yaml = @"
x-logging: &logging
  driver: json-file
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        // x-logging should be at root level, not nested under "extensions:"
        Assert.Contains("x-logging:", output);
        Assert.DoesNotContain("extensions:", output);
    }

    [Fact]
    public void Serialize_ServiceExtensionsFlattened()
    {
        var yaml = @"
services:
  web:
    image: nginx
    x-custom: test-value
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        Assert.Contains("x-custom: test-value", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // JSON output
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var yaml = @"
services:
  web:
    image: nginx:latest
    ports:
      - ""8080:80""
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var json = ProjectSerializer.ToJson(project);

        // Should be valid JSON
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);

        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("services", out var services));
        Assert.True(services.TryGetProperty("web", out var web));
        Assert.Equal("nginx:latest", web.GetProperty("image").GetString());
    }

    [Fact]
    public void ToJson_Indented()
    {
        var yaml = @"
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var json = ProjectSerializer.ToJson(project, indented: true);

        // Indented JSON should contain newlines
        Assert.Contains("\n", json);
    }

    [Fact]
    public void ToJson_NotIndented()
    {
        var yaml = @"
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var json = ProjectSerializer.ToJson(project, indented: false);

        // Non-indented JSON should NOT contain newlines (single line)
        Assert.DoesNotContain("\n", json);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Empty/default values omitted
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_EmptyCollectionsOmitted()
    {
        // Use a Project directly (not via loader) to avoid normalizer injecting defaults
        var project = new Project();
        project.Services["web"] = new ServiceConfig { Name = "web", Image = "nginx" };
        var output = ProjectSerializer.ToYaml(project);

        // Empty collections should not appear
        Assert.DoesNotContain("volumes:", output);
        Assert.DoesNotContain("networks:", output);
        Assert.DoesNotContain("configs:", output);
        Assert.DoesNotContain("secrets:", output);
        Assert.DoesNotContain("ports:", output);
        Assert.DoesNotContain("environment:", output);
        Assert.DoesNotContain("labels:", output);
    }

    [Fact]
    public void Serialize_RuntimeFieldsOmitted()
    {
        // Project runtime fields like WorkingDir, ComposeFiles, etc. should not appear
        var yaml = @"
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        project.WorkingDir = "/some/path";
        project.ComposeFiles = new List<string> { "docker-compose.yml" };
        var output = ProjectSerializer.ToYaml(project);

        Assert.DoesNotContain("working_dir: /some/path", output);
        Assert.DoesNotContain("compose_files:", output);
        Assert.DoesNotContain("docker-compose.yml", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // DependsOn: required defaults
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_DependsOn_RequiredTrue_NotEmitted()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      db:
        condition: service_started
  db:
    image: postgres
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        // required=true is the default, so it should NOT appear in output
        Assert.DoesNotContain("required:", output);
    }

    [Fact]
    public void Serialize_DependsOn_RequiredFalse_Emitted()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      db:
        condition: service_started
        required: false
  db:
    image: postgres
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        Assert.Contains("required: false", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // ExtraHosts serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_ExtraHosts()
    {
        var yaml = @"
services:
  web:
    image: nginx
    extra_hosts:
      - ""myhost:192.168.1.1""
      - ""otherhost:10.0.0.1""
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal(2, project2.Services["web"].ExtraHosts.Count);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // DNS serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_DNS()
    {
        var yaml = @"
services:
  web:
    image: nginx
    dns:
      - 8.8.8.8
      - 8.8.4.4
    dns_search:
      - example.com
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal(2, project2.Services["web"].DNS.Count);
        Assert.Contains("8.8.8.8", project2.Services["web"].DNS);
        Assert.Single(project2.Services["web"].DNSSearch);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // CapAdd / CapDrop
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_Capabilities()
    {
        var yaml = @"
services:
  web:
    image: nginx
    cap_add:
      - SYS_ADMIN
    cap_drop:
      - NET_RAW
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Contains("SYS_ADMIN", project2.Services["web"].CapAdd);
        Assert.Contains("NET_RAW", project2.Services["web"].CapDrop);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Complex round-trip
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_ComplexCompose()
    {
        var yaml = @"
name: myapp
services:
  web:
    image: nginx:latest
    ports:
      - ""8080:80""
      - ""443:443""
    environment:
      NODE_ENV: production
      DB_HOST: db
    depends_on:
      db:
        condition: service_healthy
      cache:
        condition: service_started
    networks:
      frontend: {}
      backend:
        aliases:
          - webservice
    volumes:
      - type: bind
        source: ./html
        target: /usr/share/nginx/html
    healthcheck:
      test:
        - CMD
        - curl
        - -f
        - http://localhost
      interval: 30s
      timeout: 10s
      retries: 3
    deploy:
      replicas: 2
      resources:
        limits:
          cpus: ""1""
          memory: 512MB
    labels:
      app: web
      tier: frontend
  db:
    image: postgres:15
    environment:
      POSTGRES_PASSWORD: secret
    volumes:
      - type: volume
        source: pgdata
        target: /var/lib/postgresql/data
  cache:
    image: redis:7
networks:
  frontend:
    driver: bridge
  backend:
    internal: true
volumes:
  pgdata:
    driver: local
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        // Verify all major aspects survived the round-trip
        Assert.Equal("myapp", project2.Name);
        Assert.Equal(3, project2.Services.Count);

        var web = project2.Services["web"];
        Assert.Equal("nginx:latest", web.Image);
        Assert.Equal(2, web.Ports.Count);
        Assert.Equal("production", web.Environment["NODE_ENV"]);
        Assert.Equal("service_healthy", web.DependsOn["db"].Condition);
        // web explicitly declares frontend + backend only (normalizer only adds default for services with no networks)
        Assert.Equal(2, web.Networks.Count);
        Assert.Single(web.Volumes);
        Assert.NotNull(web.HealthCheck);
        Assert.Equal(3UL, web.HealthCheck!.Retries);
        Assert.Equal(2, web.Deploy!.Replicas);
        Assert.Equal("web", web.Labels["app"]);

        // Top-level networks includes "default" added by normalizer for db/cache services
        Assert.Equal(3, project2.Networks.Count);
        Assert.Equal("bridge", project2.Networks["frontend"].Driver);
        Assert.True(project2.Networks["backend"].Internal);

        Assert.Single(project2.Volumes);
        Assert.Equal("local", project2.Volumes["pgdata"].Driver);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Null project throws
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ToYaml_NullProject_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProjectSerializer.ToYaml(null!));
    }

    [Fact]
    public void ToJson_NullProject_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProjectSerializer.ToJson(null!));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // ProjectToDict internal tests
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ProjectToDict_EmptyProject_ReturnsEmptyDict()
    {
        var project = new Project();
        var dict = ProjectSerializer.ProjectToDict(project);

        // No services â†’ no services key
        Assert.DoesNotContain("services", dict.Keys);
        Assert.DoesNotContain("networks", dict.Keys);
        Assert.DoesNotContain("volumes", dict.Keys);
    }

    [Fact]
    public void ProjectToDict_ServicesSortedAlphabetically()
    {
        var project = new Project();
        project.Services["zebra"] = new ServiceConfig { Name = "zebra", Image = "z" };
        project.Services["alpha"] = new ServiceConfig { Name = "alpha", Image = "a" };
        project.Services["mid"] = new ServiceConfig { Name = "mid", Image = "m" };

        var dict = ProjectSerializer.ProjectToDict(project);
        var services = (Dictionary<string, object?>)dict["services"]!;
        var keys = services.Keys.ToList();

        Assert.Equal("alpha", keys[0]);
        Assert.Equal("mid", keys[1]);
        Assert.Equal("zebra", keys[2]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Build config serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_BuildConfig()
    {
        var yaml = @"
services:
  web:
    build:
      context: ./app
      dockerfile: Dockerfile.prod
      args:
        BUILD_ENV: production
      target: builder
      no_cache: true
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var build = project2.Services["web"].Build;
        Assert.NotNull(build);
        // context gets resolved to absolute path by normalizer, so verify it round-trips consistently
        Assert.Equal(project.Services["web"].Build!.Context, build!.Context);
        // dockerfile also gets resolved to absolute path relative to context
        Assert.Equal(project.Services["web"].Build!.Dockerfile, build.Dockerfile);
        Assert.Equal("production", build.Args["BUILD_ENV"]);
        Assert.Equal("builder", build.Target);
        Assert.True(build.NoCache);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Develop config
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_DevelopWatch()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./src
          action: sync
          target: /app/src
          ignore:
            - node_modules/
        - path: ./package.json
          action: rebuild
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var dev = project2.Services["web"].Develop;
        Assert.NotNull(dev);
        Assert.Equal(2, dev!.Watch.Count);
        Assert.Equal("./src", dev.Watch[0].Path);
        Assert.Equal("sync", dev.Watch[0].Action);
        Assert.Contains("node_modules/", dev.Watch[0].Ignore);
        Assert.Equal("rebuild", dev.Watch[1].Action);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Privileged / ReadOnly / StdinOpen / Tty
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_BooleanFlags()
    {
        var yaml = @"
services:
  web:
    image: nginx
    privileged: true
    read_only: true
    stdin_open: true
    tty: true
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.True(project2.Services["web"].Privileged);
        Assert.True(project2.Services["web"].ReadOnly);
        Assert.True(project2.Services["web"].StdinOpen);
        Assert.True(project2.Services["web"].Tty);
    }

    [Fact]
    public void Serialize_FalseBooleanFlags_NotEmitted()
    {
        var yaml = @"
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);

        Assert.DoesNotContain("privileged:", output);
        Assert.DoesNotContain("read_only:", output);
        Assert.DoesNotContain("stdin_open:", output);
        Assert.DoesNotContain("tty:", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // IPAM config
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_IPAM()
    {
        var yaml = @"
networks:
  mynet:
    driver: bridge
    ipam:
      driver: default
      config:
        - subnet: 172.28.0.0/16
          gateway: 172.28.0.1
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var ipam = project2.Networks["mynet"].Ipam;
        Assert.Equal("default", ipam.Driver);
        Assert.Single(ipam.Config);
        Assert.Equal("172.28.0.0/16", ipam.Config[0].Subnet);
        Assert.Equal("172.28.0.1", ipam.Config[0].Gateway);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Entrypoint serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_Entrypoint_ShellForm()
    {
        var yaml = @"
services:
  web:
    image: nginx
    entrypoint: /docker-entrypoint.sh
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var entrypoint = Assert.IsType<ShellCommand>(project2.Services["web"].Entrypoint);
        Assert.True(entrypoint.IsShellForm);
        Assert.Equal("/docker-entrypoint.sh", entrypoint.Parts[0]);
    }

    [Fact]
    public void RoundTrip_Entrypoint_ExecForm()
    {
        var yaml = @"
services:
  web:
    image: nginx
    entrypoint:
      - /bin/sh
      - -c
      - echo hello
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var entrypoint = Assert.IsType<ShellCommand>(project2.Services["web"].Entrypoint);
        Assert.False(entrypoint.IsShellForm);
        Assert.Equal(3, entrypoint.Parts.Length);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // CPU properties
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_CpuProperties()
    {
        var yaml = @"
services:
  web:
    image: nginx
    cpu_shares: 512
    cpuset: ""0,1""
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal(512, project2.Services["web"].CPUShares);
        Assert.Equal("0,1", project2.Services["web"].CPUSet);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Restart policy
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_RestartPolicy()
    {
        var yaml = @"
services:
  web:
    image: nginx
    restart: unless-stopped
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("unless-stopped", project2.Services["web"].Restart);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Profiles serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_Profiles()
    {
        var yaml = @"
services:
  web:
    image: nginx
    profiles:
      - debug
      - dev
";
        var project = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { Profiles = new List<string> { "debug" } }).Project;
        var output = ProjectSerializer.ToYaml(project);

        Assert.Contains("profiles:", output);
        Assert.Contains("debug", output);
        Assert.Contains("dev", output);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // StopGracePeriod
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_StopGracePeriod()
    {
        var yaml = @"
services:
  web:
    image: nginx
    stop_grace_period: 30s
    stop_signal: SIGTERM
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.NotNull(project2.Services["web"].StopGracePeriod);
        Assert.Equal("SIGTERM", project2.Services["web"].StopSignal);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Sysctls / Annotations / Tmpfs
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_Sysctls()
    {
        var yaml = @"
services:
  web:
    image: nginx
    sysctls:
      net.core.somaxconn: ""1024""
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("1024", project2.Services["web"].Sysctls["net.core.somaxconn"]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Scale
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_Scale()
    {
        var yaml = @"
services:
  web:
    image: nginx
    scale: 3
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal(3, project2.Services["web"].Scale);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Init (nullable bool)
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_Init()
    {
        var yaml = @"
services:
  web:
    image: nginx
    init: true
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.True(project2.Services["web"].Init);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Devices
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_Devices()
    {
        var yaml = @"
services:
  web:
    image: nginx
    devices:
      - source: /dev/sda
        target: /dev/xvdc
        permissions: rwm
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Single(project2.Services["web"].Devices);
        Assert.Equal("/dev/sda", project2.Services["web"].Devices[0].Source);
        Assert.Equal("/dev/xvdc", project2.Services["web"].Devices[0].Target);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Extends
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_Extends()
    {
        // We can't truly round-trip extends (it gets resolved), so test serialization directly
        var project = new Project();
        var svc = new ServiceConfig
        {
            Name = "web",
            Image = "nginx",
            Extends = new ExtendsConfig { File = "base.yml", Service = "base-web" }
        };
        project.Services["web"] = svc;

        var dict = ProjectSerializer.ProjectToDict(project);
        var services = (Dictionary<string, object?>)dict["services"]!;
        var webDict = (Dictionary<string, object?>)services["web"]!;
        var ext = (Dictionary<string, object?>)webDict["extends"]!;

        Assert.Equal("base.yml", ext["file"]);
        Assert.Equal("base-web", ext["service"]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // CredentialSpec
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_CredentialSpec()
    {
        var project = new Project();
        var svc = new ServiceConfig
        {
            Name = "web",
            Image = "nginx",
            CredentialSpec = new CredentialSpecConfig { File = "my_credential_spec.json" }
        };
        project.Services["web"] = svc;

        var dict = ProjectSerializer.ProjectToDict(project);
        var services = (Dictionary<string, object?>)dict["services"]!;
        var webDict = (Dictionary<string, object?>)services["web"]!;
        var cs = (Dictionary<string, object?>)webDict["credential_spec"]!;

        Assert.Equal("my_credential_spec.json", cs["file"]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // BlkioConfig
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_BlkioConfig()
    {
        var yaml = @"
services:
  web:
    image: nginx
    blkio_config:
      weight: 300
      device_read_bps:
        - path: /dev/sda
          rate: 12MB
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.NotNull(project2.Services["web"].BlkioConfig);
        Assert.Equal(300, (int)project2.Services["web"].BlkioConfig!.Weight);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Volume config with labels
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_VolumeLabels()
    {
        var yaml = @"
volumes:
  data:
    driver: local
    labels:
      com.example.type: data
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("data", project2.Volumes["data"].Labels["com.example.type"]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // External network/volume
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_ExternalNetwork()
    {
        var yaml = @"
networks:
  existing:
    external: true
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.True(project2.Networks["existing"].External);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Models (top-level)
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_TopLevelModels()
    {
        var project = new Project();
        project.Services["web"] = new ServiceConfig { Name = "web", Image = "nginx" };
        project.Models["gpt4"] = new ModelConfig
        {
            Name = "gpt4",
            Model = "gpt-4-turbo",
            ContextSize = 128000,
        };

        var dict = ProjectSerializer.ProjectToDict(project);
        Assert.True(dict.ContainsKey("models"));
        var models = (Dictionary<string, object?>)dict["models"]!;
        var gpt4 = (Dictionary<string, object?>)models["gpt4"]!;
        Assert.Equal("gpt4", gpt4["name"]);
        Assert.Equal("gpt-4-turbo", gpt4["model"]);
        Assert.Equal(128000, gpt4["context_size"]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Service-level models
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_ServiceModels()
    {
        var project = new Project();
        var svc = new ServiceConfig
        {
            Name = "web",
            Image = "nginx",
        };
        svc.ServiceModels["mymodel"] = new ServiceModelConfig
        {
            EndpointVariable = "MODEL_ENDPOINT",
            ModelVariable = "MODEL_NAME",
        };
        project.Services["web"] = svc;

        var dict = ProjectSerializer.ProjectToDict(project);
        var services = (Dictionary<string, object?>)dict["services"]!;
        var webDict = (Dictionary<string, object?>)services["web"]!;
        var models = (Dictionary<string, object?>)webDict["models"]!;
        var mymodel = (Dictionary<string, object?>)models["mymodel"]!;
        Assert.Equal("MODEL_ENDPOINT", mymodel["endpoint_variable"]);
        Assert.Equal("MODEL_NAME", mymodel["model_variable"]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Service hooks (post_start / pre_stop)
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_ServiceHooks()
    {
        var project = new Project();
        var svc = new ServiceConfig
        {
            Name = "web",
            Image = "nginx",
        };
        svc.PostStart.Add(new ServiceHook
        {
            Command = ShellCommand.FromShellForm("echo started"),
            User = "root",
        });
        svc.PreStop.Add(new ServiceHook
        {
            Command = ShellCommand.FromExecForm(new[] { "cleanup", "--force" }),
        });
        project.Services["web"] = svc;

        var dict = ProjectSerializer.ProjectToDict(project);
        var services = (Dictionary<string, object?>)dict["services"]!;
        var webDict = (Dictionary<string, object?>)services["web"]!;

        var postStart = (List<object?>)webDict["post_start"]!;
        Assert.Single(postStart);
        var hook = (Dictionary<string, object?>)postStart[0]!;
        Assert.Equal("echo started", hook["command"]);
        Assert.Equal("root", hook["user"]);

        var preStop = (List<object?>)webDict["pre_stop"]!;
        Assert.Single(preStop);
        var hook2 = (Dictionary<string, object?>)preStop[0]!;
        var cmd = (List<object?>)hook2["command"]!;
        Assert.Equal("cleanup", cmd[0]);
        Assert.Equal("--force", cmd[1]);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // EnvFile serialization
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_EnvFile()
    {
        var yaml = @"
services:
  web:
    image: nginx
    env_file:
      - path: .env
        required: false
      - path: .env.local
";
        // Use SkipResolveEnvironment to avoid file-not-found for actual .env files
        var opts = new LoadOptions { SkipResolveEnvironment = true };
        var project = ComposeLoader.LoadFromContent(yaml, options: opts).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output, options: opts).Project;

        Assert.Equal(2, project2.Services["web"].EnvFiles.Count);
        // env_file paths get resolved to absolute by the loader
        Assert.Equal(project.Services["web"].EnvFiles[0].Path, project2.Services["web"].EnvFiles[0].Path);
        Assert.False(project2.Services["web"].EnvFiles[0].Required);
        Assert.Equal(project.Services["web"].EnvFiles[1].Path, project2.Services["web"].EnvFiles[1].Path);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Network EnableIPv6
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_NetworkEnableIPv6()
    {
        var yaml = @"
networks:
  mynet:
    enable_ipv6: true
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.True(project2.Networks["mynet"].EnableIPv6);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Deploy update_config / rollback_config
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_DeployUpdateConfig()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      update_config:
        parallelism: 2
        delay: 10s
        failure_action: rollback
        order: start-first
      rollback_config:
        parallelism: 1
        order: stop-first
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var update = project2.Services["web"].Deploy!.UpdateConfig;
        Assert.NotNull(update);
        Assert.Equal(2UL, update!.Parallelism);
        Assert.Equal("rollback", update.FailureAction);
        Assert.Equal("start-first", update.Order);

        var rollback = project2.Services["web"].Deploy!.RollbackConfig;
        Assert.NotNull(rollback);
        Assert.Equal(1UL, rollback!.Parallelism);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Placement
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_Placement()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      placement:
        constraints:
          - node.role == manager
        preferences:
          - spread: datacenter
        max_replicas_per_node: 2
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var placement = project2.Services["web"].Deploy!.Placement;
        Assert.Contains("node.role == manager", placement.Constraints);
        Assert.Single(placement.Preferences);
        Assert.Equal("datacenter", placement.Preferences[0].Spread);
        Assert.Equal(2, (int)placement.MaxReplicas);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Bind volume options
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_BindVolumeOptions()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - type: bind
        source: ./data
        target: /data
        read_only: true
        bind:
          propagation: shared
          create_host_path: true
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var vol = project2.Services["web"].Volumes[0];
        Assert.True(vol.ReadOnly);
        Assert.NotNull(vol.Bind);
        Assert.Equal("shared", vol.Bind!.Propagation);
        Assert.True(vol.Bind.CreateHostPath);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Secret/Config with file/environment/content
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_SecretWithContent()
    {
        var yaml = @"
secrets:
  inline_secret:
    content: supersecret
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("supersecret", project2.Secrets["inline_secret"].Content);
    }

    [Fact]
    public void RoundTrip_SecretWithEnvironment()
    {
        var yaml = @"
secrets:
  env_secret:
    environment: MY_SECRET_ENV
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        Assert.Equal("MY_SECRET_ENV", project2.Secrets["env_secret"].Environment);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // JSON round-trip
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ToJson_ComplexProject_ValidStructure()
    {
        var yaml = @"
name: testapp
services:
  web:
    image: nginx:latest
    ports:
      - ""8080:80""
    environment:
      FOO: bar
    deploy:
      replicas: 2
networks:
  frontend:
    driver: bridge
volumes:
  data:
    driver: local
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var json = ProjectSerializer.ToJson(project);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // name
        Assert.Equal("testapp", root.GetProperty("name").GetString());

        // services.web.image
        Assert.Equal("nginx:latest",
            root.GetProperty("services").GetProperty("web").GetProperty("image").GetString());

        // networks.frontend.driver
        Assert.Equal("bridge",
            root.GetProperty("networks").GetProperty("frontend").GetProperty("driver").GetString());

        // volumes.data.driver
        Assert.Equal("local",
            root.GetProperty("volumes").GetProperty("data").GetProperty("driver").GetString());
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Verify service Name is NOT emitted
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Serialize_ServiceNameNotEmitted()
    {
        var yaml = @"
services:
  web:
    image: nginx
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var dict = ProjectSerializer.ProjectToDict(project);
        var services = (Dictionary<string, object?>)dict["services"]!;
        var webDict = (Dictionary<string, object?>)services["web"]!;

        // Service Name should NOT be in the dict (it's the key)
        Assert.DoesNotContain("name", webDict.Keys);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Verify resource Device
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RoundTrip_DeployDeviceRequest()
    {
        var yaml = @"
services:
  gpu:
    image: nvidia/cuda
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 2
              capabilities:
                - gpu
";
        var project = ComposeLoader.LoadFromContent(yaml).Project;
        var output = ProjectSerializer.ToYaml(project);
        var project2 = ComposeLoader.LoadFromContent(output).Project;

        var devs = project2.Services["gpu"].Deploy!.Resources.Reservations!.Devices;
        Assert.Single(devs);
        Assert.Equal("nvidia", devs[0].Driver);
        Assert.Equal(2, devs[0].Count);
        Assert.Contains("gpu", devs[0].Capabilities);
    }
}
