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

public class DeepCopyTests
{
    // â”€â”€ Project.DeepCopy() â”€â”€

    [Fact]
    public void Project_DeepCopy_ReturnsIndependentInstance()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        Assert.NotSame(original, copy);
        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.WorkingDir, copy.WorkingDir);
    }

    [Fact]
    public void Project_DeepCopy_ServicesAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        // Modify the copy â€” original should be unchanged
        copy.Services["web"].Image = "modified:latest";
        Assert.Equal("nginx:latest", original.Services["web"].Image);
    }

    [Fact]
    public void Project_DeepCopy_ServiceListsAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.Services["web"].Ports.Add(new ServicePortConfig { Target = 9999 });
        Assert.Single(original.Services["web"].Ports);
    }

    [Fact]
    public void Project_DeepCopy_NetworksAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.Networks["frontend"].Driver = "overlay";
        Assert.Equal("bridge", original.Networks["frontend"].Driver);
    }

    [Fact]
    public void Project_DeepCopy_VolumesAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.Volumes["data"].Driver = "nfs";
        Assert.Equal("local", original.Volumes["data"].Driver);
    }

    [Fact]
    public void Project_DeepCopy_SecretsAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.Secrets["db_pass"].File = "/other/path";
        Assert.Equal("/run/secrets/db_pass", original.Secrets["db_pass"].File);
    }

    [Fact]
    public void Project_DeepCopy_ConfigsAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.Configs["app_config"].File = "/other/config";
        Assert.Equal("/configs/app.conf", original.Configs["app_config"].File);
    }

    [Fact]
    public void Project_DeepCopy_ExtensionsAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.Extensions["x-custom"] = "modified";
        Assert.Equal("value", original.Extensions["x-custom"]);
    }

    [Fact]
    public void Project_DeepCopy_ComposeFilesAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.ComposeFiles.Add("extra.yml");
        Assert.Single(original.ComposeFiles);
    }

    [Fact]
    public void Project_DeepCopy_ProfilesAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.Profiles.Add("production");
        Assert.Single(original.Profiles);
    }

    [Fact]
    public void Project_DeepCopy_DisabledServicesAreIndependent()
    {
        var original = CreateSampleProject();
        var copy = original.DeepCopy();

        copy.DisabledServices["debug"].Image = "changed";
        Assert.Equal("debug-tools", original.DisabledServices["debug"].Image);
    }

    // â”€â”€ ServiceConfig.DeepCopy() â”€â”€

    [Fact]
    public void ServiceConfig_DeepCopy_BasicProperties()
    {
        var original = new ServiceConfig
        {
            Name = "web",
            Image = "nginx",
            ContainerName = "web-container",
            Hostname = "web-host",
            Privileged = true,
            ReadOnly = true,
        };

        var copy = original.DeepCopy();

        Assert.NotSame(original, copy);
        Assert.Equal("web", copy.Name);
        Assert.Equal("nginx", copy.Image);
        Assert.Equal("web-container", copy.ContainerName);
        Assert.True(copy.Privileged);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_LabelsAreIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Labels = new Labels(new Dictionary<string, string> { ["app"] = "web" })
        };

        var copy = original.DeepCopy();
        copy.Labels["app"] = "modified";

        Assert.Equal("web", original.Labels["app"]);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_EnvironmentIsIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Environment = new MappingWithEquals(
                new Dictionary<string, string?> { ["FOO"] = "bar" })
        };

        var copy = original.DeepCopy();
        copy.Environment["FOO"] = "changed";

        Assert.Equal("bar", original.Environment["FOO"]);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_PortsAreIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Ports = new List<ServicePortConfig>
            {
                new() { Target = 80, Published = "8080", Protocol = "tcp" }
            }
        };

        var copy = original.DeepCopy();
        copy.Ports[0].Target = 9090;

        Assert.Equal(80u, original.Ports[0].Target);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_VolumesAreIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Volumes = new List<ServiceVolumeConfig>
            {
                new() { Type = "bind", Source = "/host", Target = "/container" }
            }
        };

        var copy = original.DeepCopy();
        copy.Volumes[0].Source = "/other";

        Assert.Equal("/host", original.Volumes[0].Source);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_BuildConfigIsIndependent()
    {
        var original = new ServiceConfig
        {
            Build = new BuildConfig
            {
                Context = "./app",
                Dockerfile = "Dockerfile",
                Args = new Dictionary<string, string?> { ["VERSION"] = "1.0" },
                Labels = new Labels(new Dictionary<string, string> { ["build"] = "true" }),
                Tags = new List<string> { "latest" }
            }
        };

        var copy = original.DeepCopy();
        copy.Build!.Context = "./other";
        copy.Build.Args["VERSION"] = "2.0";
        copy.Build.Tags.Add("v2");

        Assert.Equal("./app", original.Build!.Context);
        Assert.Equal("1.0", original.Build.Args["VERSION"]);
        Assert.Single(original.Build.Tags);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_DeployConfigIsIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Deploy = new DeployConfig
            {
                Replicas = 3,
                Labels = new Labels(new Dictionary<string, string> { ["deploy"] = "yes" }),
                Resources = new Resources
                {
                    Limits = new Resource { NanoCPUs = 0.5f, MemoryBytes = new UnitBytes(536870912) }
                }
            }
        };

        var copy = original.DeepCopy();
        copy.Deploy!.Replicas = 5;
        copy.Deploy.Resources.Limits!.NanoCPUs = 1.0f;

        Assert.Equal(3, original.Deploy!.Replicas);
        Assert.Equal(0.5f, original.Deploy.Resources.Limits!.NanoCPUs.Value);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_DependsOnIsIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            DependsOn = new DependsOnConfig
            {
                ["db"] = new ServiceDependency
                {
                    Condition = "service_healthy",
                    Required = true
                }
            }
        };

        var copy = original.DeepCopy();
        copy.DependsOn!["db"].Required = false;

        Assert.True(original.DependsOn["db"].Required);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_HealthCheckIsIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            HealthCheck = new HealthCheckConfig
            {
                Test = new List<string> { "CMD", "curl", "-f", "http://localhost" },
                Interval = Duration.Parse("30s"),
                Retries = 3
            }
        };

        var copy = original.DeepCopy();
        copy.HealthCheck!.Test.Add("extra");
        copy.HealthCheck.Retries = 5;

        Assert.Equal(4, original.HealthCheck!.Test.Count);
        Assert.Equal(3UL, original.HealthCheck.Retries);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_CommandIsIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Command = ShellCommand.FromExecForm(new[] { "nginx", "-g", "daemon off;" })
        };

        var copy = original.DeepCopy();

        Assert.NotSame(original.Command, copy.Command);
        Assert.Equal(original.Command.Parts, copy.Command!.Parts);
        Assert.Equal(original.Command.IsShellForm, copy.Command.IsShellForm);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_ShellFormCommandIsIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Command = ShellCommand.FromShellForm("echo hello")
        };

        var copy = original.DeepCopy();
        Assert.NotSame(original.Command, copy.Command);
        Assert.True(copy.Command!.IsShellForm);
        Assert.Equal("echo hello", copy.Command.Parts[0]);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_NetworksAreIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Networks = new Dictionary<string, ServiceNetworkConfig?>
            {
                ["frontend"] = new ServiceNetworkConfig
                {
                    Aliases = new List<string> { "web" },
                    Ipv4Address = "172.20.0.5"
                }
            }
        };

        var copy = original.DeepCopy();
        copy.Networks["frontend"]!.Ipv4Address = "172.20.0.99";
        copy.Networks["frontend"]!.Aliases.Add("another");

        Assert.Equal("172.20.0.5", original.Networks["frontend"]!.Ipv4Address);
        Assert.Single(original.Networks["frontend"]!.Aliases);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_ExtraHostsAreIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            ExtraHosts = HostsList.FromStrings(new[] { "host1:1.2.3.4" })
        };

        var copy = original.DeepCopy();
        copy.ExtraHosts.Add(new HostEntry("host2", "5.6.7.8"));

        Assert.Single(original.ExtraHosts);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_ExtensionsAreIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Extensions = new Extensions(new Dictionary<string, object>
            {
                ["x-custom"] = "value"
            })
        };

        var copy = original.DeepCopy();
        copy.Extensions["x-custom"] = "changed";

        Assert.Equal("value", original.Extensions["x-custom"]);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_UlimitsAreIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Ulimits = new Dictionary<string, UlimitsConfig>
            {
                ["nofile"] = new UlimitsConfig { Soft = 65536, Hard = 65536 }
            }
        };

        var copy = original.DeepCopy();
        copy.Ulimits["nofile"].Soft = 1024;

        Assert.Equal(65536, original.Ulimits["nofile"].Soft);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_NullBuildRemains_Null()
    {
        var original = new ServiceConfig { Image = "nginx" };
        var copy = original.DeepCopy();
        Assert.Null(copy.Build);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_SecretsAreIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Secrets = new List<ServiceSecretConfig>
            {
                new() { Source = "db_pass", Target = "/run/secrets/db_pass" }
            }
        };

        var copy = original.DeepCopy();
        copy.Secrets[0].Target = "/other/path";

        Assert.Equal("/run/secrets/db_pass", original.Secrets[0].Target);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_DevelopConfigIsIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            Develop = new DevelopConfig
            {
                Watch = new List<Trigger>
                {
                    new()
                    {
                        Path = "./src",
                        Action = "sync",
                        Target = "/app/src",
                        Ignore = new List<string> { "*.tmp" }
                    }
                }
            }
        };

        var copy = original.DeepCopy();
        copy.Develop!.Watch[0].Ignore.Add("*.log");

        Assert.Single(original.Develop!.Watch[0].Ignore);
    }

    [Fact]
    public void ServiceConfig_DeepCopy_BlkioConfigIsIndependent()
    {
        var original = new ServiceConfig
        {
            Image = "nginx",
            BlkioConfig = new BlkioConfig
            {
                Weight = 300,
                WeightDevice = new List<WeightDevice>
                {
                    new() { Path = "/dev/sda", Weight = 400 }
                }
            }
        };

        var copy = original.DeepCopy();
        copy.BlkioConfig!.Weight = 100;
        copy.BlkioConfig.WeightDevice[0].Weight = 200;

        Assert.Equal((ushort)300, original.BlkioConfig!.Weight);
        Assert.Equal((ushort)400, original.BlkioConfig.WeightDevice[0].Weight);
    }

    // â”€â”€ Helper to create a complete sample project â”€â”€

    private static Project CreateSampleProject()
    {
        return new Project
        {
            Name = "test-project",
            WorkingDir = "/app",
            ComposeFiles = new List<string> { "docker-compose.yml" },
            Profiles = new List<string> { "dev" },
            Environment = new Mapping(new Dictionary<string, string> { ["ENV"] = "test" }),
            Extensions = new Extensions(new Dictionary<string, object> { ["x-custom"] = "value" }),
            Services = new Services
            {
                ["web"] = new ServiceConfig
                {
                    Name = "web",
                    Image = "nginx:latest",
                    Ports = new List<ServicePortConfig>
                    {
                        new() { Target = 80, Published = "8080", Protocol = "tcp", Mode = "ingress" }
                    },
                    Labels = new Labels(new Dictionary<string, string> { ["app"] = "web" }),
                    Environment = new MappingWithEquals(
                        new Dictionary<string, string?> { ["PORT"] = "8080" }),
                    DependsOn = new DependsOnConfig
                    {
                        ["db"] = new ServiceDependency { Condition = "service_started", Required = true }
                    }
                },
                ["db"] = new ServiceConfig
                {
                    Name = "db",
                    Image = "postgres:15",
                    Volumes = new List<ServiceVolumeConfig>
                    {
                        new() { Type = "volume", Source = "data", Target = "/var/lib/postgresql/data" }
                    }
                }
            },
            Networks = new Networks
            {
                ["frontend"] = new NetworkConfig { Name = "frontend", Driver = "bridge" }
            },
            Volumes = new Volumes
            {
                ["data"] = new VolumeConfig { Name = "data", Driver = "local" }
            },
            Secrets = new Secrets
            {
                ["db_pass"] = new SecretConfig { File = "/run/secrets/db_pass" }
            },
            Configs = new Configs
            {
                ["app_config"] = new ConfigObjConfig { File = "/configs/app.conf" }
            },
            DisabledServices = new Services
            {
                ["debug"] = new ServiceConfig { Name = "debug", Image = "debug-tools" }
            }
        };
    }
}
