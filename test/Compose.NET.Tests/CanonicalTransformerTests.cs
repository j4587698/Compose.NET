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

namespace Compose.NET.Tests;

public class CanonicalTransformerTests
{
    // â”€â”€ PathMatches â”€â”€

    [Theory]
    [InlineData("services.web.ports", "services.*.ports", true)]
    [InlineData("services.web.build", "services.*.build", true)]
    [InlineData("services.web.depends_on", "services.*.depends_on", true)]
    [InlineData("volumes.mydata", "volumes.*", true)]
    [InlineData("networks.default", "networks.*", true)]
    [InlineData("services.web.ports", "services.*.build", false)]
    [InlineData("services.web", "services.*.ports", false)]
    [InlineData("other.web.ports", "services.*.ports", false)]
    public void PathMatches_Tests(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, CanonicalTransformer.PathMatches(path, pattern));
    }

    // â”€â”€ TransformBuild: string â†’ {"context": string} â”€â”€

    [Fact]
    public void Transform_Build_StringToContext()
    {
        var input = CreateServiceDict("build", "./app");
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        Assert.Equal("./app", build["context"]);
    }

    // â”€â”€ TransformDependsOn: list â†’ dict with defaults â”€â”€

    [Fact]
    public void Transform_DependsOn_ListToDict()
    {
        var input = CreateServiceDict("depends_on", new List<object?> { "db", "redis" });
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var deps = (Dictionary<string, object?>)svc["depends_on"]!;

        Assert.True(deps.ContainsKey("db"));
        Assert.True(deps.ContainsKey("redis"));

        var dbDep = (Dictionary<string, object?>)deps["db"]!;
        Assert.Equal("service_started", dbDep["condition"]);
        Assert.Equal(true, dbDep["required"]);
    }

    // â”€â”€ TransformEnvFile: string â†’ list of objects â”€â”€

    [Fact]
    public void Transform_EnvFile_StringToObjectList()
    {
        var input = CreateServiceDict("env_file", ".env");
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var envFile = (List<object?>)svc["env_file"]!;

        Assert.Single(envFile);
        var entry = (Dictionary<string, object?>)envFile[0]!;
        Assert.Equal(".env", entry["path"]);
        Assert.Equal(true, entry["required"]);
    }

    // â”€â”€ TransformExtends: string â†’ {"service": string} â”€â”€

    [Fact]
    public void Transform_Extends_StringToDict()
    {
        var input = CreateServiceDict("extends", "base-service");
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var extends_ = (Dictionary<string, object?>)svc["extends"]!;
        Assert.Equal("base-service", extends_["service"]);
    }

    // â”€â”€ TransformNetworks: list â†’ dict â”€â”€

    [Fact]
    public void Transform_Networks_ListToDict()
    {
        var input = CreateServiceDict("networks", new List<object?> { "frontend", "backend" });
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var nets = (Dictionary<string, object?>)svc["networks"]!;

        Assert.True(nets.ContainsKey("frontend"));
        Assert.True(nets.ContainsKey("backend"));
    }

    // â”€â”€ TransformPorts: string â†’ port config dict â”€â”€

    [Fact]
    public void Transform_Ports_StringToPortConfig()
    {
        var input = CreateServiceDict("ports", new List<object?> { "8080:80/tcp" });
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var ports = (List<object?>)svc["ports"]!;

        Assert.Single(ports);
        var port = (Dictionary<string, object?>)ports[0]!;
        Assert.Equal(80, port["target"]);
        Assert.Equal("8080", port["published"]);
        Assert.Equal("tcp", port["protocol"]);
    }

    // â”€â”€ TransformDns: string â†’ list â”€â”€

    [Fact]
    public void Transform_Dns_StringToList()
    {
        var input = CreateServiceDict("dns", "8.8.8.8");
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var dns = (List<object?>)svc["dns"]!;
        Assert.Single(dns);
        Assert.Equal("8.8.8.8", dns[0]);
    }

    // â”€â”€ TransformMaybeExternal: volumes.* â”€â”€

    [Fact]
    public void Transform_MaybeExternal_NamedExternal()
    {
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>(),
            ["volumes"] = new Dictionary<string, object?>
            {
                ["mydata"] = new Dictionary<string, object?>
                {
                    ["external"] = new Dictionary<string, object?>
                    {
                        ["name"] = "actual-name"
                    }
                }
            }
        };

        var result = CanonicalTransformer.Transform(input);
        var volumes = (Dictionary<string, object?>)result["volumes"]!;
        var mydata = (Dictionary<string, object?>)volumes["mydata"]!;
        Assert.Equal(true, mydata["external"]);
        Assert.Equal("actual-name", mydata["name"]);
    }

    // â”€â”€ TransformInclude: string â†’ {"path": string} â”€â”€

    [Fact]
    public void Transform_Include_StringToDict()
    {
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>(),
            ["include"] = new List<object?> { "other-compose.yaml" }
        };

        var result = CanonicalTransformer.Transform(input);
        var include = (List<object?>)result["include"]!;
        var entry = (Dictionary<string, object?>)include[0]!;
        Assert.Equal("other-compose.yaml", entry["path"]);
    }

    // â”€â”€ TransformDeviceMapping: string â†’ dict â”€â”€

    [Fact]
    public void Transform_DeviceMapping_StringToDict()
    {
        var input = CreateServiceDict("devices", new List<object?> { "/dev/sda:/dev/xvdc:rwm" });
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var devices = (List<object?>)svc["devices"]!;
        var dev = (Dictionary<string, object?>)devices[0]!;
        Assert.Equal("/dev/sda", dev["source"]);
        Assert.Equal("/dev/xvdc", dev["target"]);
        Assert.Equal("rwm", dev["permissions"]);
    }

    // â”€â”€ Already canonical input passes through â”€â”€

    [Fact]
    public void Transform_AlreadyCanonical_PassesThrough()
    {
        var buildDict = new Dictionary<string, object?> { ["context"] = "./app" };
        var input = CreateServiceDict("build", buildDict);
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        Assert.Equal("./app", build["context"]);
    }

    // â”€â”€ TransformModels: list â†’ dict â”€â”€

    [Fact]
    public void Transform_Models_ListToDict()
    {
        var input = CreateServiceDict("models", new List<object?> { "gpt4", "llama" });
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var models = (Dictionary<string, object?>)svc["models"]!;

        Assert.True(models.ContainsKey("gpt4"));
        Assert.True(models.ContainsKey("llama"));
        Assert.Null(models["gpt4"]);
        Assert.Null(models["llama"]);
    }

    [Fact]
    public void Transform_Models_DictPassesThrough()
    {
        var modelsDict = new Dictionary<string, object?>
        {
            ["gpt4"] = new Dictionary<string, object?> { ["context"] = "default" }
        };
        var input = CreateServiceDict("models", modelsDict);
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var models = (Dictionary<string, object?>)svc["models"]!;
        Assert.True(models.ContainsKey("gpt4"));
    }

    // â”€â”€ TransformStringOrX: non-string â†’ string â”€â”€

    [Fact]
    public void Transform_BuildProvenance_BoolToString()
    {
        var buildDict = new Dictionary<string, object?>
        {
            ["context"] = ".",
            ["provenance"] = true
        };
        var input = CreateServiceDict("build", buildDict);
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        Assert.Equal("True", build["provenance"]);
    }

    [Fact]
    public void Transform_BuildSbom_BoolToString()
    {
        var buildDict = new Dictionary<string, object?>
        {
            ["context"] = ".",
            ["sbom"] = false
        };
        var input = CreateServiceDict("build", buildDict);
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        Assert.Equal("False", build["sbom"]);
    }

    [Fact]
    public void Transform_BuildProvenance_StringPassesThrough()
    {
        var buildDict = new Dictionary<string, object?>
        {
            ["context"] = ".",
            ["provenance"] = "max"
        };
        var input = CreateServiceDict("build", buildDict);
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        Assert.Equal("max", build["provenance"]);
    }

    // â”€â”€ TransformBuildUlimits: same as service ulimits â”€â”€

    [Fact]
    public void Transform_BuildUlimits_IntPassesThrough()
    {
        var buildDict = new Dictionary<string, object?>
        {
            ["context"] = ".",
            ["ulimits"] = new Dictionary<string, object?>
            {
                ["nofile"] = 1024
            }
        };
        var input = CreateServiceDict("build", buildDict);
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        var ulimits = (Dictionary<string, object?>)build["ulimits"]!;
        Assert.Equal(1024, ulimits["nofile"]);
    }

    // â”€â”€ TransformStringOrList: watch ignore/include â”€â”€

    [Fact]
    public void Transform_DevelopWatchIgnore_StringToList()
    {
        var svcDict = new Dictionary<string, object?>
        {
            ["develop"] = new Dictionary<string, object?>
            {
                ["watch"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["action"] = "sync",
                        ["path"] = "./src",
                        ["ignore"] = "node_modules"
                    }
                }
            }
        };
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?> { ["web"] = svcDict }
        };

        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var develop = (Dictionary<string, object?>)svc["develop"]!;
        var watch = (List<object?>)develop["watch"]!;
        var entry = (Dictionary<string, object?>)watch[0]!;
        var ignore = (List<object?>)entry["ignore"]!;

        Assert.Single(ignore);
        Assert.Equal("node_modules", ignore[0]);
    }

    [Fact]
    public void Transform_DevelopWatchInclude_StringToList()
    {
        var svcDict = new Dictionary<string, object?>
        {
            ["develop"] = new Dictionary<string, object?>
            {
                ["watch"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["action"] = "rebuild",
                        ["path"] = "./src",
                        ["include"] = "*.cs"
                    }
                }
            }
        };
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?> { ["web"] = svcDict }
        };

        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var develop = (Dictionary<string, object?>)svc["develop"]!;
        var watch = (List<object?>)develop["watch"]!;
        var entry = (Dictionary<string, object?>)watch[0]!;
        var include = (List<object?>)entry["include"]!;

        Assert.Single(include);
        Assert.Equal("*.cs", include[0]);
    }

    [Fact]
    public void Transform_DevelopWatchIgnore_ListPassesThrough()
    {
        var svcDict = new Dictionary<string, object?>
        {
            ["develop"] = new Dictionary<string, object?>
            {
                ["watch"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["action"] = "sync",
                        ["path"] = "./src",
                        ["ignore"] = new List<object?> { "node_modules", ".git" }
                    }
                }
            }
        };
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?> { ["web"] = svcDict }
        };

        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var develop = (Dictionary<string, object?>)svc["develop"]!;
        var watch = (List<object?>)develop["watch"]!;
        var entry = (Dictionary<string, object?>)watch[0]!;
        var ignore = (List<object?>)entry["ignore"]!;

        Assert.Equal(2, ignore.Count);
    }

    // â”€â”€ TransformLabelFile: string â†’ list â”€â”€

    [Fact]
    public void Transform_LabelFile_StringToList()
    {
        var input = CreateServiceDict("label_file", "labels.txt");
        var result = CanonicalTransformer.Transform(input);
        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var labelFile = (List<object?>)svc["label_file"]!;
        Assert.Single(labelFile);
        Assert.Equal("labels.txt", labelFile[0]);
    }

    // â”€â”€ SetDefaultValues tests â”€â”€

    [Fact]
    public void SetDefaultValues_BuildContext_DefaultsToDot()
    {
        var input = CreateServiceDict("build", new Dictionary<string, object?>
        {
            ["dockerfile"] = "Dockerfile.dev"
        });
        // First canonical transform
        var canonical = CanonicalTransformer.Transform(input);
        // Then set defaults
        var result = CanonicalTransformer.SetDefaultValues(canonical);

        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        Assert.Equal(".", build["context"]);
        Assert.Equal("Dockerfile.dev", build["dockerfile"]);
    }

    [Fact]
    public void SetDefaultValues_BuildContext_PreservesExisting()
    {
        var input = CreateServiceDict("build", new Dictionary<string, object?>
        {
            ["context"] = "./app",
            ["dockerfile"] = "Dockerfile"
        });
        var canonical = CanonicalTransformer.Transform(input);
        var result = CanonicalTransformer.SetDefaultValues(canonical);

        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        Assert.Equal("./app", build["context"]);
    }

    [Fact]
    public void SetDefaultValues_SecretMount_DefaultTarget()
    {
        var svcDict = new Dictionary<string, object?>
        {
            ["secrets"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["source"] = "db_password"
                }
            }
        };
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?> { ["web"] = svcDict }
        };
        var result = CanonicalTransformer.SetDefaultValues(input);

        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var secrets = (List<object?>)svc["secrets"]!;
        var secret = (Dictionary<string, object?>)secrets[0]!;
        Assert.Equal("/run/secrets/db_password", secret["target"]);
    }

    [Fact]
    public void SetDefaultValues_Port_DefaultProtocolAndMode()
    {
        var svcDict = new Dictionary<string, object?>
        {
            ["ports"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["target"] = 80,
                    ["published"] = "8080"
                }
            }
        };
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?> { ["web"] = svcDict }
        };
        var result = CanonicalTransformer.SetDefaultValues(input);

        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var ports = (List<object?>)svc["ports"]!;
        var port = (Dictionary<string, object?>)ports[0]!;
        Assert.Equal("tcp", port["protocol"]);
        Assert.Equal("ingress", port["mode"]);
    }

    [Fact]
    public void SetDefaultValues_DeviceRequest_DefaultCount()
    {
        var svcDict = new Dictionary<string, object?>
        {
            ["deploy"] = new Dictionary<string, object?>
            {
                ["resources"] = new Dictionary<string, object?>
                {
                    ["reservations"] = new Dictionary<string, object?>
                    {
                        ["devices"] = new List<object?>
                        {
                            new Dictionary<string, object?>
                            {
                                ["capabilities"] = new List<object?> { "gpu" }
                            }
                        }
                    }
                }
            }
        };
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?> { ["web"] = svcDict }
        };
        var result = CanonicalTransformer.SetDefaultValues(input);

        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var deploy = (Dictionary<string, object?>)svc["deploy"]!;
        var resources = (Dictionary<string, object?>)deploy["resources"]!;
        var reservations = (Dictionary<string, object?>)resources["reservations"]!;
        var devices = (List<object?>)reservations["devices"]!;
        var device = (Dictionary<string, object?>)devices[0]!;
        Assert.Equal("all", device["count"]);
    }

    [Fact]
    public void SetDefaultValues_VolumeBind_DefaultCreateHostPath()
    {
        var svcDict = new Dictionary<string, object?>
        {
            ["volumes"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "bind",
                    ["source"] = "./data",
                    ["target"] = "/app/data",
                    ["bind"] = new Dictionary<string, object?>
                    {
                        ["propagation"] = "rprivate"
                    }
                }
            }
        };
        var input = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?> { ["web"] = svcDict }
        };
        var result = CanonicalTransformer.SetDefaultValues(input);

        var services = (Dictionary<string, object?>)result["services"]!;
        var svc = (Dictionary<string, object?>)services["web"]!;
        var volumes = (List<object?>)svc["volumes"]!;
        var vol = (Dictionary<string, object?>)volumes[0]!;
        var bind = (Dictionary<string, object?>)vol["bind"]!;
        Assert.Equal(true, bind["create_host_path"]);
        Assert.Equal("rprivate", bind["propagation"]);
    }

    // â”€â”€ Helper â”€â”€

    private static Dictionary<string, object?> CreateServiceDict(string key, object? value)
    {
        return new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>
            {
                ["web"] = new Dictionary<string, object?>
                {
                    [key] = value
                }
            }
        };
    }
}
