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
using Compose.NET.Errors;
using Compose.NET.Types;

namespace Compose.NET.Tests;

public class ComposeErrorTests
{
    // â”€â”€ Exception hierarchy â”€â”€

    [Fact]
    public void ComposeNotFoundException_IsComposeException()
    {
        var ex = new ComposeNotFoundException("not found");
        Assert.IsAssignableFrom<ComposeException>(ex);
        Assert.Equal("not found", ex.Message);
    }

    [Fact]
    public void ComposeInvalidException_IsComposeException()
    {
        var ex = new ComposeInvalidException("invalid config");
        Assert.IsAssignableFrom<ComposeException>(ex);
        Assert.Equal("invalid config", ex.Message);
    }

    [Fact]
    public void ComposeUnsupportedException_IsComposeException()
    {
        var ex = new ComposeUnsupportedException("not supported");
        Assert.IsAssignableFrom<ComposeException>(ex);
    }

    [Fact]
    public void ComposeIncompatibleException_IsComposeException()
    {
        var ex = new ComposeIncompatibleException("incompatible");
        Assert.IsAssignableFrom<ComposeException>(ex);
    }

    [Fact]
    public void ComposeDisabledException_IsComposeException()
    {
        var ex = new ComposeDisabledException("web");
        Assert.IsAssignableFrom<ComposeException>(ex);
        Assert.Equal("web", ex.ServiceName);
        Assert.Contains("web", ex.Message);
        Assert.Contains("disabled", ex.Message);
    }

    [Fact]
    public void ComposeDisabledException_CustomMessage()
    {
        var ex = new ComposeDisabledException("db", "custom msg");
        Assert.Equal("db", ex.ServiceName);
        Assert.Equal("custom msg", ex.Message);
    }

    // â”€â”€ InnerException wrapping â”€â”€

    [Fact]
    public void ComposeNotFoundException_WithInnerException()
    {
        var inner = new Exception("root cause");
        var ex = new ComposeNotFoundException("not found", inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void ComposeInvalidException_WithInnerException()
    {
        var inner = new Exception("root cause");
        var ex = new ComposeInvalidException("invalid", inner);
        Assert.Same(inner, ex.InnerException);
    }

    // â”€â”€ Helper methods â”€â”€

    [Fact]
    public void IsNotFound_DirectMatch_ReturnsTrue()
    {
        var ex = new ComposeNotFoundException("msg");
        Assert.True(ComposeErrorHelpers.IsNotFound(ex));
    }

    [Fact]
    public void IsNotFound_WrappedMatch_ReturnsTrue()
    {
        var inner = new ComposeNotFoundException("msg");
        var outer = new Exception("wrapper", inner);
        Assert.True(ComposeErrorHelpers.IsNotFound(outer));
    }

    [Fact]
    public void IsNotFound_NoMatch_ReturnsFalse()
    {
        var ex = new ComposeInvalidException("msg");
        Assert.False(ComposeErrorHelpers.IsNotFound(ex));
    }

    [Fact]
    public void IsNotFound_Null_ReturnsFalse()
    {
        Assert.False(ComposeErrorHelpers.IsNotFound(null));
    }

    [Fact]
    public void IsInvalid_DirectMatch_ReturnsTrue()
    {
        Assert.True(ComposeErrorHelpers.IsInvalid(new ComposeInvalidException("msg")));
    }

    [Fact]
    public void IsUnsupported_DirectMatch_ReturnsTrue()
    {
        Assert.True(ComposeErrorHelpers.IsUnsupported(new ComposeUnsupportedException("msg")));
    }

    [Fact]
    public void IsIncompatible_DirectMatch_ReturnsTrue()
    {
        Assert.True(ComposeErrorHelpers.IsIncompatible(new ComposeIncompatibleException("msg")));
    }

    [Fact]
    public void IsDisabled_DirectMatch_ReturnsTrue()
    {
        Assert.True(ComposeErrorHelpers.IsDisabled(new ComposeDisabledException("web")));
    }

    [Fact]
    public void IsDisabled_WrappedDeep_ReturnsTrue()
    {
        var disabled = new ComposeDisabledException("web");
        var mid = new ComposeInvalidException("mid", disabled);
        var outer = new Exception("outer", mid);
        Assert.True(ComposeErrorHelpers.IsDisabled(outer));
    }

    // â”€â”€ Integration: Project.GetService uses typed exceptions â”€â”€

    [Fact]
    public void GetService_NotFound_ThrowsComposeNotFoundException()
    {
        var project = new Project
        {
            Services = new Services
            {
                ["web"] = new ServiceConfig { Image = "nginx" }
            }
        };

        var ex = Assert.Throws<ComposeNotFoundException>(() => project.GetService("unknown"));
        Assert.Contains("unknown", ex.Message);
    }

    [Fact]
    public void GetService_Disabled_ThrowsComposeDisabledException()
    {
        var project = new Project
        {
            Services = new Services
            {
                ["web"] = new ServiceConfig { Image = "nginx" }
            },
            DisabledServices = new Services
            {
                ["db"] = new ServiceConfig { Image = "postgres" }
            }
        };

        var ex = Assert.Throws<ComposeDisabledException>(() => project.GetService("db"));
        Assert.Equal("db", ex.ServiceName);
    }

    [Fact]
    public void GetService_Enabled_ReturnsService()
    {
        var project = new Project
        {
            Services = new Services
            {
                ["web"] = new ServiceConfig { Image = "nginx" }
            }
        };

        var svc = project.GetService("web");
        Assert.Equal("nginx", svc.Image);
    }
}
