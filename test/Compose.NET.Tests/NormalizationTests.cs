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

public class NormalizationTests
{
    // â”€â”€ pull_policy normalization â”€â”€

    [Fact]
    public void Normalize_PullPolicy_IfNotPresent_BecomeMissing()
    {
        var yaml = @"
services:
  web:
    image: nginx
    pull_policy: if_not_present
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal("missing", result.Project.Services["web"].PullPolicy);
    }

    [Fact]
    public void Normalize_PullPolicy_Always_Unchanged()
    {
        var yaml = @"
services:
  web:
    image: nginx
    pull_policy: always
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal("always", result.Project.Services["web"].PullPolicy);
    }

    [Fact]
    public void Normalize_PullPolicy_Missing_Unchanged()
    {
        var yaml = @"
services:
  web:
    image: nginx
    pull_policy: missing
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal("missing", result.Project.Services["web"].PullPolicy);
    }

    // â”€â”€ Build defaults â”€â”€

    [Fact]
    public void Normalize_Build_DefaultContext()
    {
        var yaml = @"
services:
  web:
    build:
      dockerfile: Dockerfile.dev
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        Assert.NotNull(result.Project.Services["web"].Build);
        Assert.Equal(".", result.Project.Services["web"].Build!.Context);
    }

    [Fact]
    public void Normalize_Build_DefaultDockerfile()
    {
        var yaml = @"
services:
  web:
    build:
      context: ./app
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        Assert.NotNull(result.Project.Services["web"].Build);
        Assert.Equal("Dockerfile", result.Project.Services["web"].Build!.Dockerfile);
    }

    [Fact]
    public void Normalize_Build_DockerfileInline_NoDefaultDockerfile()
    {
        var yaml = @"
services:
  web:
    build:
      context: ./app
      dockerfile_inline: |
        FROM alpine
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            ResolvePaths = false,
            SkipConsistencyCheck = true
        });
        var build = result.Project.Services["web"].Build;
        Assert.NotNull(build);
        // When dockerfile_inline is present, dockerfile should NOT be set to default
        Assert.True(string.IsNullOrEmpty(build!.Dockerfile));
    }

    [Fact]
    public void Normalize_Build_ExplicitContextPreserved()
    {
        var yaml = @"
services:
  web:
    build:
      context: ./custom
      dockerfile: Custom.Dockerfile
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var build = result.Project.Services["web"].Build;
        Assert.NotNull(build);
        Assert.Equal("./custom", build!.Context);
        Assert.Equal("Custom.Dockerfile", build.Dockerfile);
    }

    // â”€â”€ Default network injection â”€â”€

    [Fact]
    public void Normalize_DefaultNetwork_Injected_WhenNoNetworks()
    {
        var yaml = @"
services:
  web:
    image: nginx
";
        var result = ComposeLoader.LoadFromContent(yaml);
        // Service should get "default" network
        Assert.True(result.Project.Services["web"].Networks.ContainsKey("default"));
        // Top-level should have "default" network
        Assert.True(result.Project.Networks.ContainsKey("default"));
    }

    [Fact]
    public void Normalize_DefaultNetwork_NotInjected_WhenNetworkModeSet()
    {
        var yaml = @"
services:
  web:
    image: nginx
    network_mode: host
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipConsistencyCheck = true });
        // Service with network_mode should NOT get default network
        Assert.Empty(result.Project.Services["web"].Networks);
    }

    [Fact]
    public void Normalize_DefaultNetwork_NotInjected_WhenExplicitNetworks()
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
        // Service should only have frontend, not default
        Assert.True(result.Project.Services["web"].Networks.ContainsKey("frontend"));
        Assert.False(result.Project.Services["web"].Networks.ContainsKey("default"));
    }

    [Fact]
    public void Normalize_DefaultNetwork_AutoCreated_InTopLevel()
    {
        var yaml = @"
services:
  web:
    image: nginx
  db:
    image: postgres
";
        var result = ComposeLoader.LoadFromContent(yaml);
        // Both services should get default
        Assert.True(result.Project.Services["web"].Networks.ContainsKey("default"));
        Assert.True(result.Project.Services["db"].Networks.ContainsKey("default"));
        // Top-level should have default network
        Assert.True(result.Project.Networks.ContainsKey("default"));
    }

    [Fact]
    public void Normalize_DefaultNetwork_ExistingTopLevel_AddedIfUsed()
    {
        var yaml = @"
services:
  web:
    image: nginx
    networks:
      - frontend
  db:
    image: postgres
networks:
  frontend:
    driver: bridge
";
        var result = ComposeLoader.LoadFromContent(yaml);
        // db has no explicit networks, should get default
        Assert.True(result.Project.Services["db"].Networks.ContainsKey("default"));
        // default should be auto-added to top-level networks
        Assert.True(result.Project.Networks.ContainsKey("default"));
        Assert.True(result.Project.Networks.ContainsKey("frontend"));
    }

    // â”€â”€ links â†’ depends_on â”€â”€

    [Fact]
    public void Normalize_Links_CreateDependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    links:
      - db
  db:
    image: postgres
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.True(svc.DependsOn.ContainsKey("db"));
        Assert.Equal("service_started", svc.DependsOn["db"].Condition);
        Assert.True(svc.DependsOn["db"].Restart);
        Assert.True(svc.DependsOn["db"].Required);
    }

    [Fact]
    public void Normalize_Links_WithAlias_CreateDependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    links:
      - db:database
  db:
    image: postgres
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        // link "db:database" should create depends_on for "db" (not "database")
        Assert.True(svc.DependsOn.ContainsKey("db"));
    }

    [Fact]
    public void Normalize_Links_DontOverrideExistingDependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      db:
        condition: service_healthy
    links:
      - db
  db:
    image: postgres
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        // Existing depends_on should be preserved (service_healthy, not overwritten to service_started)
        Assert.Equal("service_healthy", svc.DependsOn["db"].Condition);
    }

    // â”€â”€ Namespace refs â†’ depends_on â”€â”€

    [Fact]
    public void Normalize_NetworkMode_ServiceRef_CreatesDependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    network_mode: ""service:proxy""
  proxy:
    image: envoy
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipConsistencyCheck = true });
        var svc = result.Project.Services["web"];
        Assert.True(svc.DependsOn.ContainsKey("proxy"));
        Assert.Equal("service_started", svc.DependsOn["proxy"].Condition);
        Assert.True(svc.DependsOn["proxy"].Restart);
    }

    [Fact]
    public void Normalize_Pid_ServiceRef_CreatesDependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    pid: ""service:sidecar""
  sidecar:
    image: alpine
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.True(svc.DependsOn.ContainsKey("sidecar"));
    }

    [Fact]
    public void Normalize_Ipc_ServiceRef_CreatesDependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ipc: ""service:shared""
  shared:
    image: alpine
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.True(svc.DependsOn.ContainsKey("shared"));
    }

    // â”€â”€ volumes_from â†’ depends_on â”€â”€

    [Fact]
    public void Normalize_VolumesFrom_CreatesDependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes_from:
      - datacontainer
  datacontainer:
    image: alpine
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.True(svc.DependsOn.ContainsKey("datacontainer"));
        Assert.Equal("service_started", svc.DependsOn["datacontainer"].Condition);
        // volumes_from uses restart=false (unlike links)
        Assert.False(svc.DependsOn["datacontainer"].Restart);
    }

    [Fact]
    public void Normalize_VolumesFrom_ContainerPrefix_Skipped()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes_from:
      - ""container:external_data""
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipConsistencyCheck = true });
        var svc = result.Project.Services["web"];
        // container: prefix should not create depends_on
        Assert.Empty(svc.DependsOn);
    }

    [Fact]
    public void Normalize_VolumesFrom_WithMode_CreatesDependsOn()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes_from:
      - ""datacontainer:ro""
  datacontainer:
    image: alpine
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        // "datacontainer:ro" â†’ depends on "datacontainer"
        Assert.True(svc.DependsOn.ContainsKey("datacontainer"));
    }

    // â”€â”€ Volume target path cleanup â”€â”€

    [Fact]
    public void Normalize_VolumeTarget_CleanPath()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - type: bind
        source: ./data
        target: /usr/share/nginx/html/./extra/../
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var vol = result.Project.Services["web"].Volumes[0];
        // Path should be cleaned (no . or ..)
        Assert.Equal("/usr/share/nginx/html", vol.Target);
    }

    // â”€â”€ Resource name prefixing (SetNameFromKey) â”€â”€

    [Fact]
    public void Normalize_NetworkNames_Prefixed()
    {
        var yaml = @"
name: myproject
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
        Assert.Equal("myproject_frontend", result.Project.Networks["frontend"].Name);
    }

    [Fact]
    public void Normalize_VolumeNames_Prefixed()
    {
        var yaml = @"
name: myproject
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
        Assert.Equal("myproject_dbdata", result.Project.Volumes["dbdata"].Name);
    }

    [Fact]
    public void Normalize_ExternalNetwork_UsesKeyAsName()
    {
        var yaml = @"
name: myproject
services:
  web:
    image: nginx
    networks:
      - existing
networks:
  existing:
    external: true
";
        var result = ComposeLoader.LoadFromContent(yaml);
        // External resources should use key directly, not prefixed
        Assert.Equal("existing", result.Project.Networks["existing"].Name);
    }

    [Fact]
    public void Normalize_ExternalVolume_UsesKeyAsName()
    {
        var yaml = @"
name: myproject
services:
  db:
    image: postgres
    volumes:
      - shared:/data
volumes:
  shared:
    external: true
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal("shared", result.Project.Volumes["shared"].Name);
    }

    [Fact]
    public void Normalize_ExplicitName_Preserved()
    {
        var yaml = @"
name: myproject
services:
  web:
    image: nginx
    networks:
      - frontend
networks:
  frontend:
    name: my-custom-network
";
        var result = ComposeLoader.LoadFromContent(yaml);
        // Explicit name should be preserved, not overwritten
        Assert.Equal("my-custom-network", result.Project.Networks["frontend"].Name);
    }

    [Fact]
    public void Normalize_DefaultNetwork_Prefixed()
    {
        var yaml = @"
name: myproject
services:
  web:
    image: nginx
";
        var result = ComposeLoader.LoadFromContent(yaml);
        // Auto-created default network should also be prefixed
        Assert.Equal("myproject_default", result.Project.Networks["default"].Name);
    }

    // â”€â”€ SkipNormalization â”€â”€

    [Fact]
    public void Normalize_Skipped_WhenOptionSet()
    {
        var yaml = @"
services:
  web:
    image: nginx
    pull_policy: if_not_present
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            SkipNormalization = true,
            SkipConsistencyCheck = true
        });
        // With SkipNormalization, pull_policy should remain "if_not_present"
        Assert.Equal("if_not_present", result.Project.Services["web"].PullPolicy);
    }

    // â”€â”€ Provider services don't need networks â”€â”€

    [Fact]
    public void Normalize_ProviderService_NoDefaultNetwork()
    {
        var yaml = @"
services:
  mycloud:
    provider:
      type: aws
      options:
        region:
          - us-east-1
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipConsistencyCheck = true });
        // Provider services should NOT get default network
        Assert.Empty(result.Project.Services["mycloud"].Networks);
    }
}
