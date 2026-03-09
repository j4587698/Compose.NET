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

public class PortParserTests
{
    // â”€â”€ Simple target port â”€â”€

    [Fact]
    public void Parse_SimplePort()
    {
        var ports = PortParser.ParsePortConfig("80");
        Assert.Single(ports);
        Assert.Equal(80u, ports[0].Target);
        Assert.Null(ports[0].Published);
        Assert.Equal("tcp", ports[0].Protocol);
    }

    // â”€â”€ Host:target mapping â”€â”€

    [Fact]
    public void Parse_HostTargetMapping()
    {
        var ports = PortParser.ParsePortConfig("8080:80");
        Assert.Single(ports);
        Assert.Equal(80u, ports[0].Target);
        Assert.Equal("8080", ports[0].Published);
    }

    // â”€â”€ Protocol â”€â”€

    [Fact]
    public void Parse_UdpProtocol()
    {
        var ports = PortParser.ParsePortConfig("80/udp");
        Assert.Single(ports);
        Assert.Equal(80u, ports[0].Target);
        Assert.Equal("udp", ports[0].Protocol);
    }

    [Fact]
    public void Parse_SctpProtocol()
    {
        var ports = PortParser.ParsePortConfig("80/sctp");
        Assert.Single(ports);
        Assert.Equal("sctp", ports[0].Protocol);
    }

    // â”€â”€ IP binding â”€â”€

    [Fact]
    public void Parse_IPBinding()
    {
        var ports = PortParser.ParsePortConfig("127.0.0.1:8080:80");
        Assert.Single(ports);
        Assert.Equal("127.0.0.1", ports[0].HostIP);
        Assert.Equal(80u, ports[0].Target);
        Assert.Equal("8080", ports[0].Published);
    }

    // â”€â”€ IPv6 â”€â”€

    [Fact]
    public void Parse_IPv6Binding()
    {
        var ports = PortParser.ParsePortConfig("[::1]:8080:80");
        Assert.Single(ports);
        Assert.Equal("::1", ports[0].HostIP);
        Assert.Equal(80u, ports[0].Target);
        Assert.Equal("8080", ports[0].Published);
    }

    // â”€â”€ Port ranges â”€â”€

    [Fact]
    public void Parse_PortRange()
    {
        var ports = PortParser.ParsePortConfig("8080-8082:80-82");
        Assert.Equal(3, ports.Count);
        Assert.Equal(80u, ports[0].Target);
        Assert.Equal("8080", ports[0].Published);
        Assert.Equal(81u, ports[1].Target);
        Assert.Equal("8081", ports[1].Published);
        Assert.Equal(82u, ports[2].Target);
        Assert.Equal("8082", ports[2].Published);
    }

    [Fact]
    public void Parse_TargetRangeOnly()
    {
        var ports = PortParser.ParsePortConfig("80-82");
        Assert.Equal(3, ports.Count);
        Assert.Equal(80u, ports[0].Target);
        Assert.Equal(81u, ports[1].Target);
        Assert.Equal(82u, ports[2].Target);
    }

    // â”€â”€ Range mismatch â”€â”€

    [Fact]
    public void Parse_RangeMismatch_Throws()
    {
        Assert.Throws<FormatException>(() => PortParser.ParsePortConfig("8080-8082:80-81"));
    }

    // â”€â”€ Full syntax with IP, range, protocol â”€â”€

    [Fact]
    public void Parse_FullSyntax()
    {
        var ports = PortParser.ParsePortConfig("127.0.0.1:8080-8081:80-81/udp");
        Assert.Equal(2, ports.Count);
        Assert.Equal("127.0.0.1", ports[0].HostIP);
        Assert.Equal(80u, ports[0].Target);
        Assert.Equal("8080", ports[0].Published);
        Assert.Equal("udp", ports[0].Protocol);
        Assert.Equal(81u, ports[1].Target);
        Assert.Equal("8081", ports[1].Published);
    }

    // â”€â”€ Empty / invalid â”€â”€

    [Fact]
    public void Parse_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => PortParser.ParsePortConfig(""));
    }

    // â”€â”€ Host port as range, target as single â€” range mismatch throws â”€â”€

    [Fact]
    public void Parse_HostRangeTargetSingle_Throws()
    {
        // PortParser correctly rejects host range with single target (range mismatch)
        Assert.Throws<FormatException>(() => PortParser.ParsePortConfig("8080-8082:80"));
    }

    // â”€â”€ Published port as string (range) â”€â”€

    [Fact]
    public void Parse_PublishedOnlyNoTarget()
    {
        // "8080" means target=8080
        var ports = PortParser.ParsePortConfig("8080");
        Assert.Single(ports);
        Assert.Equal(8080u, ports[0].Target);
    }

    // â”€â”€ Default values â”€â”€

    [Fact]
    public void Parse_DefaultProtocol_IsTcp()
    {
        var ports = PortParser.ParsePortConfig("80");
        Assert.Equal("tcp", ports[0].Protocol);
    }

    [Fact]
    public void Parse_DefaultMode_IsIngress()
    {
        var ports = PortParser.ParsePortConfig("80");
        Assert.Equal("ingress", ports[0].Mode);
    }
}
