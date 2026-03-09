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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Compose.NET.Errors;
using Compose.NET.Types;

namespace Compose.NET.Graph
{
    /// <summary>
    /// Options for controlling concurrent graph traversal.
    /// </summary>
    public class TraversalOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of concurrent operations.
        /// A value of 0 or less means unlimited concurrency.
        /// </summary>
        public int MaxConcurrency { get; set; }

        /// <summary>
        /// Gets or sets whether to traverse in reverse dependency order
        /// (dependents before dependencies, useful for shutdown operations).
        /// </summary>
        public bool ReverseOrder { get; set; }

        /// <summary>
        /// Gets or sets an optional set of root service names to start traversal from.
        /// When specified, only these services and their transitive dependencies
        /// (or dependents in reverse mode) are visited.
        /// When <c>null</c>, all services are visited.
        /// </summary>
        public IReadOnlyCollection<string>? RootNodes { get; set; }
    }

    /// <summary>
    /// Provides concurrent DAG (Directed Acyclic Graph) traversal for Docker Compose services.
    /// Mirrors compose-go's <c>graph</c> package which provides <c>InDependencyOrder</c>
    /// and <c>CollectInDependencyOrder</c> with channel-based concurrent execution.
    /// <para>
    /// Services are visited as soon as all their dependencies have completed,
    /// maximizing parallelism while respecting the dependency graph.
    /// </para>
    /// </summary>
    public static class ServiceGraph
    {
        /// <summary>
        /// Traverses services in dependency order, invoking <paramref name="action"/> for each
        /// service concurrently where possible. A service is only processed after all its
        /// dependencies have completed.
        /// </summary>
        /// <param name="project">The project whose services to traverse.</param>
        /// <param name="action">
        /// The async action to perform on each service. Receives the service name,
        /// the service configuration, and a cancellation token.
        /// </param>
        /// <param name="options">Optional traversal options.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when all services have been processed.</returns>
        /// <exception cref="ComposeInvalidException">The dependency graph contains a cycle.</exception>
        /// <exception cref="AggregateException">One or more service actions threw exceptions.</exception>
        public static async Task InDependencyOrderAsync(
            Project project,
            Func<string, ServiceConfig, CancellationToken, Task> action,
            TraversalOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await CollectInDependencyOrderAsync<object?>(
                project,
                async (name, svc, ct) =>
                {
                    await action(name, svc, ct).ConfigureAwait(false);
                    return null;
                },
                options,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Traverses services in dependency order, invoking <paramref name="func"/> for each
        /// service concurrently where possible, and collecting the results.
        /// </summary>
        /// <typeparam name="T">The type of result collected from each service.</typeparam>
        /// <param name="project">The project whose services to traverse.</param>
        /// <param name="func">
        /// The async function to invoke for each service. Receives the service name,
        /// the service configuration, and a cancellation token. Returns a result of type <typeparamref name="T"/>.
        /// </param>
        /// <param name="options">Optional traversal options.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>
        /// A dictionary mapping service name to the result of the function invocation,
        /// in the order they completed.
        /// </returns>
        /// <exception cref="ComposeInvalidException">The dependency graph contains a cycle.</exception>
        /// <exception cref="AggregateException">One or more service functions threw exceptions.</exception>
        public static async Task<Dictionary<string, T>> CollectInDependencyOrderAsync<T>(
            Project project,
            Func<string, ServiceConfig, CancellationToken, Task<T>> func,
            TraversalOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new TraversalOptions();

            // Build the dependency graph from the project
            var (graph, servicesToVisit) = BuildGraph(project, options);

            if (servicesToVisit.Count == 0)
                return new Dictionary<string, T>(StringComparer.Ordinal);

            // Check for cycles
            if (graph.HasCycle(out var cyclePath))
            {
                throw new ComposeInvalidException(
                    $"Dependency cycle detected: {string.Join(" -> ", cyclePath)}");
            }

            // Compute in-degrees for the subgraph
            var inDegrees = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var svc in servicesToVisit)
            {
                var count = 0;
                foreach (var edge in graph.InEdges(svc))
                {
                    if (servicesToVisit.Contains(edge.Source))
                        count++;
                }
                inDegrees[svc] = count;
            }

            // Concurrency control
            var semaphore = options.MaxConcurrency > 0
                ? new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency)
                : null;

            var results = new Dictionary<string, T>(StringComparer.Ordinal);
            var resultsLock = new object();
            var exceptions = new List<Exception>();
            var exceptionsLock = new object();

            // Track completion of each service
            var completionSources = new Dictionary<string, TaskCompletionSource<bool>>(StringComparer.Ordinal);
            foreach (var svc in servicesToVisit)
            {
                completionSources[svc] = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            // Track remaining in-degrees (thread-safe)
            var remainingDegrees = new Dictionary<string, int>(inDegrees, StringComparer.Ordinal);
            var degreeLock = new object();

            // Find initially ready services (in-degree == 0)
            var readyServices = new List<string>();
            foreach (var kvp in inDegrees)
            {
                if (kvp.Value == 0)
                    readyServices.Add(kvp.Key);
            }
            readyServices.Sort(StringComparer.Ordinal); // deterministic

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = linkedCts.Token;
            using var cancellationRegistration = ct.Register(() =>
            {
                foreach (var completion in completionSources.Values)
                {
                    completion.TrySetCanceled(ct);
                }
            });

            // Process a single service
            async Task ProcessService(string serviceName)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    // Wait for semaphore if concurrency is limited
                    if (semaphore != null)
                    {
                        await semaphore.WaitAsync(ct).ConfigureAwait(false);
                    }

                    try
                    {
                        var svcConfig = project.Services[serviceName];
                        var result = await func(serviceName, svcConfig, ct).ConfigureAwait(false);

                        lock (resultsLock)
                        {
                            results[serviceName] = result;
                        }
                    }
                    finally
                    {
                        semaphore?.Release();
                    }

                    // Mark this service as complete
                    completionSources[serviceName].TrySetResult(true);

                    // Notify dependents
                    var dependents = options.ReverseOrder
                        ? graph.Predecessors(serviceName)
                        : graph.Successors(serviceName);

                    // In reverse mode: edges go from A->B (A depends on B),
                    // so successors of A are B's dependencies.
                    // We want to process dependents (parents) after children.
                    // Actually let me reconsider the graph direction...

                    // Graph direction: edges are source -> target where source depends_on target.
                    // Normal order: process targets (dependencies) first, then sources.
                    //   => After completing a vertex, notify vertices that depend on it (predecessors in edge direction)
                    //   Wait: No. Edge is "A depends on B" = A -> B.
                    //   In normal (dependency-first) order: B runs first, then A.
                    //   After B completes, A might become ready.
                    //   A's in-edges point from A to B. So B's *reverse* adjacency (predecessors of B) = vertices pointing to B = A.
                    //   Wait, that's wrong. Let me think again.
                    //   Edge A -> B means "A depends on B". B is the target.
                    //   In-edges of A = edges coming into A = edges where A is the target. These would be edges like "C -> A".
                    //   Actually, in our graph: AddEdge(source, target) where source depends_on target.
                    //   edge.Source = A, edge.Target = B means A depends on B.
                    //   Predecessors(A) = InEdges(A).Source = vertices that depend on A... wait no.
                    //   InEdges(A) = edges where A is the target, so these are edges X -> A meaning X depends on A.
                    //   Predecessors(A) = {X : X depends on A}
                    //   Successors(A) = OutEdges(A).Target = {B : A depends on B}
                    //
                    // Normal order (dependency-first): Process B before A (B has no deps, A depends on B)
                    //   inDegrees = number of dependencies = OutDegree (A has outDegree 1 because A->B)
                    //   Wait, I used InEdges above for inDegrees, but that measures how many things depend ON this service.
                    //   That's wrong for normal order!
                    //
                    // Let me fix: For normal (dependency-first) order:
                    //   We need to process nodes whose all dependencies are done.
                    //   A service's dependencies = Successors(service) = targets of out-edges.
                    //   "In-degree" for scheduling = number of unfinished dependencies = OutDegree in our edge direction.
                    //   After a dependency B completes, reduce the scheduling-degree of each service that depends on B.
                    //   Services depending on B = Predecessors(B) = InEdges(B).Source
                    //
                    // For reverse order:
                    //   Process A before B (dependents before dependencies).
                    //   A service can be processed when all its dependents are done.
                    //   Dependents of B = Predecessors(B).
                    //   "In-degree" for scheduling = InDegree in our edge direction.
                    //   After A completes, reduce the scheduling-degree of each dependency of A.
                    //   Dependencies of A = Successors(A) = OutEdges(A).Target

                    // I need to fix the degree computation above. Let me collect the notifyTargets.
                    List<string> notifyTargets;
                    if (!options.ReverseOrder)
                    {
                        // Normal: after completing B, notify services that depend on B
                        notifyTargets = new List<string>();
                        foreach (var edge in graph.InEdges(serviceName))
                        {
                            if (servicesToVisit.Contains(edge.Source))
                                notifyTargets.Add(edge.Source);
                        }
                    }
                    else
                    {
                        // Reverse: after completing A, notify A's dependencies
                        notifyTargets = new List<string>();
                        foreach (var edge in graph.OutEdges(serviceName))
                        {
                            if (servicesToVisit.Contains(edge.Target))
                                notifyTargets.Add(edge.Target);
                        }
                    }

                    foreach (var target in notifyTargets)
                    {
                        bool isReady;
                        lock (degreeLock)
                        {
                            remainingDegrees[target]--;
                            isReady = remainingDegrees[target] == 0;
                        }

                        if (isReady)
                        {
                            // Launch processing of the newly ready service
                            _ = Task.Run(() => ProcessService(target), ct);
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    completionSources[serviceName].TrySetCanceled(ct);
                }
                catch (Exception ex)
                {
                    lock (exceptionsLock)
                    {
                        exceptions.Add(ex);
                    }
                    linkedCts.Cancel();
                    completionSources[serviceName].TrySetException(ex);
                }
            }

            // Fix: Recompute inDegrees based on actual scheduling direction
            inDegrees.Clear();
            remainingDegrees.Clear();

            if (!options.ReverseOrder)
            {
                // Normal order: a service is ready when all its dependencies (Successors) are done
                foreach (var svc in servicesToVisit)
                {
                    var count = 0;
                    foreach (var edge in graph.OutEdges(svc))
                    {
                        if (servicesToVisit.Contains(edge.Target))
                            count++;
                    }
                    inDegrees[svc] = count;
                    remainingDegrees[svc] = count;
                }
            }
            else
            {
                // Reverse order: a service is ready when all its dependents (Predecessors) are done
                foreach (var svc in servicesToVisit)
                {
                    var count = 0;
                    foreach (var edge in graph.InEdges(svc))
                    {
                        if (servicesToVisit.Contains(edge.Source))
                            count++;
                    }
                    inDegrees[svc] = count;
                    remainingDegrees[svc] = count;
                }
            }

            // Recompute ready services
            readyServices.Clear();
            foreach (var kvp in inDegrees)
            {
                if (kvp.Value == 0)
                    readyServices.Add(kvp.Key);
            }
            readyServices.Sort(StringComparer.Ordinal);

            // Launch all initially ready services
            var tasks = new List<Task>();
            foreach (var svc in readyServices)
            {
                tasks.Add(Task.Run(() => ProcessService(svc), ct));
            }

            // Wait for all services to complete
            try
            {
                var allCompletionTasks = new List<Task>();
                foreach (var tcs in completionSources.Values)
                {
                    allCompletionTasks.Add(tcs.Task);
                }
                await Task.WhenAll(allCompletionTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Errors are collected in the exceptions list
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(
                    "One or more errors occurred during service graph traversal.",
                    exceptions);
            }

            return results;
        }

        /// <summary>
        /// Synchronous version of <see cref="InDependencyOrderAsync"/> for simpler use cases.
        /// </summary>
        /// <param name="project">The project whose services to traverse.</param>
        /// <param name="action">The action to perform on each service.</param>
        /// <param name="options">Optional traversal options.</param>
        public static void InDependencyOrder(
            Project project,
            Action<string, ServiceConfig> action,
            TraversalOptions? options = null)
        {
            options ??= new TraversalOptions();

            var (graph, servicesToVisit) = BuildGraph(project, options);

            if (servicesToVisit.Count == 0)
                return;

            if (graph.HasCycle(out var cyclePath))
            {
                throw new ComposeInvalidException(
                    $"Dependency cycle detected: {string.Join(" -> ", cyclePath)}");
            }

            // Get topological order
            var order = options.ReverseOrder
                ? graph.ReverseTopologicalSort()
                : graph.TopologicalSort();

            // Filter to only services we want to visit
            foreach (var key in order)
            {
                if (servicesToVisit.Contains(key))
                {
                    action(key, project.Services[key]);
                }
            }
        }

        /// <summary>
        /// Builds a <see cref="DependencyGraph{T}"/> from the project's services and their
        /// <c>depends_on</c> relationships. Also computes the set of services to visit
        /// based on <see cref="TraversalOptions.RootNodes"/>.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="options">Traversal options.</param>
        /// <returns>
        /// A tuple of the graph and the set of service names to visit.
        /// </returns>
        internal static (DependencyGraph<ServiceConfig> Graph, HashSet<string> ServicesToVisit)
            BuildGraph(Project project, TraversalOptions options)
        {
            var graph = new DependencyGraph<ServiceConfig>();

            // Add all services as vertices
            foreach (var kvp in project.Services)
            {
                graph.AddVertex(kvp.Key, kvp.Value);
            }

            // Add edges for depends_on relationships
            // Edge direction: A depends_on B => AddEdge(A, B)
            foreach (var kvp in project.Services)
            {
                if (kvp.Value.DependsOn != null)
                {
                    foreach (var dep in kvp.Value.DependsOn)
                    {
                        if (graph.ContainsVertex(dep.Key))
                        {
                            graph.AddEdge(kvp.Key, dep.Key);
                        }
                    }
                }
            }

            // Determine which services to visit
            HashSet<string> servicesToVisit;

            if (options.RootNodes != null && options.RootNodes.Count > 0)
            {
                servicesToVisit = new HashSet<string>(StringComparer.Ordinal);

                foreach (var root in options.RootNodes)
                {
                    if (!project.Services.ContainsKey(root))
                    {
                        throw new ComposeNotFoundException(
                            $"Service '{root}' not found in project.");
                    }
                    servicesToVisit.Add(root);

                    if (!options.ReverseOrder)
                    {
                        // Normal: include all transitive dependencies (Successors direction)
                        var deps = graph.TransitiveDependencies(root);
                        foreach (var dep in deps)
                        {
                            servicesToVisit.Add(dep);
                        }
                    }
                    else
                    {
                        // Reverse: include all transitive dependents
                        // (services that transitively depend on root)
                        var dependents = GetTransitiveDependents(graph, root);
                        foreach (var dep in dependents)
                        {
                            servicesToVisit.Add(dep);
                        }
                    }
                }
            }
            else
            {
                servicesToVisit = new HashSet<string>(
                    project.Services.Keys, StringComparer.Ordinal);
            }

            return (graph, servicesToVisit);
        }

        /// <summary>
        /// Gets all vertices that transitively depend on the given vertex
        /// (traverses reverse edges / InEdges).
        /// </summary>
        private static HashSet<string> GetTransitiveDependents(
            DependencyGraph<ServiceConfig> graph, string key)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();

            foreach (var edge in graph.InEdges(key))
            {
                stack.Push(edge.Source);
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!result.Add(current))
                    continue;

                foreach (var edge in graph.InEdges(current))
                {
                    if (!result.Contains(edge.Source))
                    {
                        stack.Push(edge.Source);
                    }
                }
            }

            return result;
        }
    }
}
