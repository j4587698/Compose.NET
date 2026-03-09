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

using Compose.NET.Override;
using Xunit;

namespace Compose.NET.Tests;

public class EnforceUnicityTests
{
    private static Dictionary<string, object?> MakeConfig(Dictionary<string, object?> services)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["services"] = services
        };
    }

    private static Dictionary<string, object?> MakeService(string key, List<object?> items)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [key] = items
        };
    }

    // â”€â”€â”€â”€â”€â”€ Ports â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Ports_DictForm_DeduplicatesByCompositeKey()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("ports", new List<object?>
            {
                new Dictionary<string, object?> { ["target"] = "80", ["published"] = "8080", ["protocol"] = "tcp", ["host_ip"] = "0.0.0.0" },
                new Dictionary<string, object?> { ["target"] = "443", ["published"] = "8443", ["protocol"] = "tcp", ["host_ip"] = "0.0.0.0" },
                new Dictionary<string, object?> { ["target"] = "80", ["published"] = "8080", ["protocol"] = "tcp", ["host_ip"] = "0.0.0.0" }, // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var ports = (List<object?>)svc["ports"]!;
        Assert.Equal(2, ports.Count);
    }

    [Fact]
    public void Ports_StringForm_DeduplicatesByString()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("ports", new List<object?>
            {
                "8080:80",
                "8443:443",
                "8080:80", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var ports = (List<object?>)svc["ports"]!;
        Assert.Equal(2, ports.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Volumes â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Volumes_DictForm_DeduplicatesByTarget()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("volumes", new List<object?>
            {
                new Dictionary<string, object?> { ["source"] = "data", ["target"] = "/app/data" },
                new Dictionary<string, object?> { ["source"] = "logs", ["target"] = "/app/logs" },
                new Dictionary<string, object?> { ["source"] = "data2", ["target"] = "/app/data" }, // same target â†’ replaces first
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var vols = (List<object?>)svc["volumes"]!;
        Assert.Equal(2, vols.Count);
        // Later entry wins â€” "data2" should be in the result
        var first = (Dictionary<string, object?>)vols[0]!;
        Assert.Equal("data2", first["source"]?.ToString());
    }

    [Fact]
    public void Volumes_StringForm_DeduplicatesByTarget()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("volumes", new List<object?>
            {
                "./src:/app",
                "./logs:/var/log",
                "./other:/app", // same target /app
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var vols = (List<object?>)svc["volumes"]!;
        Assert.Equal(2, vols.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Devices â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Devices_StringForm_DeduplicatesByTarget()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("devices", new List<object?>
            {
                "/dev/sda:/dev/xvdc",
                "/dev/sdb:/dev/xvdd",
                "/dev/sdc:/dev/xvdc", // same target /dev/xvdc
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var devices = (List<object?>)svc["devices"]!;
        Assert.Equal(2, devices.Count);
        Assert.Equal("/dev/sdc:/dev/xvdc", devices[0]); // later wins
    }

    [Fact]
    public void Devices_DictForm_DeduplicatesByTarget()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("devices", new List<object?>
            {
                new Dictionary<string, object?> { ["source"] = "/dev/sda", ["target"] = "/dev/xvdc" },
                new Dictionary<string, object?> { ["source"] = "/dev/sdb", ["target"] = "/dev/xvdc" }, // same target
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var devices = (List<object?>)svc["devices"]!;
        Assert.Single(devices);
    }

    // â”€â”€â”€â”€â”€â”€ Environment / Labels / KeyValue â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Environment_DeduplicatesByKey()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("environment", new List<object?>
            {
                "FOO=bar",
                "BAZ=qux",
                "FOO=overridden", // dup key
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var env = (List<object?>)svc["environment"]!;
        Assert.Equal(2, env.Count);
        Assert.Equal("FOO=overridden", env[0]); // later wins, replaces at same index
        Assert.Equal("BAZ=qux", env[1]);
    }

    [Fact]
    public void Labels_DeduplicatesByKey()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("labels", new List<object?>
            {
                "com.example.version=1.0",
                "com.example.description=test",
                "com.example.version=2.0", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var labels = (List<object?>)svc["labels"]!;
        Assert.Equal(2, labels.Count);
    }

    // â”€â”€â”€â”€â”€â”€ DNS â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Dns_DeduplicatesByValue()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("dns", new List<object?>
            {
                "8.8.8.8",
                "8.8.4.4",
                "8.8.8.8", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var dns = (List<object?>)svc["dns"]!;
        Assert.Equal(2, dns.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Expose â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Expose_DeduplicatesByPortValue()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("expose", new List<object?>
            {
                "3000",
                "8000",
                "3000", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var expose = (List<object?>)svc["expose"]!;
        Assert.Equal(2, expose.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Configs â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Configs_StringForm_DeduplicatesByMountTarget()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("configs", new List<object?>
            {
                "my_config",
                "other_config",
                "my_config", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var configs = (List<object?>)svc["configs"]!;
        Assert.Equal(2, configs.Count);
    }

    [Fact]
    public void Configs_DictForm_DeduplicatesByTarget()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("configs", new List<object?>
            {
                new Dictionary<string, object?> { ["source"] = "my_config", ["target"] = "/app/config.json" },
                new Dictionary<string, object?> { ["source"] = "other_config", ["target"] = "/app/config.json" }, // same target
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var configs = (List<object?>)svc["configs"]!;
        Assert.Single(configs);
    }

    // â”€â”€â”€â”€â”€â”€ Secrets â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Secrets_StringForm_DeduplicatesByMountTarget()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("secrets", new List<object?>
            {
                "my_secret",
                "other_secret",
                "my_secret", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var secrets = (List<object?>)svc["secrets"]!;
        Assert.Equal(2, secrets.Count);
    }

    // â”€â”€â”€â”€â”€â”€ EnvFile â”€â”€â”€â”€â”€â”€

    [Fact]
    public void EnvFile_StringForm_DeduplicatesByPath()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("env_file", new List<object?>
            {
                ".env",
                ".env.local",
                ".env", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var envFiles = (List<object?>)svc["env_file"]!;
        Assert.Equal(2, envFiles.Count);
    }

    [Fact]
    public void EnvFile_DictForm_DeduplicatesByPath()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("env_file", new List<object?>
            {
                new Dictionary<string, object?> { ["path"] = ".env", ["required"] = true },
                new Dictionary<string, object?> { ["path"] = ".env.local", ["required"] = false },
                new Dictionary<string, object?> { ["path"] = ".env", ["required"] = false }, // dup path
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var envFiles = (List<object?>)svc["env_file"]!;
        Assert.Equal(2, envFiles.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Build sub-keys â”€â”€â”€â”€â”€â”€

    [Fact]
    public void BuildArgs_DeduplicatesByKey()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["build"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["args"] = new List<object?> { "NODE_ENV=prod", "VERSION=1.0", "NODE_ENV=dev" }
                }
            }
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        var args = (List<object?>)build["args"]!;
        Assert.Equal(2, args.Count);
        Assert.Equal("NODE_ENV=dev", args[0]);
    }

    [Fact]
    public void BuildLabels_DeduplicatesByKey()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["build"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["labels"] = new List<object?> { "key1=a", "key2=b", "key1=c" }
                }
            }
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var build = (Dictionary<string, object?>)svc["build"]!;
        var labels = (List<object?>)build["labels"]!;
        Assert.Equal(2, labels.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Deploy sub-keys â”€â”€â”€â”€â”€â”€

    [Fact]
    public void DeployLabels_DeduplicatesByKey()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["deploy"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["labels"] = new List<object?> { "key1=a", "key2=b", "key1=c" }
                }
            }
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var deploy = (Dictionary<string, object?>)svc["deploy"]!;
        var labels = (List<object?>)deploy["labels"]!;
        Assert.Equal(2, labels.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Cap add/drop â”€â”€â”€â”€â”€â”€

    [Fact]
    public void CapAdd_DeduplicatesByValue()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("cap_add", new List<object?>
            {
                "SYS_ADMIN",
                "NET_ADMIN",
                "SYS_ADMIN", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var caps = (List<object?>)svc["cap_add"]!;
        Assert.Equal(2, caps.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Profiles â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Profiles_DeduplicatesByValue()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("profiles", new List<object?>
            {
                "dev",
                "debug",
                "dev", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var profiles = (List<object?>)svc["profiles"]!;
        Assert.Equal(2, profiles.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Network labels (top-level) â”€â”€â”€â”€â”€â”€

    [Fact]
    public void NetworkLabels_DeduplicatesByKey()
    {
        var config = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["networks"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mynet"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["labels"] = new List<object?> { "com.example.a=1", "com.example.b=2", "com.example.a=3" }
                }
            }
        };

        MergeProject.EnforceUnicity(config);

        var net = (Dictionary<string, object?>)((Dictionary<string, object?>)config["networks"]!)["mynet"]!;
        var labels = (List<object?>)net["labels"]!;
        Assert.Equal(2, labels.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Nested: networks.*.aliases â”€â”€â”€â”€â”€â”€

    [Fact]
    public void NetworkAliases_DeduplicatesByValue()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["networks"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mynet"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["aliases"] = new List<object?> { "web", "api", "web" }
                    }
                }
            }
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var nets = (Dictionary<string, object?>)svc["networks"]!;
        var mynet = (Dictionary<string, object?>)nets["mynet"]!;
        var aliases = (List<object?>)mynet["aliases"]!;
        Assert.Equal(2, aliases.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Multiple services â”€â”€â”€â”€â”€â”€

    [Fact]
    public void MultipleServices_EachDeduplicatedIndependently()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("dns", new List<object?> { "8.8.8.8", "8.8.8.8" }),
            ["api"] = MakeService("dns", new List<object?> { "1.1.1.1", "8.8.4.4", "1.1.1.1" }),
        });

        MergeProject.EnforceUnicity(config);

        var services = (Dictionary<string, object?>)config["services"]!;
        var webDns = (List<object?>)((Dictionary<string, object?>)services["web"]!)["dns"]!;
        var apiDns = (List<object?>)((Dictionary<string, object?>)services["api"]!)["dns"]!;
        Assert.Single(webDns);
        Assert.Equal(2, apiDns.Count);
    }

    // â”€â”€â”€â”€â”€â”€ No crash on empty/missing â”€â”€â”€â”€â”€â”€

    [Fact]
    public void EmptyConfig_NoCrash()
    {
        var config = new Dictionary<string, object?>(StringComparer.Ordinal);
        MergeProject.EnforceUnicity(config);
        Assert.Empty(config);
    }

    [Fact]
    public void NoMatchingKeys_NoCrash()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["image"] = "nginx:latest",
                ["container_name"] = "web"
            }
        });

        MergeProject.EnforceUnicity(config);
        // Should not throw, no changes
    }

    // â”€â”€â”€â”€â”€â”€ Sysctls â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Sysctls_DeduplicatesByKey()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("sysctls", new List<object?>
            {
                "net.core.somaxconn=1024",
                "net.ipv4.tcp_syncookies=1",
                "net.core.somaxconn=2048", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var sysctls = (List<object?>)svc["sysctls"]!;
        Assert.Equal(2, sysctls.Count);
        Assert.Equal("net.core.somaxconn=2048", sysctls[0]); // later wins
    }

    // â”€â”€â”€â”€â”€â”€ Links â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Links_DeduplicatesByAlias()
    {
        var config = MakeConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = MakeService("links", new List<object?>
            {
                "db:database",
                "cache:redis",
                "db:database", // dup
            })
        });

        MergeProject.EnforceUnicity(config);

        var svc = (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)["web"]!;
        var links = (List<object?>)svc["links"]!;
        Assert.Equal(2, links.Count);
    }
}
