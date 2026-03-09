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

public class ConsistencyCheckTests
{
    /// <summary>Helper: load with consistency checks enabled (default).</summary>
    private static LoadResult Load(string yaml) =>
        ComposeLoader.LoadFromContent(yaml);

    /// <summary>Helper: expect consistency check to throw.</summary>
    private static ComposeInvalidException ExpectConsistencyError(string yaml) =>
        Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromContent(yaml));

    // â”€â”€ 1. Service must have image, build, or provider â”€â”€

    [Fact]
    public void Check_ServiceMissingImageBuildProvider_Throws()
    {
        var yaml = @"
services:
  web:
    ports:
      - ""8080:80""
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("has neither an image nor a build context", ex.Message);
    }

    [Fact]
    public void Check_ServiceWithProvider_NoImageRequired()
    {
        var yaml = @"
services:
  mycloud:
    provider:
      type: aws
";
        // Should not throw â€” provider is sufficient
        var result = Load(yaml);
        Assert.NotNull(result.Project.Services["mycloud"].Provider);
    }

    // â”€â”€ 2. dockerfile and dockerfile_inline mutually exclusive â”€â”€

    [Fact]
    public void Check_DockerfileAndInline_MutuallyExclusive()
    {
        var yaml = @"
services:
  web:
    build:
      context: .
      dockerfile: Dockerfile
      dockerfile_inline: ""FROM alpine""
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("mutually exclusive", ex.Message);
        Assert.Contains("dockerfile", ex.Message);
    }

    // â”€â”€ 4. build.platforms must contain service.platform â”€â”€

    [Fact]
    public void Check_BuildPlatforms_MustContainServicePlatform()
    {
        var yaml = @"
services:
  web:
    build:
      context: .
      platforms:
        - linux/amd64
        - linux/arm64
    platform: linux/s390x
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("platform", ex.Message);
        Assert.Contains("linux/s390x", ex.Message);
    }

    [Fact]
    public void Check_BuildPlatforms_ContainsPlatform_NoError()
    {
        var yaml = @"
services:
  web:
    build:
      context: .
      platforms:
        - linux/amd64
        - linux/arm64
    platform: linux/amd64
";
        var result = Load(yaml);
        Assert.Equal("linux/amd64", result.Project.Services["web"].Platform);
    }

    // â”€â”€ 5. network_mode and networks mutually exclusive â”€â”€

    [Fact]
    public void Check_NetworkModeAndNetworks_MutuallyExclusive()
    {
        var yaml = @"
services:
  web:
    image: nginx
    network_mode: host
    networks:
      - frontend
networks:
  frontend: {}
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("mutually exclusive", ex.Message);
        Assert.Contains("network_mode", ex.Message);
    }

    // â”€â”€ 6. Referenced networks must exist â”€â”€

    [Fact]
    public void Check_UndefinedNetwork_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    networks:
      - nonexistent
networks:
  frontend: {}
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("undefined network", ex.Message);
        Assert.Contains("nonexistent", ex.Message);
    }

    // â”€â”€ 7. healthcheck.test format validation â”€â”€

    [Fact]
    public void Check_HealthcheckTest_InvalidFormat()
    {
        var yaml = @"
services:
  web:
    image: nginx
    healthcheck:
      test: [""INVALID"", ""curl""]
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("healthcheck.test", ex.Message);
        Assert.Contains("CMD", ex.Message);
    }

    [Fact]
    public void Check_HealthcheckTest_ValidCMD_NoError()
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
        var result = Load(yaml);
        Assert.NotNull(result.Project.Services["web"].HealthCheck);
    }

    [Fact]
    public void Check_HealthcheckTest_ValidNONE_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    healthcheck:
      test: [""NONE""]
";
        var result = Load(yaml);
        Assert.NotNull(result.Project.Services["web"].HealthCheck);
    }

    // â”€â”€ 8. depends_on references must exist â”€â”€

    [Fact]
    public void Check_DependsOnUndefined_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      - ghost
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("depends on undefined service", ex.Message);
        Assert.Contains("ghost", ex.Message);
    }

    [Fact]
    public void Check_DependsOnExistingService_NoError()
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
        var result = Load(yaml);
        Assert.True(result.Project.Services["web"].DependsOn.ContainsKey("db"));
    }

    // â”€â”€ 9. network_mode service: references must exist â”€â”€

    [Fact]
    public void Check_NetworkModeServiceRef_Undefined()
    {
        var yaml = @"
services:
  web:
    image: nginx
    network_mode: ""service:ghost""
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("not found", ex.Message);
        Assert.Contains("ghost", ex.Message);
    }

    // â”€â”€ 10. Volume references must exist â”€â”€

    [Fact]
    public void Check_VolumeRefUndefined_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - mydata:/data
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("undefined volume", ex.Message);
        Assert.Contains("mydata", ex.Message);
    }

    [Fact]
    public void Check_VolumeRefDefined_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - mydata:/data
volumes:
  mydata: {}
";
        var result = Load(yaml);
        Assert.Single(result.Project.Services["web"].Volumes);
    }

    // â”€â”€ 12. Config references must exist â”€â”€

    [Fact]
    public void Check_ConfigRefUndefined_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - myconfig
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("undefined config", ex.Message);
        Assert.Contains("myconfig", ex.Message);
    }

    [Fact]
    public void Check_ConfigRefDefined_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - myconfig
configs:
  myconfig:
    file: ./config.txt
";
        var result = Load(yaml);
        Assert.Single(result.Project.Services["web"].Configs);
    }

    // â”€â”€ 14. Secret references must exist â”€â”€

    [Fact]
    public void Check_SecretRefUndefined_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("undefined secret", ex.Message);
        Assert.Contains("mysecret", ex.Message);
    }

    [Fact]
    public void Check_SecretRefDefined_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret:
    file: ./secret.txt
";
        var result = Load(yaml);
        Assert.Single(result.Project.Services["web"].Secrets);
    }

    // â”€â”€ 15. scale and deploy.replicas consistency â”€â”€

    [Fact]
    public void Check_ScaleAndReplicas_Distinct_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    scale: 3
    deploy:
      replicas: 5
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("scale", ex.Message);
        Assert.Contains("deploy.replicas", ex.Message);
    }

    [Fact]
    public void Check_ScaleAndReplicas_Same_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    scale: 3
    deploy:
      replicas: 3
";
        var result = Load(yaml);
        Assert.Equal(3, result.Project.Services["web"].Scale);
        Assert.Equal(3, result.Project.Services["web"].Deploy!.Replicas);
    }

    // â”€â”€ 16. scale/replicas non-negative â”€â”€

    [Fact]
    public void Check_NegativeScale_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    scale: -1
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("scale", ex.Message);
        Assert.Contains("non-negative", ex.Message);
    }

    // â”€â”€ 18. container_name conflicts with scale > 1 â”€â”€

    [Fact]
    public void Check_ContainerName_WithScale_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    container_name: myweb
    scale: 2
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("container_name", ex.Message);
        Assert.Contains("scale", ex.Message);
    }

    [Fact]
    public void Check_ContainerName_Scale1_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    container_name: myweb
    scale: 1
";
        var result = Load(yaml);
        Assert.Equal("myweb", result.Project.Services["web"].ContainerName);
    }

    // â”€â”€ 19. develop.watch target validation â”€â”€

    [Fact]
    public void Check_DevelopWatch_SyncWithoutTarget_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./src
          action: sync
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("target", ex.Message);
        Assert.Contains("develop.watch", ex.Message);
    }

    [Fact]
    public void Check_DevelopWatch_RebuildWithoutTarget_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./src
          action: rebuild
";
        var result = Load(yaml);
        Assert.NotNull(result.Project.Services["web"].Develop);
    }

    [Fact]
    public void Check_DevelopWatch_RestartWithoutTarget_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./src
          action: restart
";
        var result = Load(yaml);
        Assert.NotNull(result.Project.Services["web"].Develop);
    }

    [Fact]
    public void Check_DevelopWatch_SyncWithTarget_NoError()
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
";
        var result = Load(yaml);
        Assert.NotNull(result.Project.Services["web"].Develop);
    }

    // â”€â”€ 20. Mount conflict detection (tmpfs + volumes) â”€â”€

    [Fact]
    public void Check_MountConflict_TmpfsAndVolume_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    tmpfs:
      - /tmp
    volumes:
      - type: tmpfs
        target: /tmp
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("/tmp", ex.Message);
        Assert.Contains("already mounted", ex.Message);
    }

    // â”€â”€ 21. Top-level secret validation â”€â”€

    [Fact]
    public void Check_SecretMissingFileAndEnvironment_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret: {}
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("secret", ex.Message);
        Assert.Contains("file", ex.Message);
        Assert.Contains("environment", ex.Message);
    }

    [Fact]
    public void Check_SecretWithFile_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret:
    file: ./secret.txt
";
        var result = Load(yaml);
        Assert.True(result.Project.Secrets.ContainsKey("mysecret"));
    }

    [Fact]
    public void Check_SecretWithEnvironment_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret:
    environment: MY_SECRET_VAR
";
        var result = Load(yaml);
        Assert.True(result.Project.Secrets.ContainsKey("mysecret"));
    }

    [Fact]
    public void Check_ExternalSecret_NoFileRequired()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret:
    external: true
";
        var result = Load(yaml);
        Assert.True(result.Project.Secrets["mysecret"].External);
    }

    // â”€â”€ 22. Dependency cycle detection â”€â”€

    [Fact]
    public void Check_DependencyCycle_Throws()
    {
        var yaml = @"
services:
  a:
    image: nginx
    depends_on:
      - b
  b:
    image: nginx
    depends_on:
      - c
  c:
    image: nginx
    depends_on:
      - a
";
        var ex = ExpectConsistencyError(yaml);
        Assert.Contains("cycle", ex.Message);
    }

    [Fact]
    public void Check_NoCycle_NoError()
    {
        var yaml = @"
services:
  a:
    image: nginx
    depends_on:
      - b
  b:
    image: nginx
    depends_on:
      - c
  c:
    image: nginx
";
        var result = Load(yaml);
        Assert.Equal(3, result.Project.Services.Count);
    }

    // â”€â”€ SkipConsistencyCheck option â”€â”€

    [Fact]
    public void Check_SkippedWithOption()
    {
        var yaml = @"
services:
  web:
    ports:
      - ""8080:80""
";
        // Should not throw when consistency check is skipped
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipConsistencyCheck = true });
        Assert.NotNull(result.Project);
    }

    [Fact]
    public void Check_SkippedWithSkipValidation()
    {
        var yaml = @"
services:
  web:
    ports:
      - ""8080:80""
";
        // SkipValidation should also skip consistency check
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipValidation = true });
        Assert.NotNull(result.Project);
    }
}
