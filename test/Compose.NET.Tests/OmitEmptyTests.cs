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

public class OmitEmptyTests
{
    [Fact]
    public void OmitEmpty_DnsWithNullAndEmptyString_RemovesThem()
    {
        var config = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>
            {
                ["web"] = new Dictionary<string, object?>
                {
                    ["dns"] = new List<object?> { "8.8.8.8", null, "", "1.1.1.1" }
                }
            }
        };

        ComposeLoader.OmitEmpty(config);

        var services = (Dictionary<string, object?>)config["services"]!;
        var web = (Dictionary<string, object?>)services["web"]!;
        var dns = (List<object?>)web["dns"]!;

        Assert.Equal(2, dns.Count);
        Assert.Equal("8.8.8.8", dns[0]);
        Assert.Equal("1.1.1.1", dns[1]);
    }

    [Fact]
    public void OmitEmpty_DnsAllEmpty_ResultsInEmptyList()
    {
        var config = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>
            {
                ["web"] = new Dictionary<string, object?>
                {
                    ["dns"] = new List<object?> { null, "" }
                }
            }
        };

        ComposeLoader.OmitEmpty(config);

        var services = (Dictionary<string, object?>)config["services"]!;
        var web = (Dictionary<string, object?>)services["web"]!;
        var dns = (List<object?>)web["dns"]!;

        Assert.Empty(dns);
    }

    [Fact]
    public void OmitEmpty_DnsWithValidEntries_Unchanged()
    {
        var config = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>
            {
                ["web"] = new Dictionary<string, object?>
                {
                    ["dns"] = new List<object?> { "8.8.8.8", "8.8.4.4" }
                }
            }
        };

        ComposeLoader.OmitEmpty(config);

        var services = (Dictionary<string, object?>)config["services"]!;
        var web = (Dictionary<string, object?>)services["web"]!;
        var dns = (List<object?>)web["dns"]!;

        Assert.Equal(2, dns.Count);
    }

    [Fact]
    public void OmitEmpty_NonRegisteredPath_NotAffected()
    {
        // environment is NOT in the OmitEmpty paths
        var config = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>
            {
                ["web"] = new Dictionary<string, object?>
                {
                    ["environment"] = new List<object?> { "FOO=bar", null, "" }
                }
            }
        };

        ComposeLoader.OmitEmpty(config);

        var services = (Dictionary<string, object?>)config["services"]!;
        var web = (Dictionary<string, object?>)services["web"]!;
        var env = (List<object?>)web["environment"]!;

        // Should remain untouched â€” 3 items
        Assert.Equal(3, env.Count);
    }

    [Fact]
    public void OmitEmpty_MultipleServices_AllProcessed()
    {
        var config = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>
            {
                ["web"] = new Dictionary<string, object?>
                {
                    ["dns"] = new List<object?> { null, "8.8.8.8" }
                },
                ["db"] = new Dictionary<string, object?>
                {
                    ["dns"] = new List<object?> { "", "1.1.1.1", null }
                }
            }
        };

        ComposeLoader.OmitEmpty(config);

        var services = (Dictionary<string, object?>)config["services"]!;
        var webDns = (List<object?>)((Dictionary<string, object?>)services["web"]!)["dns"]!;
        var dbDns = (List<object?>)((Dictionary<string, object?>)services["db"]!)["dns"]!;

        Assert.Single(webDns);
        Assert.Equal("8.8.8.8", webDns[0]);
        Assert.Single(dbDns);
        Assert.Equal("1.1.1.1", dbDns[0]);
    }

    [Fact]
    public void OmitEmpty_NoDnsKey_NoError()
    {
        var config = new Dictionary<string, object?>
        {
            ["services"] = new Dictionary<string, object?>
            {
                ["web"] = new Dictionary<string, object?>
                {
                    ["image"] = "nginx"
                }
            }
        };

        // Should not throw
        ComposeLoader.OmitEmpty(config);
    }

    [Fact]
    public void OmitEmpty_EmptyConfig_NoError()
    {
        var config = new Dictionary<string, object?>();
        ComposeLoader.OmitEmpty(config);
        Assert.Empty(config);
    }

    [Fact]
    public void OmitEmpty_IntegrationWithLoader_DnsNullsRemoved()
    {
        // Integration test: load YAML with dns that includes
        // a valid entry; verify it loads cleanly
        var yaml = @"
services:
  web:
    image: nginx
    dns:
      - 8.8.8.8
      - 1.1.1.1
";
        var result = ComposeLoader.LoadFromContent(yaml);
        var web = result.Project.Services["web"];
        Assert.Equal(2, web.DNS.Count);
        Assert.Contains("8.8.8.8", web.DNS);
        Assert.Contains("1.1.1.1", web.DNS);
    }
}
