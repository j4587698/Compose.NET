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

public class DurationTests
{
    // â”€â”€ Parsing â”€â”€

    [Fact]
    public void Parse_Seconds()
    {
        var d = Duration.Parse("30s");
        Assert.Equal(TimeSpan.FromSeconds(30), d.Value);
    }

    [Fact]
    public void Parse_Minutes()
    {
        var d = Duration.Parse("5m");
        Assert.Equal(TimeSpan.FromMinutes(5), d.Value);
    }

    [Fact]
    public void Parse_Hours()
    {
        var d = Duration.Parse("2h");
        Assert.Equal(TimeSpan.FromHours(2), d.Value);
    }

    [Fact]
    public void Parse_Milliseconds()
    {
        var d = Duration.Parse("100ms");
        Assert.Equal(TimeSpan.FromMilliseconds(100), d.Value);
    }

    [Fact]
    public void Parse_Combined()
    {
        var d = Duration.Parse("1h30m10s");
        Assert.Equal(new TimeSpan(1, 30, 10), d.Value);
    }

    [Fact]
    public void Parse_Zero()
    {
        var d = Duration.Parse("0");
        Assert.Equal(TimeSpan.Zero, d.Value);
    }

    [Fact]
    public void Parse_Negative()
    {
        var d = Duration.Parse("-5s");
        Assert.Equal(TimeSpan.FromSeconds(-5), d.Value);
    }

    [Fact]
    public void Parse_Microseconds()
    {
        var d = Duration.Parse("500us");
        Assert.Equal(TimeSpan.FromTicks(500 * 10), d.Value); // 1 Âµs = 10 ticks
    }

    [Fact]
    public void Parse_Nanoseconds()
    {
        var d = Duration.Parse("1000ns");
        // 1 ns = 0.01 ticks, 1000ns = 1Âµs = 10 ticks
        Assert.Equal(TimeSpan.FromTicks(10), d.Value);
    }

    // â”€â”€ TryParse â”€â”€

    [Fact]
    public void TryParse_Valid_ReturnsTrue()
    {
        Assert.True(Duration.TryParse("5s", out var d));
        Assert.Equal(TimeSpan.FromSeconds(5), d.Value);
    }

    [Fact]
    public void TryParse_Invalid_ReturnsFalse()
    {
        Assert.False(Duration.TryParse("invalid", out _));
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        Assert.False(Duration.TryParse(null, out _));
    }

    [Fact]
    public void TryParse_Empty_ReturnsFalse()
    {
        Assert.False(Duration.TryParse("", out _));
    }

    // â”€â”€ ToString â”€â”€

    [Fact]
    public void ToString_RoundTrip()
    {
        var d = Duration.Parse("1h30m10s");
        var str = d.ToString();
        var d2 = Duration.Parse(str);
        Assert.Equal(d.Value, d2.Value);
    }

    // â”€â”€ Implicit conversion â”€â”€

    [Fact]
    public void ImplicitConversion_ToTimeSpan()
    {
        Duration d = Duration.Parse("5s");
        TimeSpan ts = d;
        Assert.Equal(TimeSpan.FromSeconds(5), ts);
    }

    [Fact]
    public void ImplicitConversion_FromTimeSpan()
    {
        Duration d = TimeSpan.FromMinutes(2);
        Assert.Equal(TimeSpan.FromMinutes(2), d.Value);
    }

    // â”€â”€ Comparison â”€â”€

    [Fact]
    public void Comparison_LessThan()
    {
        var d1 = Duration.Parse("1s");
        var d2 = Duration.Parse("2s");
        Assert.True(d1 < d2);
    }

    [Fact]
    public void Equality()
    {
        var d1 = Duration.Parse("60s");
        var d2 = Duration.Parse("1m");
        Assert.Equal(d1, d2);
    }
}

public class UnitBytesTests
{
    // â”€â”€ Parsing â”€â”€

    [Fact]
    public void Parse_Bytes()
    {
        var b = UnitBytes.Parse("1024b");
        Assert.Equal(1024L, b.Bytes);
    }

    [Fact]
    public void Parse_Kilobytes()
    {
        var b = UnitBytes.Parse("1kb");
        Assert.Equal(1024L, b.Bytes);
    }

    [Fact]
    public void Parse_Megabytes()
    {
        var b = UnitBytes.Parse("1mb");
        Assert.Equal(1024L * 1024, b.Bytes);
    }

    [Fact]
    public void Parse_Gigabytes()
    {
        var b = UnitBytes.Parse("1gb");
        Assert.Equal(1024L * 1024 * 1024, b.Bytes);
    }

    [Fact]
    public void Parse_Terabytes()
    {
        var b = UnitBytes.Parse("1tb");
        Assert.Equal(1024L * 1024 * 1024 * 1024, b.Bytes);
    }

    [Fact]
    public void Parse_ShortSuffix()
    {
        var b = UnitBytes.Parse("5k");
        Assert.Equal(5 * 1024L, b.Bytes);
    }

    [Fact]
    public void Parse_CaseInsensitive()
    {
        var b = UnitBytes.Parse("1GB");
        Assert.Equal(1024L * 1024 * 1024, b.Bytes);
    }

    [Fact]
    public void Parse_PlainNumber()
    {
        var b = UnitBytes.Parse("4096");
        Assert.Equal(4096L, b.Bytes);
    }

    // â”€â”€ TryParse â”€â”€

    [Fact]
    public void TryParse_Valid()
    {
        Assert.True(UnitBytes.TryParse("512mb", out var b));
        Assert.Equal(512 * 1024L * 1024, b.Bytes);
    }

    [Fact]
    public void TryParse_Invalid()
    {
        Assert.False(UnitBytes.TryParse("abc", out _));
    }

    [Fact]
    public void TryParse_Null()
    {
        Assert.False(UnitBytes.TryParse(null, out _));
    }

    // â”€â”€ ToString â”€â”€

    [Fact]
    public void ToString_Bytes()
    {
        var b = new UnitBytes(512);
        Assert.Equal("512B", b.ToString());
    }

    [Fact]
    public void ToString_Kilobytes()
    {
        var b = new UnitBytes(1024);
        Assert.Equal("1KB", b.ToString());
    }

    [Fact]
    public void ToString_Megabytes()
    {
        var b = new UnitBytes(1024 * 1024);
        Assert.Equal("1MB", b.ToString());
    }

    // â”€â”€ Implicit conversion â”€â”€

    [Fact]
    public void ImplicitConversion_ToLong()
    {
        UnitBytes b = new UnitBytes(4096);
        long val = b;
        Assert.Equal(4096L, val);
    }

    [Fact]
    public void ImplicitConversion_FromLong()
    {
        UnitBytes b = 2048L;
        Assert.Equal(2048L, b.Bytes);
    }

    // â”€â”€ Comparison â”€â”€

    [Fact]
    public void Comparison()
    {
        var b1 = UnitBytes.Parse("1kb");
        var b2 = UnitBytes.Parse("1mb");
        Assert.True(b1 < b2);
    }

    [Fact]
    public void Equality()
    {
        var b1 = UnitBytes.Parse("1024b");
        var b2 = UnitBytes.Parse("1kb");
        Assert.Equal(b1, b2);
    }
}
