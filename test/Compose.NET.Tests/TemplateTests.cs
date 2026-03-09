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
using Compose.NET.Interpolation;

namespace Compose.NET.Tests;

public class TemplateTests
{
    private static (string Value, bool Found) Lookup(string key)
    {
        return key switch
        {
            "FOO" => ("foo_value", true),
            "BAR" => ("bar_value", true),
            "EMPTY" => ("", true),
            "NUM" => ("42", true),
            _ => ("", false)
        };
    }

    // â”€â”€ Basic substitution â”€â”€

    [Fact]
    public void Substitute_NoVariables_ReturnsOriginal()
    {
        Assert.Equal("hello world", Template.Substitute("hello world", Lookup));
    }

    [Fact]
    public void Substitute_NullOrEmpty_ReturnsOriginal()
    {
        Assert.Equal("", Template.Substitute("", Lookup));
    }

    [Fact]
    public void Substitute_SimpleVar_Replaced()
    {
        Assert.Equal("foo_value", Template.Substitute("$FOO", Lookup));
    }

    [Fact]
    public void Substitute_BracedVar_Replaced()
    {
        Assert.Equal("foo_value", Template.Substitute("${FOO}", Lookup));
    }

    [Fact]
    public void Substitute_VarInText_Replaced()
    {
        Assert.Equal("prefix-foo_value-suffix", Template.Substitute("prefix-${FOO}-suffix", Lookup));
    }

    [Fact]
    public void Substitute_MultipleVars_AllReplaced()
    {
        Assert.Equal("foo_value and bar_value", Template.Substitute("$FOO and $BAR", Lookup));
    }

    [Fact]
    public void Substitute_UnsetVar_ReturnsEmpty()
    {
        Assert.Equal("", Template.Substitute("$UNSET", Lookup));
    }

    // â”€â”€ Escaping â”€â”€

    [Fact]
    public void Substitute_DoubleDollar_EscapedToSingleDollar()
    {
        Assert.Equal("$FOO", Template.Substitute("$$FOO", Lookup));
    }

    [Fact]
    public void Substitute_TrailingDollar_Preserved()
    {
        Assert.Equal("test$", Template.Substitute("test$", Lookup));
    }

    [Fact]
    public void Substitute_DollarFollowedByNonAlpha_Preserved()
    {
        Assert.Equal("$1", Template.Substitute("$1", Lookup));
    }

    // â”€â”€ Default value operator :- (unset or empty) â”€â”€

    [Fact]
    public void Substitute_ColonDash_SetVar_ReturnsValue()
    {
        Assert.Equal("foo_value", Template.Substitute("${FOO:-default}", Lookup));
    }

    [Fact]
    public void Substitute_ColonDash_EmptyVar_ReturnsDefault()
    {
        Assert.Equal("default", Template.Substitute("${EMPTY:-default}", Lookup));
    }

    [Fact]
    public void Substitute_ColonDash_UnsetVar_ReturnsDefault()
    {
        Assert.Equal("default", Template.Substitute("${UNSET:-default}", Lookup));
    }

    // â”€â”€ Default value operator - (unset only) â”€â”€

    [Fact]
    public void Substitute_Dash_SetVar_ReturnsValue()
    {
        Assert.Equal("foo_value", Template.Substitute("${FOO-default}", Lookup));
    }

    [Fact]
    public void Substitute_Dash_EmptyVar_ReturnsEmpty()
    {
        Assert.Equal("", Template.Substitute("${EMPTY-default}", Lookup));
    }

    [Fact]
    public void Substitute_Dash_UnsetVar_ReturnsDefault()
    {
        Assert.Equal("default", Template.Substitute("${UNSET-default}", Lookup));
    }

    // â”€â”€ Alternate value operator :+ (set and non-empty) â”€â”€

    [Fact]
    public void Substitute_ColonPlus_SetVar_ReturnsAlternate()
    {
        Assert.Equal("alternate", Template.Substitute("${FOO:+alternate}", Lookup));
    }

    [Fact]
    public void Substitute_ColonPlus_EmptyVar_ReturnsEmpty()
    {
        Assert.Equal("", Template.Substitute("${EMPTY:+alternate}", Lookup));
    }

    [Fact]
    public void Substitute_ColonPlus_UnsetVar_ReturnsEmpty()
    {
        Assert.Equal("", Template.Substitute("${UNSET:+alternate}", Lookup));
    }

    // â”€â”€ Alternate value operator + (set) â”€â”€

    [Fact]
    public void Substitute_Plus_SetVar_ReturnsAlternate()
    {
        Assert.Equal("alternate", Template.Substitute("${FOO+alternate}", Lookup));
    }

    [Fact]
    public void Substitute_Plus_EmptyVar_ReturnsAlternate()
    {
        Assert.Equal("alternate", Template.Substitute("${EMPTY+alternate}", Lookup));
    }

    [Fact]
    public void Substitute_Plus_UnsetVar_ReturnsEmpty()
    {
        Assert.Equal("", Template.Substitute("${UNSET+alternate}", Lookup));
    }

    // â”€â”€ Error operator :? (unset or empty) â”€â”€

    [Fact]
    public void Substitute_ColonQuestion_SetVar_ReturnsValue()
    {
        Assert.Equal("foo_value", Template.Substitute("${FOO:?error msg}", Lookup));
    }

    [Fact]
    public void Substitute_ColonQuestion_EmptyVar_Throws()
    {
        Assert.Throws<MissingRequiredVariableException>(
            () => Template.Substitute("${EMPTY:?must not be empty}", Lookup));
    }

    [Fact]
    public void Substitute_ColonQuestion_UnsetVar_Throws()
    {
        Assert.Throws<MissingRequiredVariableException>(
            () => Template.Substitute("${UNSET:?must be set}", Lookup));
    }

    // â”€â”€ Error operator ? (unset only) â”€â”€

    [Fact]
    public void Substitute_Question_SetVar_ReturnsValue()
    {
        Assert.Equal("foo_value", Template.Substitute("${FOO?error msg}", Lookup));
    }

    [Fact]
    public void Substitute_Question_EmptyVar_ReturnsEmpty()
    {
        Assert.Equal("", Template.Substitute("${EMPTY?error msg}", Lookup));
    }

    [Fact]
    public void Substitute_Question_UnsetVar_Throws()
    {
        Assert.Throws<MissingRequiredVariableException>(
            () => Template.Substitute("${UNSET?must be set}", Lookup));
    }

    // â”€â”€ Nested expressions â”€â”€

    [Fact]
    public void Substitute_NestedDefault_Resolved()
    {
        Assert.Equal("bar_value", Template.Substitute("${UNSET:-${BAR}}", Lookup));
    }

    [Fact]
    public void Substitute_DoubleNestedDefault_Resolved()
    {
        Assert.Equal("fallback", Template.Substitute("${UNSET:-${ALSO_UNSET:-fallback}}", Lookup));
    }

    // â”€â”€ Invalid templates â”€â”€

    [Fact]
    public void Substitute_EmptyBraces_Throws()
    {
        Assert.Throws<InvalidTemplateException>(
            () => Template.Substitute("${}", Lookup));
    }

    [Fact]
    public void Substitute_MissingClosingBrace_Throws()
    {
        Assert.Throws<InvalidTemplateException>(
            () => Template.Substitute("${FOO", Lookup));
    }

    // â”€â”€ Variable name validation â”€â”€

    [Fact]
    public void IsValidVariableName_ValidNames()
    {
        Assert.True(Template.IsValidVariableName("FOO"));
        Assert.True(Template.IsValidVariableName("_bar"));
        Assert.True(Template.IsValidVariableName("a1_B2"));
    }

    [Fact]
    public void IsValidVariableName_InvalidNames()
    {
        Assert.False(Template.IsValidVariableName(""));
        Assert.False(Template.IsValidVariableName("1abc"));
        Assert.False(Template.IsValidVariableName("foo-bar"));
        Assert.False(Template.IsValidVariableName("foo.bar"));
    }
}
