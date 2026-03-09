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

/// <summary>
/// Tests for the path-based special merge rules in MergeProject.
/// Mirrors compose-go/override/merge.go mergeSpecials.
/// </summary>
public class MergeSpecialsTests
{
    private static Dictionary<string, object?> Svc(string name, Dictionary<string, object?> props)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["services"] = new Dictionary<string, object?>(StringComparer.Ordinal) { [name] = props }
        };
    }

    private static Dictionary<string, object?> GetService(Dictionary<string, object?> config, string name)
    {
        return (Dictionary<string, object?>)((Dictionary<string, object?>)config["services"]!)[name]!;
    }

    // â”€â”€â”€â”€â”€â”€ override (command, entrypoint, healthcheck.test) â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Command_Override_FullyReplaced()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?> { ["command"] = new List<object?> { "npm", "start" }, ["image"] = "node" });
        var overrideConfig = Svc("web", new Dictionary<string, object?> { ["command"] = new List<object?> { "npm", "run", "dev" } });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var svc = GetService(result, "web");
        var cmd = (List<object?>)svc["command"]!;
        Assert.Equal(3, cmd.Count);
        Assert.Equal("dev", cmd[2]);
    }

    [Fact]
    public void Entrypoint_Override_FullyReplaced()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?> { ["entrypoint"] = "/bin/sh" });
        var overrideConfig = Svc("web", new Dictionary<string, object?> { ["entrypoint"] = "/bin/bash" });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        Assert.Equal("/bin/bash", GetService(result, "web")["entrypoint"]);
    }

    // â”€â”€â”€â”€â”€â”€ mergeToSequence (environment, labels, dns, etc.) â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Environment_MergeToSequence_AppendsBothSides()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["environment"] = new List<object?> { "FOO=bar" }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["environment"] = new List<object?> { "BAZ=qux" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var env = (List<object?>)GetService(result, "web")["environment"]!;
        Assert.Equal(2, env.Count);
        Assert.Contains("FOO=bar", env.Cast<string>());
        Assert.Contains("BAZ=qux", env.Cast<string>());
    }

    [Fact]
    public void Environment_MergeToSequence_MapConvertedToSequence()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["environment"] = new Dictionary<string, object?> { ["FOO"] = "bar" }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["environment"] = new List<object?> { "BAZ=qux" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var env = (List<object?>)GetService(result, "web")["environment"]!;
        Assert.Equal(2, env.Count);
    }

    [Fact]
    public void Labels_MergeToSequence_Appended()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["labels"] = new List<object?> { "com.example.a=1" }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["labels"] = new List<object?> { "com.example.b=2" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var labels = (List<object?>)GetService(result, "web")["labels"]!;
        Assert.Equal(2, labels.Count);
    }

    [Fact]
    public void Dns_MergeToSequence_Appended()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["dns"] = new List<object?> { "8.8.8.8" }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["dns"] = new List<object?> { "1.1.1.1" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var dns = (List<object?>)GetService(result, "web")["dns"]!;
        Assert.Equal(2, dns.Count);
    }

    // â”€â”€â”€â”€â”€â”€ mergeBuild (string â†’ {context: string}) â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Build_StringToMap_Normalized()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?> { ["build"] = "." });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["build"] = new Dictionary<string, object?> { ["dockerfile"] = "Dockerfile.dev" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var build = (Dictionary<string, object?>)GetService(result, "web")["build"]!;
        Assert.Equal(".", build["context"]);
        Assert.Equal("Dockerfile.dev", build["dockerfile"]);
    }

    [Fact]
    public void Build_BothStrings_MergedAsContexts()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?> { ["build"] = "./app" });
        var overrideConfig = Svc("web", new Dictionary<string, object?> { ["build"] = "./new-app" });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var build = (Dictionary<string, object?>)GetService(result, "web")["build"]!;
        Assert.Equal("./new-app", build["context"]);
    }

    // â”€â”€â”€â”€â”€â”€ mergeDependsOn (list â†’ {svc: {condition, required}}) â”€â”€â”€â”€â”€â”€

    [Fact]
    public void DependsOn_ListToMap_Normalized()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["depends_on"] = new List<object?> { "db" }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["depends_on"] = new List<object?> { "redis" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var deps = (Dictionary<string, object?>)GetService(result, "web")["depends_on"]!;
        Assert.True(deps.ContainsKey("db"));
        Assert.True(deps.ContainsKey("redis"));
    }

    [Fact]
    public void DependsOn_MapMerge_OverrideTakesPrecedence()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["depends_on"] = new Dictionary<string, object?>
            {
                ["db"] = new Dictionary<string, object?> { ["condition"] = "service_started", ["required"] = true }
            }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["depends_on"] = new Dictionary<string, object?>
            {
                ["db"] = new Dictionary<string, object?> { ["condition"] = "service_healthy" }
            }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var deps = (Dictionary<string, object?>)GetService(result, "web")["depends_on"]!;
        var db = (Dictionary<string, object?>)deps["db"]!;
        Assert.Equal("service_healthy", db["condition"]);
    }

    // â”€â”€â”€â”€â”€â”€ mergeNetworks (list â†’ map) â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Networks_ListToMap_Normalized()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["networks"] = new List<object?> { "frontend" }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["networks"] = new List<object?> { "backend" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var nets = (Dictionary<string, object?>)GetService(result, "web")["networks"]!;
        Assert.True(nets.ContainsKey("frontend"));
        Assert.True(nets.ContainsKey("backend"));
    }

    // â”€â”€â”€â”€â”€â”€ mergeModels (list â†’ map) â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Models_ListToMap_Normalized()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["models"] = new List<object?> { "gpt-4" }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["models"] = new List<object?> { "claude" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var models = (Dictionary<string, object?>)GetService(result, "web")["models"]!;
        Assert.True(models.ContainsKey("gpt-4"));
        Assert.True(models.ContainsKey("claude"));
    }

    // â”€â”€â”€â”€â”€â”€ mergeExtraHosts (dedup by hostname) â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ExtraHosts_DeduplicateByHostname()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["extra_hosts"] = new List<object?> { "host1=1.2.3.4", "host2=5.6.7.8" }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["extra_hosts"] = new List<object?> { "host1=10.20.30.40" }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var hosts = (List<object?>)GetService(result, "web")["extra_hosts"]!;
        // host2 (from base, not overridden) + host1 (from override)
        Assert.Equal(2, hosts.Count);
        // host2 should still be there
        Assert.Contains("host2=5.6.7.8", hosts.Cast<string>());
        // host1 should have the new IP
        Assert.Contains("host1=10.20.30.40", hosts.Cast<string>());
        // old host1 should NOT be there
        Assert.DoesNotContain("host1=1.2.3.4", hosts.Cast<string>());
    }

    // â”€â”€â”€â”€â”€â”€ mergeLogging â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Logging_SameDriver_DeepMerge()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["logging"] = new Dictionary<string, object?>
            {
                ["driver"] = "json-file",
                ["options"] = new Dictionary<string, object?> { ["max-size"] = "10m" }
            }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["logging"] = new Dictionary<string, object?>
            {
                ["driver"] = "json-file",
                ["options"] = new Dictionary<string, object?> { ["max-file"] = "5" }
            }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var logging = (Dictionary<string, object?>)GetService(result, "web")["logging"]!;
        var options = (Dictionary<string, object?>)logging["options"]!;
        Assert.Equal("10m", options["max-size"]);
        Assert.Equal("5", options["max-file"]);
    }

    [Fact]
    public void Logging_DifferentDriver_FullReplace()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["logging"] = new Dictionary<string, object?>
            {
                ["driver"] = "json-file",
                ["options"] = new Dictionary<string, object?> { ["max-size"] = "10m" }
            }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["logging"] = new Dictionary<string, object?>
            {
                ["driver"] = "syslog",
                ["options"] = new Dictionary<string, object?> { ["syslog-address"] = "tcp://192.168.0.42:123" }
            }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var logging = (Dictionary<string, object?>)GetService(result, "web")["logging"]!;
        Assert.Equal("syslog", logging["driver"]);
        var options = (Dictionary<string, object?>)logging["options"]!;
        Assert.False(options.ContainsKey("max-size")); // base options removed
        Assert.Equal("tcp://192.168.0.42:123", options["syslog-address"]);
    }

    [Fact]
    public void Logging_NoDriverInOverride_Merged()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["logging"] = new Dictionary<string, object?>
            {
                ["driver"] = "json-file",
                ["options"] = new Dictionary<string, object?> { ["max-size"] = "10m" }
            }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["logging"] = new Dictionary<string, object?>
            {
                ["options"] = new Dictionary<string, object?> { ["max-file"] = "3" }
            }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var logging = (Dictionary<string, object?>)GetService(result, "web")["logging"]!;
        Assert.Equal("json-file", logging["driver"]);
        var options = (Dictionary<string, object?>)logging["options"]!;
        Assert.Equal("10m", options["max-size"]);
        Assert.Equal("3", options["max-file"]);
    }

    // â”€â”€â”€â”€â”€â”€ mergeUlimit â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Ulimits_ScalarOverridesScalar()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["ulimits"] = new Dictionary<string, object?> { ["nofile"] = 1024 }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["ulimits"] = new Dictionary<string, object?> { ["nofile"] = 2048 }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var ulimits = (Dictionary<string, object?>)GetService(result, "web")["ulimits"]!;
        Assert.Equal(2048, ulimits["nofile"]);
    }

    [Fact]
    public void Ulimits_MapOverridesMap_Merged()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["ulimits"] = new Dictionary<string, object?>
            {
                ["nofile"] = new Dictionary<string, object?> { ["soft"] = 1024, ["hard"] = 2048 }
            }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["ulimits"] = new Dictionary<string, object?>
            {
                ["nofile"] = new Dictionary<string, object?> { ["hard"] = 4096 }
            }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var ulimits = (Dictionary<string, object?>)GetService(result, "web")["ulimits"]!;
        var nofile = (Dictionary<string, object?>)ulimits["nofile"]!;
        Assert.Equal(1024, nofile["soft"]); // kept from base
        Assert.Equal(4096, nofile["hard"]); // overridden
    }

    [Fact]
    public void Ulimits_MapOverridesScalar()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["ulimits"] = new Dictionary<string, object?> { ["nofile"] = 1024 }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["ulimits"] = new Dictionary<string, object?>
            {
                ["nofile"] = new Dictionary<string, object?> { ["soft"] = 2048, ["hard"] = 4096 }
            }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var ulimits = (Dictionary<string, object?>)GetService(result, "web")["ulimits"]!;
        var nofile = (Dictionary<string, object?>)ulimits["nofile"]!;
        Assert.Equal(2048, nofile["soft"]);
        Assert.Equal(4096, nofile["hard"]);
    }

    // â”€â”€â”€â”€â”€â”€ mergeIPAMConfig â”€â”€â”€â”€â”€â”€

    [Fact]
    public void IPAMConfig_SameSubnet_Merged()
    {
        var baseConfig = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["networks"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mynet"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ipam"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["config"] = new List<object?>
                        {
                            new Dictionary<string, object?> { ["subnet"] = "172.28.0.0/16", ["gateway"] = "172.28.0.1" }
                        }
                    }
                }
            }
        };
        var overrideConfig = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["networks"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mynet"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ipam"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["config"] = new List<object?>
                        {
                            new Dictionary<string, object?> { ["subnet"] = "172.28.0.0/16", ["ip_range"] = "172.28.5.0/24" }
                        }
                    }
                }
            }
        };

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var ipam = (Dictionary<string, object?>)((Dictionary<string, object?>)((Dictionary<string, object?>)result["networks"]!)["mynet"]!)["ipam"]!;
        var configs = (List<object?>)ipam["config"]!;
        Assert.Single(configs); // same subnet â†’ merged, not duplicated
        var cfg = (Dictionary<string, object?>)configs[0]!;
        Assert.Equal("172.28.0.0/16", cfg["subnet"]);
        Assert.Equal("172.28.0.1", cfg["gateway"]); // from base
        Assert.Equal("172.28.5.0/24", cfg["ip_range"]); // from override
    }

    [Fact]
    public void IPAMConfig_DifferentSubnet_Appended()
    {
        var baseConfig = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["networks"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mynet"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ipam"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["config"] = new List<object?>
                        {
                            new Dictionary<string, object?> { ["subnet"] = "172.28.0.0/16" }
                        }
                    }
                }
            }
        };
        var overrideConfig = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["networks"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mynet"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ipam"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["config"] = new List<object?>
                        {
                            new Dictionary<string, object?> { ["subnet"] = "10.0.0.0/8" }
                        }
                    }
                }
            }
        };

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var ipam = (Dictionary<string, object?>)((Dictionary<string, object?>)((Dictionary<string, object?>)result["networks"]!)["mynet"]!)["ipam"]!;
        var configs = (List<object?>)ipam["config"]!;
        Assert.Equal(2, configs.Count); // different subnets â†’ both present
    }

    // â”€â”€â”€â”€â”€â”€ Build sub-keys mergeToSequence â”€â”€â”€â”€â”€â”€

    [Fact]
    public void BuildArgs_MergeToSequence_Appended()
    {
        var baseConfig = Svc("web", new Dictionary<string, object?>
        {
            ["build"] = new Dictionary<string, object?>
            {
                ["context"] = ".",
                ["args"] = new List<object?> { "NODE_ENV=prod" }
            }
        });
        var overrideConfig = Svc("web", new Dictionary<string, object?>
        {
            ["build"] = new Dictionary<string, object?>
            {
                ["args"] = new List<object?> { "VERSION=2.0" }
            }
        });

        var result = MergeProject.MergeYaml(baseConfig, overrideConfig);
        var build = (Dictionary<string, object?>)GetService(result, "web")["build"]!;
        var args = (List<object?>)build["args"]!;
        Assert.Equal(2, args.Count);
    }

    // â”€â”€â”€â”€â”€â”€ Extends uses same path-based merge â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ExtendService_UsesPathBasedMerge()
    {
        var source = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["environment"] = new List<object?> { "FOO=bar" },
            ["logging"] = new Dictionary<string, object?> { ["driver"] = "json-file" },
            ["depends_on"] = new Dictionary<string, object?> { ["db"] = new Dictionary<string, object?> { ["condition"] = "service_started" } }
        };
        var target = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["environment"] = new List<object?> { "BAZ=qux" }
        };

        var result = MergeProject.ExtendService(source, target);
        // Environment should be appended (mergeToSequence)
        var env = (List<object?>)result["environment"]!;
        Assert.Equal(2, env.Count);
        // Logging should be inherited
        Assert.True(result.ContainsKey("logging"));
        // depends_on should NOT be inherited
        Assert.False(result.ContainsKey("depends_on"));
    }
}
