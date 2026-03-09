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

public class VolumeParserTests
{
    // â”€â”€ Anonymous volume â”€â”€

    [Fact]
    public void Parse_AnonymousVolume()
    {
        var vol = VolumeParser.ParseVolume("/target");
        Assert.Equal(VolumeTypes.Volume, vol.Type);
        Assert.Equal("/target", vol.Target);
        Assert.Null(vol.Source);
    }

    // â”€â”€ Named volume â”€â”€

    [Fact]
    public void Parse_NamedVolume()
    {
        var vol = VolumeParser.ParseVolume("mydata:/target");
        Assert.Equal(VolumeTypes.Volume, vol.Type);
        Assert.Equal("mydata", vol.Source);
        Assert.Equal("/target", vol.Target);
    }

    // â”€â”€ Bind mount (relative) â”€â”€

    [Fact]
    public void Parse_RelativeBindMount()
    {
        var vol = VolumeParser.ParseVolume("./src:/app/src");
        Assert.Equal(VolumeTypes.Bind, vol.Type);
        Assert.Equal("./src", vol.Source);
        Assert.Equal("/app/src", vol.Target);
    }

    // â”€â”€ Bind mount (absolute) â”€â”€

    [Fact]
    public void Parse_AbsoluteBindMount()
    {
        var vol = VolumeParser.ParseVolume("/host/path:/container/path");
        Assert.Equal(VolumeTypes.Bind, vol.Type);
        Assert.Equal("/host/path", vol.Source);
        Assert.Equal("/container/path", vol.Target);
    }

    // â”€â”€ Read-only flag â”€â”€

    [Fact]
    public void Parse_ReadOnly()
    {
        var vol = VolumeParser.ParseVolume("mydata:/target:ro");
        Assert.True(vol.ReadOnly);
    }

    [Fact]
    public void Parse_ReadWrite()
    {
        var vol = VolumeParser.ParseVolume("mydata:/target:rw");
        Assert.False(vol.ReadOnly);
    }

    // â”€â”€ Multiple options â”€â”€

    [Fact]
    public void Parse_MultipleOptions()
    {
        var vol = VolumeParser.ParseVolume("./src:/app:ro,z");
        Assert.True(vol.ReadOnly);
        Assert.NotNull(vol.Bind);
        Assert.Equal("z", vol.Bind!.SELinux);
    }

    // â”€â”€ Windows path â”€â”€

    [Fact]
    public void Parse_WindowsPath()
    {
        var vol = VolumeParser.ParseVolume("C:\\Users\\test:/app");
        Assert.Equal(VolumeTypes.Bind, vol.Type);
        Assert.Equal("C:\\Users\\test", vol.Source);
        Assert.Equal("/app", vol.Target);
    }

    // â”€â”€ Named pipe (Windows) â”€â”€

    [Fact]
    public void Parse_NamedPipe()
    {
        var vol = VolumeParser.ParseVolume("\\\\.\\pipe\\docker_engine:/var/run/docker.sock");
        Assert.Equal(VolumeTypes.NamedPipe, vol.Type);
        Assert.Equal("\\\\.\\pipe\\docker_engine", vol.Source);
    }

    // â”€â”€ Home directory â”€â”€

    [Fact]
    public void Parse_HomeDirectory()
    {
        var vol = VolumeParser.ParseVolume("~/data:/target");
        Assert.Equal(VolumeTypes.Bind, vol.Type);
        Assert.Equal("~/data", vol.Source);
    }

    // â”€â”€ Propagation options â”€â”€

    [Fact]
    public void Parse_Propagation()
    {
        var vol = VolumeParser.ParseVolume("./src:/app:rprivate");
        Assert.NotNull(vol.Bind);
        Assert.Equal("rprivate", vol.Bind!.Propagation);
    }

    // â”€â”€ NoCopy option â”€â”€

    [Fact]
    public void Parse_NoCopy()
    {
        var vol = VolumeParser.ParseVolume("mydata:/target:nocopy");
        Assert.NotNull(vol.Volume);
        Assert.True(vol.Volume!.NoCopy);
    }

    // â”€â”€ Empty string â”€â”€

    [Fact]
    public void Parse_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => VolumeParser.ParseVolume(""));
    }

    // â”€â”€ SELinux options â”€â”€

    [Theory]
    [InlineData("z")]
    [InlineData("Z")]
    public void Parse_SELinuxOptions(string option)
    {
        var vol = VolumeParser.ParseVolume($"./src:/app:{option}");
        Assert.NotNull(vol.Bind);
        Assert.Equal(option, vol.Bind!.SELinux);
    }
}
