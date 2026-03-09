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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Compose.NET.Errors;
using Compose.NET.Graph;
using Compose.NET.Types;
using Xunit;

namespace Compose.NET.Tests;

public class ServiceGraphTests
{
    #region Helper methods

    private static Project CreateProject(
        Dictionary<string, List<string>?> services)
    {
        var project = new Project();
        foreach (var (name, deps) in services)
        {
            var svc = new ServiceConfig
            {
                Name = name,
                Image = $"{name}:latest"
            };
            if (deps != null && deps.Count > 0)
            {
                svc.DependsOn = new DependsOnConfig();
                foreach (var dep in deps)
                {
                    svc.DependsOn[dep] = new ServiceDependency
                    {
                        Condition = "service_started",
                        Required = true
                    };
                }
            }
            project.Services[name] = svc;
        }
        return project;
    }

    #endregion

    #region InDependencyOrder (sync)

    [Fact]
    public void Sync_NoDependencies_VisitsAll()
    {
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["web"] = null,
            ["db"] = null,
            ["cache"] = null,
        });

        var visited = new List<string>();
        ServiceGraph.InDependencyOrder(project, (name, svc) => visited.Add(name));

        Assert.Equal(3, visited.Count);
        Assert.Contains("web", visited);
        Assert.Contains("db", visited);
        Assert.Contains("cache", visited);
    }

    [Fact]
    public void Sync_LinearChain_DependenciesFirst()
    {
        // app -> api -> db
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
        });

        var visited = new List<string>();
        ServiceGraph.InDependencyOrder(project, (name, svc) => visited.Add(name));

        Assert.Equal(3, visited.Count);
        Assert.True(visited.IndexOf("db") < visited.IndexOf("api"));
        Assert.True(visited.IndexOf("api") < visited.IndexOf("app"));
    }

    [Fact]
    public void Sync_ReverseOrder_DependentsFirst()
    {
        // app -> api -> db
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
        });

        var visited = new List<string>();
        ServiceGraph.InDependencyOrder(project, (name, svc) => visited.Add(name),
            new TraversalOptions { ReverseOrder = true });

        Assert.Equal(3, visited.Count);
        Assert.True(visited.IndexOf("app") < visited.IndexOf("api"));
        Assert.True(visited.IndexOf("api") < visited.IndexOf("db"));
    }

    [Fact]
    public void Sync_RootNodes_OnlyVisitsSubgraph()
    {
        // app -> api -> db, worker -> cache
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
            ["worker"] = new() { "cache" },
            ["cache"] = null,
        });

        var visited = new List<string>();
        ServiceGraph.InDependencyOrder(project, (name, svc) => visited.Add(name),
            new TraversalOptions { RootNodes = new[] { "app" } });

        Assert.Equal(3, visited.Count);
        Assert.Contains("app", visited);
        Assert.Contains("api", visited);
        Assert.Contains("db", visited);
        Assert.DoesNotContain("worker", visited);
        Assert.DoesNotContain("cache", visited);
    }

    [Fact]
    public void Sync_CycleDetected_Throws()
    {
        // a -> b -> c -> a
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["a"] = new() { "b" },
            ["b"] = new() { "c" },
            ["c"] = new() { "a" },
        });

        Assert.Throws<ComposeInvalidException>(() =>
            ServiceGraph.InDependencyOrder(project, (name, svc) => { }));
    }

    [Fact]
    public void Sync_EmptyProject_NoError()
    {
        var project = new Project();
        var visited = new List<string>();

        ServiceGraph.InDependencyOrder(project, (name, svc) => visited.Add(name));

        Assert.Empty(visited);
    }

    [Fact]
    public void Sync_DiamondDependency_AllVisited()
    {
        //   app
        //  /   \
        // api  worker
        //  \   /
        //   db
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api", "worker" },
            ["api"] = new() { "db" },
            ["worker"] = new() { "db" },
            ["db"] = null,
        });

        var visited = new List<string>();
        ServiceGraph.InDependencyOrder(project, (name, svc) => visited.Add(name));

        Assert.Equal(4, visited.Count);
        Assert.True(visited.IndexOf("db") < visited.IndexOf("api"));
        Assert.True(visited.IndexOf("db") < visited.IndexOf("worker"));
        Assert.True(visited.IndexOf("api") < visited.IndexOf("app"));
        Assert.True(visited.IndexOf("worker") < visited.IndexOf("app"));
    }

    [Fact]
    public void Sync_RootNodes_NotFound_Throws()
    {
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["web"] = null,
        });

        Assert.Throws<ComposeNotFoundException>(() =>
            ServiceGraph.InDependencyOrder(project, (name, svc) => { },
                new TraversalOptions { RootNodes = new[] { "nonexistent" } }));
    }

    #endregion

    #region InDependencyOrderAsync

    [Fact]
    public async Task Async_NoDependencies_VisitsAll()
    {
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["web"] = null,
            ["db"] = null,
            ["cache"] = null,
        });

        var visited = new ConcurrentBag<string>();
        await ServiceGraph.InDependencyOrderAsync(project, (name, svc, ct) =>
        {
            visited.Add(name);
            return Task.CompletedTask;
        });

        Assert.Equal(3, visited.Count);
        Assert.Contains("web", visited);
        Assert.Contains("db", visited);
        Assert.Contains("cache", visited);
    }

    [Fact]
    public async Task Async_LinearChain_DependenciesFirst()
    {
        // app -> api -> db
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
        });

        var visited = new ConcurrentQueue<string>();
        await ServiceGraph.InDependencyOrderAsync(project, (name, svc, ct) =>
        {
            visited.Enqueue(name);
            return Task.CompletedTask;
        });

        var list = visited.ToList();
        Assert.Equal(3, list.Count);
        Assert.True(list.IndexOf("db") < list.IndexOf("api"));
        Assert.True(list.IndexOf("api") < list.IndexOf("app"));
    }

    [Fact]
    public async Task Async_ConcurrentExecution_RespectsMaxConcurrency()
    {
        // Three independent services, max concurrency 1
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["a"] = null,
            ["b"] = null,
            ["c"] = null,
        });

        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        await ServiceGraph.InDependencyOrderAsync(project, async (name, svc, ct) =>
        {
            lock (lockObj)
            {
                currentConcurrent++;
                if (currentConcurrent > maxConcurrent)
                    maxConcurrent = currentConcurrent;
            }

            await Task.Delay(50, ct);

            lock (lockObj)
            {
                currentConcurrent--;
            }
        }, new TraversalOptions { MaxConcurrency = 1 });

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task Async_UnlimitedConcurrency_AllRunInParallel()
    {
        // Three independent services
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["a"] = null,
            ["b"] = null,
            ["c"] = null,
        });

        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        await ServiceGraph.InDependencyOrderAsync(project, async (name, svc, ct) =>
        {
            lock (lockObj)
            {
                currentConcurrent++;
                if (currentConcurrent > maxConcurrent)
                    maxConcurrent = currentConcurrent;
            }

            await Task.Delay(200, ct);

            lock (lockObj)
            {
                currentConcurrent--;
            }
        }, new TraversalOptions { MaxConcurrency = 0 }); // unlimited

        // All 3 should have run concurrently
        Assert.True(maxConcurrent >= 2, $"Expected at least 2 concurrent, got {maxConcurrent}");
    }

    [Fact]
    public async Task Async_ReverseOrder_DependentsFirst()
    {
        // app -> api -> db
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
        });

        var visited = new ConcurrentQueue<string>();
        await ServiceGraph.InDependencyOrderAsync(project, (name, svc, ct) =>
        {
            visited.Enqueue(name);
            return Task.CompletedTask;
        }, new TraversalOptions { ReverseOrder = true });

        var list = visited.ToList();
        Assert.Equal(3, list.Count);
        Assert.True(list.IndexOf("app") < list.IndexOf("api"));
        Assert.True(list.IndexOf("api") < list.IndexOf("db"));
    }

    [Fact]
    public async Task Async_CycleDetected_Throws()
    {
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["a"] = new() { "b" },
            ["b"] = new() { "a" },
        });

        await Assert.ThrowsAsync<ComposeInvalidException>(() =>
            ServiceGraph.InDependencyOrderAsync(project, (name, svc, ct) =>
                Task.CompletedTask));
    }

    [Fact]
    public async Task Async_ActionThrows_PropagatesAsAggregateException()
    {
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["web"] = null,
        });

        var ex = await Assert.ThrowsAsync<AggregateException>(() =>
            ServiceGraph.InDependencyOrderAsync(project, (name, svc, ct) =>
                throw new InvalidOperationException("test error")));

        Assert.Single(ex.InnerExceptions);
        Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
    }

    [Fact]
    public async Task Async_ActionThrows_WithBlockedDependents_DoesNotHang()
    {
        // app depends on db; db throws before app can start
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "db" },
            ["db"] = null,
        });

        var task = ServiceGraph.InDependencyOrderAsync(project, (name, svc, ct) =>
        {
            if (name == "db")
                throw new InvalidOperationException("db failed");
            return Task.CompletedTask;
        });

        var completed = await Task.WhenAny(task, Task.Delay(3000));
        Assert.Same(task, completed);

        var ex = await Assert.ThrowsAsync<AggregateException>(() => task);
        Assert.Contains(ex.InnerExceptions, e => e is InvalidOperationException ioe && ioe.Message.Contains("db failed"));
    }

    [Fact]
    public async Task Async_CancellationToken_StopsProcessing()
    {
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["a"] = null,
            ["b"] = null,
            ["c"] = null,
        });

        using var cts = new CancellationTokenSource();
        var startCount = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await ServiceGraph.InDependencyOrderAsync(project, async (name, svc, ct) =>
            {
                Interlocked.Increment(ref startCount);
                if (name == "a")
                {
                    cts.Cancel();
                }
                await Task.Delay(1000, ct);
            }, new TraversalOptions { MaxConcurrency = 1 }, cts.Token);
        });
    }

    [Fact]
    public async Task Async_RootNodes_SubgraphOnly()
    {
        // app -> api -> db, worker -> cache
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
            ["worker"] = new() { "cache" },
            ["cache"] = null,
        });

        var visited = new ConcurrentBag<string>();
        await ServiceGraph.InDependencyOrderAsync(project, (name, svc, ct) =>
        {
            visited.Add(name);
            return Task.CompletedTask;
        }, new TraversalOptions { RootNodes = new[] { "api" } });

        Assert.Equal(2, visited.Count);
        Assert.Contains("api", visited);
        Assert.Contains("db", visited);
    }

    [Fact]
    public async Task Async_EmptyProject_NoError()
    {
        var project = new Project();
        var visited = new ConcurrentBag<string>();

        await ServiceGraph.InDependencyOrderAsync(project, (name, svc, ct) =>
        {
            visited.Add(name);
            return Task.CompletedTask;
        });

        Assert.Empty(visited);
    }

    #endregion

    #region CollectInDependencyOrderAsync

    [Fact]
    public async Task Collect_ReturnsResults()
    {
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["web"] = null,
            ["db"] = null,
        });

        var results = await ServiceGraph.CollectInDependencyOrderAsync<int>(
            project, (name, svc, ct) =>
            {
                return Task.FromResult(name.Length);
            });

        Assert.Equal(2, results.Count);
        Assert.Equal(3, results["web"]);
        Assert.Equal(2, results["db"]);
    }

    [Fact]
    public async Task Collect_WithDependencies_AllCollected()
    {
        // app -> db
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "db" },
            ["db"] = null,
        });

        var results = await ServiceGraph.CollectInDependencyOrderAsync<string>(
            project, (name, svc, ct) =>
            {
                return Task.FromResult($"processed:{name}");
            });

        Assert.Equal(2, results.Count);
        Assert.Equal("processed:app", results["app"]);
        Assert.Equal("processed:db", results["db"]);
    }

    #endregion

    #region BuildGraph (internal)

    [Fact]
    public void BuildGraph_CreatesCorrectEdges()
    {
        // web -> api -> db
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["web"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
        });

        var (graph, services) = ServiceGraph.BuildGraph(project, new TraversalOptions());

        Assert.Equal(3, graph.VertexCount);
        Assert.Equal(2, graph.EdgeCount);
        Assert.Equal(3, services.Count);

        // web depends on api => edge web -> api
        var webOutEdges = graph.OutEdges("web");
        Assert.Single(webOutEdges);
        Assert.Equal("api", webOutEdges[0].Target);
    }

    [Fact]
    public void BuildGraph_RootNodes_Normal_IncludesDependencies()
    {
        // app -> api -> db, worker -> cache
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
            ["worker"] = new() { "cache" },
            ["cache"] = null,
        });

        var (_, services) = ServiceGraph.BuildGraph(project,
            new TraversalOptions { RootNodes = new[] { "app" } });

        Assert.Equal(3, services.Count);
        Assert.Contains("app", services);
        Assert.Contains("api", services);
        Assert.Contains("db", services);
    }

    [Fact]
    public void BuildGraph_RootNodes_Reverse_IncludesDependents()
    {
        // app -> api -> db
        var project = CreateProject(new Dictionary<string, List<string>?>
        {
            ["app"] = new() { "api" },
            ["api"] = new() { "db" },
            ["db"] = null,
        });

        var (_, services) = ServiceGraph.BuildGraph(project,
            new TraversalOptions
            {
                RootNodes = new[] { "db" },
                ReverseOrder = true
            });

        Assert.Equal(3, services.Count);
        Assert.Contains("db", services);
        Assert.Contains("api", services);
        Assert.Contains("app", services);
    }

    #endregion
}
