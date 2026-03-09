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

public class MappingTests
{
    // â”€â”€ NewMapping â”€â”€

    [Fact]
    public void NewMapping_ParsesKeyValue()
    {
        var m = Mapping.NewMapping(new[] { "FOO=bar", "BAZ=qux" });
        Assert.Equal("bar", m["FOO"]);
        Assert.Equal("qux", m["BAZ"]);
    }

    [Fact]
    public void NewMapping_NoEquals_EmptyStringValue()
    {
        var m = Mapping.NewMapping(new[] { "FOO" });
        Assert.Equal("", m["FOO"]);
    }

    [Fact]
    public void NewMapping_ValueWithEquals()
    {
        var m = Mapping.NewMapping(new[] { "FOO=bar=baz" });
        Assert.Equal("bar=baz", m["FOO"]);
    }

    // â”€â”€ ToValues â”€â”€

    [Fact]
    public void ToValues_SortedOutput()
    {
        var m = Mapping.NewMapping(new[] { "Z=1", "A=2", "M=3" });
        var values = m.ToValues();
        Assert.Equal(new[] { "A=2", "M=3", "Z=1" }, values);
    }

    // â”€â”€ Resolve â”€â”€

    [Fact]
    public void Resolve_ExistingKey_Found()
    {
        var m = Mapping.NewMapping(new[] { "FOO=bar" });
        var (value, found) = m.Resolve("FOO");
        Assert.True(found);
        Assert.Equal("bar", value);
    }

    [Fact]
    public void Resolve_MissingKey_NotFound()
    {
        var m = Mapping.NewMapping(new[] { "FOO=bar" });
        var (value, found) = m.Resolve("MISSING");
        Assert.False(found);
        Assert.Equal("", value);
    }

    // â”€â”€ Clone â”€â”€

    [Fact]
    public void Clone_IsIndependent()
    {
        var m = Mapping.NewMapping(new[] { "FOO=bar" });
        var clone = m.Clone();
        clone["FOO"] = "changed";
        Assert.Equal("bar", m["FOO"]);
    }

    // â”€â”€ Merge â”€â”€

    [Fact]
    public void Merge_DoesNotOverwrite()
    {
        var m1 = Mapping.NewMapping(new[] { "A=1" });
        var m2 = Mapping.NewMapping(new[] { "A=2", "B=3" });
        m1.Merge(m2);
        Assert.Equal("1", m1["A"]); // not overwritten
        Assert.Equal("3", m1["B"]); // added
    }

    // â”€â”€ ToMappingWithEquals â”€â”€

    [Fact]
    public void ToMappingWithEquals_Conversion()
    {
        var m = Mapping.NewMapping(new[] { "FOO=bar" });
        var mwe = m.ToMappingWithEquals();
        Assert.Equal("bar", mwe["FOO"]);
    }
}

public class MappingWithEqualsTests
{
    // â”€â”€ NewMappingWithEquals â”€â”€

    [Fact]
    public void NewMappingWithEquals_ParsesKeyValue()
    {
        var m = MappingWithEquals.NewMappingWithEquals(new[] { "FOO=bar", "BAZ=qux" });
        Assert.Equal("bar", m["FOO"]);
        Assert.Equal("qux", m["BAZ"]);
    }

    [Fact]
    public void NewMappingWithEquals_NoEquals_NullValue()
    {
        var m = MappingWithEquals.NewMappingWithEquals(new[] { "FOO" });
        Assert.True(m.ContainsKey("FOO"));
        Assert.Null(m["FOO"]);
    }

    // â”€â”€ OverrideBy â”€â”€

    [Fact]
    public void OverrideBy_OverwritesExisting()
    {
        var m1 = MappingWithEquals.NewMappingWithEquals(new[] { "A=1" });
        var m2 = MappingWithEquals.NewMappingWithEquals(new[] { "A=2" });
        m1.OverrideBy(m2);
        Assert.Equal("2", m1["A"]);
    }

    [Fact]
    public void OverrideBy_AddsNew()
    {
        var m1 = MappingWithEquals.NewMappingWithEquals(new[] { "A=1" });
        var m2 = MappingWithEquals.NewMappingWithEquals(new[] { "B=2" });
        m1.OverrideBy(m2);
        Assert.Equal("2", m1["B"]);
    }

    // â”€â”€ Resolve â”€â”€

    [Fact]
    public void Resolve_NullValuesResolved()
    {
        var m = MappingWithEquals.NewMappingWithEquals(new[] { "FOO" });
        var resolved = m.Resolve(key =>
            key == "FOO" ? ("resolved", true) : ("", false));
        Assert.Equal("resolved", resolved["FOO"]);
    }

    [Fact]
    public void Resolve_NullValueUnresolved_StaysNull()
    {
        var m = MappingWithEquals.NewMappingWithEquals(new[] { "FOO" });
        var resolved = m.Resolve(key => ("", false));
        Assert.Null(resolved["FOO"]);
    }

    // â”€â”€ RemoveEmpty â”€â”€

    [Fact]
    public void RemoveEmpty_RemovesNullValues()
    {
        var m = MappingWithEquals.NewMappingWithEquals(new[] { "FOO", "BAR=value" });
        var cleaned = m.RemoveEmpty();
        Assert.DoesNotContain("FOO", cleaned.Keys);
        Assert.Equal("value", cleaned["BAR"]);
    }

    // â”€â”€ ToMapping â”€â”€

    [Fact]
    public void ToMapping_DropsNullValues()
    {
        var m = MappingWithEquals.NewMappingWithEquals(new[] { "FOO", "BAR=value" });
        var mapping = m.ToMapping();
        Assert.DoesNotContain("FOO", (IDictionary<string, string>)mapping);
        Assert.Equal("value", mapping["BAR"]);
    }
}
