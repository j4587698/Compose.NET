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
using Compose.NET.Loader;
using Compose.NET.Types;

namespace Compose.NET.Tests;

public class ProfileFilterTests
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Profile filtering during loading
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void LoadWithProfiles_ServiceWithNoProfile_AlwaysActive()
    {
        var yaml = @"
services:
  web:
    image: nginx
  db:
    image: postgres
    profiles:
      - dev
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string> { "prod" }
        });

        Assert.True(result.Project.Services.ContainsKey("web"));
        Assert.False(result.Project.Services.ContainsKey("db"));
        Assert.True(result.Project.DisabledServices.ContainsKey("db"));
    }

    [Fact]
    public void LoadWithProfiles_MatchingProfile_ServiceEnabled()
    {
        var yaml = @"
services:
  web:
    image: nginx
  db:
    image: postgres
    profiles:
      - dev
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string> { "dev" }
        });

        Assert.True(result.Project.Services.ContainsKey("web"));
        Assert.True(result.Project.Services.ContainsKey("db"));
        Assert.Empty(result.Project.DisabledServices);
    }

    [Fact]
    public void LoadWithProfiles_MultipleProfiles_AnyMatch()
    {
        var yaml = @"
services:
  worker:
    image: worker
    profiles:
      - dev
      - staging
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string> { "staging" }
        });

        Assert.True(result.Project.Services.ContainsKey("worker"));
    }

    [Fact]
    public void LoadWithProfiles_NoMatch_Disabled()
    {
        var yaml = @"
services:
  worker:
    image: worker
    profiles:
      - dev
      - staging
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string> { "prod" }
        });

        Assert.False(result.Project.Services.ContainsKey("worker"));
        Assert.True(result.Project.DisabledServices.ContainsKey("worker"));
    }

    [Fact]
    public void LoadWithProfiles_Wildcard_ActivatesAll()
    {
        var yaml = @"
services:
  web:
    image: nginx
  db:
    image: postgres
    profiles:
      - dev
  worker:
    image: worker
    profiles:
      - staging
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string> { "*" }
        });

        Assert.Equal(3, result.Project.Services.Count);
        Assert.Empty(result.Project.DisabledServices);
    }

    [Fact]
    public void LoadWithProfiles_NullProfiles_NoFiltering()
    {
        var yaml = @"
services:
  web:
    image: nginx
  db:
    image: postgres
    profiles:
      - dev
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = null
        });

        // When no profiles are specified, no filtering should occur
        Assert.Equal(2, result.Project.Services.Count);
        Assert.True(result.Project.Services.ContainsKey("web"));
        Assert.True(result.Project.Services.ContainsKey("db"));
    }

    [Fact]
    public void LoadWithProfiles_EmptyProfilesList_OnlyNoProfileServicesActive()
    {
        var yaml = @"
services:
  web:
    image: nginx
  db:
    image: postgres
    profiles:
      - dev
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string>()
        });

        // Empty profiles list means only services without profiles are active
        Assert.True(result.Project.Services.ContainsKey("web"));
        Assert.False(result.Project.Services.ContainsKey("db"));
        Assert.True(result.Project.DisabledServices.ContainsKey("db"));
    }

    [Fact]
    public void LoadWithProfiles_ProfilesStoredOnProject()
    {
        var yaml = @"
services:
  web:
    image: nginx
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string> { "dev", "test" }
        });

        Assert.Equal(new List<string> { "dev", "test" }, result.Project.Profiles);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Consistency check interaction with profiles
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void LoadWithProfiles_DependsOnDisabledService_RequiredFalse_NoError()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      db:
        condition: service_started
        required: false
  db:
    image: postgres
    profiles:
      - dev
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string> { "prod" }
        });

        Assert.True(result.Project.Services.ContainsKey("web"));
        Assert.False(result.Project.Services.ContainsKey("db"));
    }

    [Fact]
    public void LoadWithProfiles_DependsOnDisabledService_RequiredTrue_Throws()
    {
        var yaml = @"
services:
  web:
    image: nginx
    depends_on:
      db:
        condition: service_started
        required: true
  db:
    image: postgres
    profiles:
      - dev
";
        var ex = Assert.Throws<ComposeInvalidException>(() =>
            ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
            {
                Profiles = new List<string> { "prod" }
            }));

        Assert.Contains("disabled service", ex.Message);
    }

    [Fact]
    public void LoadWithProfiles_NetworkModeRefDisabledService_ImplicitDependsOn_Throws()
    {
        // network_mode: service:xxx creates an implicit depends_on with required: true
        // When the referenced service is disabled, this should fail consistency check
        var yaml = @"
services:
  web:
    image: nginx
    network_mode: ""service:proxy""
  proxy:
    image: envoy
    profiles:
      - dev
";
        var ex = Assert.Throws<ComposeInvalidException>(() =>
            ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
            {
                Profiles = new List<string> { "prod" }
            }));

        Assert.Contains("disabled service", ex.Message);
    }

    [Fact]
    public void LoadWithProfiles_NetworkModeRefDisabledService_SkipConsistencyCheck_NoError()
    {
        // With consistency check skipped, profile filtering still works
        var yaml = @"
services:
  web:
    image: nginx
    network_mode: ""service:proxy""
  proxy:
    image: envoy
    profiles:
      - dev
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions
        {
            Profiles = new List<string> { "prod" },
            SkipConsistencyCheck = true
        });

        Assert.True(result.Project.Services.ContainsKey("web"));
        Assert.True(result.Project.DisabledServices.ContainsKey("proxy"));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // WithProfiles on Project directly
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void WithProfiles_ReturnsSeparateInstance()
    {
        var project = new Project();
        project.Services["web"] = new ServiceConfig { Image = "nginx" };
        project.Services["db"] = new ServiceConfig
        {
            Image = "postgres",
            Profiles = new List<string> { "dev" }
        };

        var filtered = project.WithProfiles(new[] { "prod" });

        // Original unchanged
        Assert.Equal(2, project.Services.Count);
        // Filtered result
        Assert.Single(filtered.Services);
        Assert.True(filtered.Services.ContainsKey("web"));
        Assert.True(filtered.DisabledServices.ContainsKey("db"));
    }

    [Fact]
    public void HasProfile_NoProfiles_AlwaysTrue()
    {
        var svc = new ServiceConfig { Image = "nginx" };
        Assert.True(svc.HasProfile(new[] { "anything" }));
    }

    [Fact]
    public void HasProfile_WithProfiles_Match()
    {
        var svc = new ServiceConfig
        {
            Image = "nginx",
            Profiles = new List<string> { "dev", "test" }
        };
        Assert.True(svc.HasProfile(new[] { "test" }));
    }

    [Fact]
    public void HasProfile_WithProfiles_NoMatch()
    {
        var svc = new ServiceConfig
        {
            Image = "nginx",
            Profiles = new List<string> { "dev", "test" }
        };
        Assert.False(svc.HasProfile(new[] { "prod" }));
    }

    [Fact]
    public void HasProfile_Wildcard_MatchesAll()
    {
        var svc = new ServiceConfig
        {
            Image = "nginx",
            Profiles = new List<string> { "dev" }
        };
        Assert.True(svc.HasProfile(new[] { "*" }));
    }

    [Fact]
    public void HasProfile_EmptyActiveProfiles_WithServiceProfiles_ReturnsFalse()
    {
        var svc = new ServiceConfig
        {
            Image = "nginx",
            Profiles = new List<string> { "dev" }
        };
        Assert.False(svc.HasProfile(Array.Empty<string>()));
    }
}
