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

public class TypeTests
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // DevelopConfig
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void DevelopConfig_WatchSync()
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
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var dev = result.Project.Services["web"].Develop;
        Assert.NotNull(dev);
        Assert.Single(dev!.Watch);
        Assert.Equal("./src", dev.Watch[0].Path);
        Assert.Equal(WatchActions.Sync, dev.Watch[0].Action);
        Assert.Equal("/app/src", dev.Watch[0].Target);
    }

    [Fact]
    public void DevelopConfig_WatchRebuild()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./Dockerfile
          action: rebuild
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var dev = result.Project.Services["web"].Develop;
        Assert.NotNull(dev);
        Assert.Equal(WatchActions.Rebuild, dev!.Watch[0].Action);
        Assert.Null(dev.Watch[0].Target);
    }

    [Fact]
    public void DevelopConfig_WatchRestart()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./config.yaml
          action: restart
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var dev = result.Project.Services["web"].Develop;
        Assert.NotNull(dev);
        Assert.Equal(WatchActions.Restart, dev!.Watch[0].Action);
    }

    [Fact]
    public void DevelopConfig_WatchSyncRestart()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./assets
          action: sync+restart
          target: /app/assets
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var dev = result.Project.Services["web"].Develop;
        Assert.NotNull(dev);
        Assert.Equal(WatchActions.SyncRestart, dev!.Watch[0].Action);
    }

    [Fact]
    public void DevelopConfig_WatchWithIgnore()
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
            - ""*.pyc""
            - __pycache__
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var dev = result.Project.Services["web"].Develop;
        Assert.NotNull(dev);
        Assert.Equal(2, dev!.Watch[0].Ignore.Count);
        Assert.Contains("*.pyc", dev.Watch[0].Ignore);
        Assert.Contains("__pycache__", dev.Watch[0].Ignore);
    }

    [Fact]
    public void DevelopConfig_MultipleWatchers()
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
        - path: ./Dockerfile
          action: rebuild
        - path: ./config
          action: restart
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { ResolvePaths = false });
        var dev = result.Project.Services["web"].Develop;
        Assert.NotNull(dev);
        Assert.Equal(3, dev!.Watch.Count);
        Assert.Equal(WatchActions.Sync, dev.Watch[0].Action);
        Assert.Equal(WatchActions.Rebuild, dev.Watch[1].Action);
        Assert.Equal(WatchActions.Restart, dev.Watch[2].Action);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ModelConfig
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void ModelConfig_TopLevel()
    {
        var yaml = @"
services:
  web:
    image: nginx
models:
  gpt4:
    model: gpt-4
    context_size: 8192
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.True(result.Project.Models.ContainsKey("gpt4"));
        var model = result.Project.Models["gpt4"];
        Assert.Equal("gpt-4", model.Model);
        Assert.Equal(8192, model.ContextSize);
    }

    [Fact]
    public void ModelConfig_WithRuntimeFlags()
    {
        var yaml = @"
services:
  web:
    image: nginx
models:
  llama:
    model: llama-3
    context_size: 4096
    runtime_flags:
      - --no-gpu
      - --threads=8
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var model = result.Project.Models["llama"];
        Assert.Equal("llama-3", model.Model);
        Assert.Equal(2, model.RuntimeFlags.Count);
        Assert.Contains("--no-gpu", model.RuntimeFlags);
        Assert.Contains("--threads=8", model.RuntimeFlags);
    }

    [Fact]
    public void ModelConfig_MultipleModels()
    {
        var yaml = @"
services:
  web:
    image: nginx
models:
  gpt4:
    model: gpt-4
    context_size: 8192
  llama:
    model: llama-3
    context_size: 4096
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal(2, result.Project.Models.Count);
        Assert.True(result.Project.Models.ContainsKey("gpt4"));
        Assert.True(result.Project.Models.ContainsKey("llama"));
    }

    [Fact]
    public void ModelConfig_ServiceModels()
    {
        var yaml = @"
services:
  web:
    image: nginx
    models:
      gpt4:
        endpoint_variable: OPENAI_ENDPOINT
        model_variable: MODEL_NAME
models:
  gpt4:
    model: gpt-4
    context_size: 8192
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.True(svc.ServiceModels.ContainsKey("gpt4"));
        var svcModel = svc.ServiceModels["gpt4"];
        Assert.NotNull(svcModel);
        Assert.Equal("OPENAI_ENDPOINT", svcModel!.EndpointVariable);
        Assert.Equal("MODEL_NAME", svcModel.ModelVariable);
    }

    [Fact]
    public void ModelConfig_ProjectModelNames()
    {
        var yaml = @"
services:
  web:
    image: nginx
models:
  beta:
    model: beta-model
  alpha:
    model: alpha-model
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var names = result.Project.ModelNames();
        // Should be sorted
        Assert.Equal(2, names.Count);
        Assert.Equal("alpha", names[0]);
        Assert.Equal("beta", names[1]);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ProviderConfig
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void ProviderConfig_BasicType()
    {
        var yaml = @"
services:
  mycloud:
    provider:
      type: aws
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["mycloud"];
        Assert.NotNull(svc.Provider);
        Assert.Equal("aws", svc.Provider!.Type);
    }

    [Fact]
    public void ProviderConfig_WithOptions()
    {
        var yaml = @"
services:
  mycloud:
    provider:
      type: aws
      options:
        region:
          - us-east-1
        instance_type:
          - t3.micro
          - t3.small
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipConsistencyCheck = true });
        var prov = result.Project.Services["mycloud"].Provider;
        Assert.NotNull(prov);
        Assert.Equal("aws", prov!.Type);
        Assert.True(prov.Options.ContainsKey("region"));
        Assert.Single(prov.Options["region"]);
        Assert.Equal("us-east-1", prov.Options["region"][0]);
        Assert.Equal(2, prov.Options["instance_type"].Count);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ServiceHook
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void ServiceHook_PostStart()
    {
        var yaml = @"
services:
  web:
    image: nginx
    post_start:
      - command: echo started
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Single(svc.PostStart);
    }

    [Fact]
    public void ServiceHook_PreStop()
    {
        var yaml = @"
services:
  web:
    image: nginx
    pre_stop:
      - command: echo stopping
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Single(svc.PreStop);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // DeployConfig binding
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void DeployConfig_Replicas()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      replicas: 3
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal(3, result.Project.Services["web"].Deploy!.Replicas);
    }

    [Fact]
    public void DeployConfig_Resources_Limits()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      resources:
        limits:
          cpus: ""0.5""
          memory: 512M
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var limits = result.Project.Services["web"].Deploy!.Resources.Limits;
        Assert.NotNull(limits);
        Assert.True(limits!.NanoCPUs > 0);
        Assert.True(limits.MemoryBytes.Bytes > 0);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Extensions
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Extensions_ServiceLevel()
    {
        var yaml = @"
services:
  web:
    image: nginx
    x-custom-field: myvalue
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.True(svc.Extensions.ContainsKey("x-custom-field"));
    }

    [Fact]
    public void Extensions_ProjectLevel()
    {
        var yaml = @"
services:
  web:
    image: nginx
x-global-config:
  key: value
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.True(result.Project.Extensions.ContainsKey("x-global-config"));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Gpus binding
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Gpus_DeviceRequest()
    {
        var yaml = @"
services:
  ml:
    image: tensorflow
    gpus:
      - capabilities:
          - gpu
        count: 1
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["ml"];
        Assert.Single(svc.Gpus);
        Assert.Contains("gpu", svc.Gpus[0].Capabilities);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Misc types
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void ExtraHosts_Binding()
    {
        var yaml = @"
services:
  web:
    image: nginx
    extra_hosts:
      - ""somehost:10.0.0.1""
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Single(svc.ExtraHosts);
    }

    [Fact]
    public void Tmpfs_StringList()
    {
        var yaml = @"
services:
  web:
    image: nginx
    tmpfs:
      - /run
      - /tmp
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.Equal(2, svc.Tmpfs.Count);
        Assert.Contains("/run", svc.Tmpfs);
        Assert.Contains("/tmp", svc.Tmpfs);
    }

    [Fact]
    public void Ulimits_Binding()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ulimits:
      nofile:
        soft: 1024
        hard: 2048
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var svc = result.Project.Services["web"];
        Assert.True(svc.Ulimits.ContainsKey("nofile"));
        Assert.Equal(1024, svc.Ulimits["nofile"].Soft);
        Assert.Equal(2048, svc.Ulimits["nofile"].Hard);
    }

    [Fact]
    public void Scale_Binding()
    {
        var yaml = @"
services:
  web:
    image: nginx
    scale: 3
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.Equal(3, result.Project.Services["web"].Scale);
        Assert.Equal(3, result.Project.Services["web"].GetScale());
    }

    [Fact]
    public void CredentialSpec_Binding()
    {
        var yaml = @"
services:
  web:
    image: nginx
    credential_spec:
      file: credspec.json
";
        var result = ComposeLoader.LoadFromContent(yaml);
        Assert.NotNull(result.Project.Services["web"].CredentialSpec);
    }
}
