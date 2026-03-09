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

using Compose.NET.Errors;

namespace Compose.NET.Graph;

/// <summary>
/// Represents a directed edge in a <see cref="DependencyGraph{T}"/>.
/// </summary>
/// <typeparam name="T">The type of data stored in each vertex.</typeparam>
internal class Edge<T>
{
    /// <summary>
    /// Gets the source vertex key.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the target vertex key.
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Gets optional metadata associated with this edge.
    /// </summary>
    public Dictionary<string, string> Metadata { get; }

    /// <summary>
    /// Initializes a new <see cref="Edge{T}"/>.
    /// </summary>
    /// <param name="source">The source vertex key.</param>
    /// <param name="target">The target vertex key.</param>
    /// <param name="metadata">Optional metadata for the edge.</param>
    public Edge(string source, string target, Dictionary<string, string>? metadata = null)
    {
        Source = source;
        Target = target;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    /// <inheritdoc />
    public override string ToString() => $"{Source} -> {Target}";
}

/// <summary>
/// Represents a vertex in a <see cref="DependencyGraph{T}"/>.
/// </summary>
/// <typeparam name="T">The type of data stored in the vertex.</typeparam>
internal class Vertex<T>
{
    /// <summary>
    /// Gets the unique key identifying this vertex.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets or sets the data associated with this vertex.
    /// </summary>
    public T Data { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Vertex{T}"/>.
    /// </summary>
    /// <param name="key">The unique vertex key.</param>
    /// <param name="data">The data associated with the vertex.</param>
    public Vertex(string key, T data)
    {
        Key = key;
        Data = data;
    }

    /// <inheritdoc />
    public override string ToString() => Key;
}

/// <summary>
/// A generic directed graph data structure for tracking dependencies.
/// Mirrors the compose-go/graph package, providing topological sort,
/// cycle detection, and in-degree / out-degree queries.
/// </summary>
/// <typeparam name="T">The type of data stored in each vertex.</typeparam>
internal class DependencyGraph<T>
{
    private readonly Dictionary<string, Vertex<T>> _vertices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Edge<T>>> _adjacency = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Edge<T>>> _reverseAdjacency = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the number of vertices in the graph.
    /// </summary>
    public int VertexCount => _vertices.Count;

    /// <summary>
    /// Gets the total number of edges in the graph.
    /// </summary>
    public int EdgeCount
    {
        get
        {
            var count = 0;
            foreach (var edges in _adjacency.Values)
            {
                count += edges.Count;
            }
            return count;
        }
    }

    /// <summary>
    /// Gets all vertices in the graph.
    /// </summary>
    /// <returns>A read-only collection of all vertices.</returns>
    public IReadOnlyCollection<Vertex<T>> Vertices => _vertices.Values;

    /// <summary>
    /// Gets all edges in the graph.
    /// </summary>
    /// <returns>An enumerable of all edges.</returns>
    public IEnumerable<Edge<T>> Edges
    {
        get
        {
            foreach (var edges in _adjacency.Values)
            {
                foreach (var edge in edges)
                {
                    yield return edge;
                }
            }
        }
    }

    /// <summary>
    /// Adds a vertex to the graph. If a vertex with the same key already exists,
    /// its data is updated.
    /// </summary>
    /// <param name="key">The unique vertex key.</param>
    /// <param name="data">The data to associate with the vertex.</param>
    /// <returns>The created or updated <see cref="Vertex{T}"/>.</returns>
    public Vertex<T> AddVertex(string key, T data)
    {
        if (_vertices.TryGetValue(key, out var existing))
        {
            existing.Data = data;
            return existing;
        }

        var vertex = new Vertex<T>(key, data);
        _vertices[key] = vertex;
        _adjacency[key] = new List<Edge<T>>();
        _reverseAdjacency[key] = new List<Edge<T>>();
        return vertex;
    }

    /// <summary>
    /// Gets a vertex by key.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <returns>The vertex.</returns>
    /// <exception cref="KeyNotFoundException">No vertex with the given key exists.</exception>
    public Vertex<T> GetVertex(string key)
    {
        if (_vertices.TryGetValue(key, out var vertex))
            return vertex;

        throw new ComposeNotFoundException($"Vertex '{key}' not found in graph.");
    }

    /// <summary>
    /// Tries to get a vertex by key.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <param name="vertex">The vertex if found; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if the vertex was found; otherwise <c>false</c>.</returns>
    public bool TryGetVertex(string key, out Vertex<T> vertex)
    {
        return _vertices.TryGetValue(key, out vertex!);
    }

    /// <summary>
    /// Returns <c>true</c> if the graph contains a vertex with the given key.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <returns><c>true</c> if the vertex exists.</returns>
    public bool ContainsVertex(string key) => _vertices.ContainsKey(key);

    /// <summary>
    /// Adds a directed edge from <paramref name="source"/> to <paramref name="target"/>.
    /// Both vertices must already exist in the graph.
    /// </summary>
    /// <param name="source">The source vertex key.</param>
    /// <param name="target">The target vertex key.</param>
    /// <param name="metadata">Optional metadata for the edge.</param>
    /// <returns>The created <see cref="Edge{T}"/>.</returns>
    /// <exception cref="KeyNotFoundException">Source or target vertex does not exist.</exception>
    public Edge<T> AddEdge(string source, string target, Dictionary<string, string>? metadata = null)
    {
        if (!_vertices.ContainsKey(source))
            throw new ComposeNotFoundException($"Source vertex '{source}' not found in graph.");
        if (!_vertices.ContainsKey(target))
            throw new ComposeNotFoundException($"Target vertex '{target}' not found in graph.");

        var edge = new Edge<T>(source, target, metadata);
        _adjacency[source].Add(edge);
        _reverseAdjacency[target].Add(edge);
        return edge;
    }

    /// <summary>
    /// Returns the outgoing edges from the given vertex.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <returns>A read-only list of outgoing edges.</returns>
    /// <exception cref="KeyNotFoundException">The vertex does not exist.</exception>
    public IReadOnlyList<Edge<T>> OutEdges(string key)
    {
        if (_adjacency.TryGetValue(key, out var edges))
            return edges;

        throw new ComposeNotFoundException($"Vertex '{key}' not found in graph.");
    }

    /// <summary>
    /// Returns the incoming edges to the given vertex.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <returns>A read-only list of incoming edges.</returns>
    /// <exception cref="KeyNotFoundException">The vertex does not exist.</exception>
    public IReadOnlyList<Edge<T>> InEdges(string key)
    {
        if (_reverseAdjacency.TryGetValue(key, out var edges))
            return edges;

        throw new ComposeNotFoundException($"Vertex '{key}' not found in graph.");
    }

    /// <summary>
    /// Returns the out-degree (number of outgoing edges) of the given vertex.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <returns>The out-degree.</returns>
    /// <exception cref="KeyNotFoundException">The vertex does not exist.</exception>
    public int OutDegree(string key) => OutEdges(key).Count;

    /// <summary>
    /// Returns the in-degree (number of incoming edges) of the given vertex.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <returns>The in-degree.</returns>
    /// <exception cref="KeyNotFoundException">The vertex does not exist.</exception>
    public int InDegree(string key) => InEdges(key).Count;

    /// <summary>
    /// Returns the direct successors (children) of the given vertex.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <returns>A list of successor vertex keys.</returns>
    public List<string> Successors(string key)
    {
        var result = new List<string>();
        foreach (var edge in OutEdges(key))
        {
            result.Add(edge.Target);
        }
        return result;
    }

    /// <summary>
    /// Returns the direct predecessors (parents) of the given vertex.
    /// </summary>
    /// <param name="key">The vertex key.</param>
    /// <returns>A list of predecessor vertex keys.</returns>
    public List<string> Predecessors(string key)
    {
        var result = new List<string>();
        foreach (var edge in InEdges(key))
        {
            result.Add(edge.Source);
        }
        return result;
    }

    /// <summary>
    /// Determines whether the graph contains a cycle.
    /// </summary>
    /// <returns><c>true</c> if the graph has at least one cycle; otherwise <c>false</c>.</returns>
    public bool HasCycle()
    {
        return HasCycle(out _);
    }

    /// <summary>
    /// Determines whether the graph contains a cycle, and if so, returns the
    /// cycle path as a list of vertex keys.
    /// </summary>
    /// <param name="cyclePath">
    /// When a cycle is found, contains the vertex keys forming the cycle
    /// (the last element connects back to the first). Empty if no cycle exists.
    /// </param>
    /// <returns><c>true</c> if the graph has at least one cycle; otherwise <c>false</c>.</returns>
    public bool HasCycle(out List<string> cyclePath)
    {
        cyclePath = new List<string>();

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();

        foreach (var key in _vertices.Keys)
        {
            if (visited.Contains(key))
                continue;

            if (DetectCycleDfs(key, visited, visiting, path, out cyclePath))
                return true;
        }

        cyclePath = new List<string>();
        return false;
    }

    /// <summary>
    /// Returns the vertices in topological order using DFS-based sorting.
    /// Dependencies (targets of outgoing edges) appear before their dependents
    /// in the resulting list.
    /// </summary>
    /// <returns>A list of vertex keys in topological (dependency-first) order.</returns>
    /// <exception cref="InvalidOperationException">The graph contains a cycle.</exception>
    public List<string> TopologicalSort()
    {
        if (HasCycle(out var cyclePath))
        {
            var cycleDescription = string.Join(" -> ", cyclePath);
            throw new ComposeInvalidException(
                $"Cannot perform topological sort: dependency cycle detected: {cycleDescription}");
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();

        // Sort keys for deterministic output.
        var keys = new List<string>(_vertices.Keys);
        keys.Sort(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            TopologicalVisit(key, visited, order);
        }

        return order;
    }

    /// <summary>
    /// Returns the vertices in reverse topological order.
    /// Dependents appear before their dependencies.
    /// </summary>
    /// <returns>A list of vertex keys in reverse topological order.</returns>
    /// <exception cref="InvalidOperationException">The graph contains a cycle.</exception>
    public List<string> ReverseTopologicalSort()
    {
        var order = TopologicalSort();
        order.Reverse();
        return order;
    }

    /// <summary>
    /// Returns the vertices in topological order using Kahn's algorithm
    /// (BFS-based). Dependencies appear before their dependents.
    /// </summary>
    /// <returns>A list of vertex keys in topological order.</returns>
    /// <exception cref="InvalidOperationException">The graph contains a cycle.</exception>
    public List<string> TopologicalSortKahn()
    {
        // Compute in-degrees.
        var inDegrees = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in _vertices.Keys)
        {
            inDegrees[key] = _reverseAdjacency[key].Count;
        }

        // Initialize with vertices that have no incoming edges.
        // Use a sorted set for deterministic order among vertices with equal priority.
        var queue = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var kvp in inDegrees)
        {
            if (kvp.Value == 0)
            {
                queue.Add(kvp.Key);
            }
        }

        var order = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Min!;
            queue.Remove(current);
            order.Add(current);

            foreach (var edge in _adjacency[current])
            {
                inDegrees[edge.Target]--;
                if (inDegrees[edge.Target] == 0)
                {
                    queue.Add(edge.Target);
                }
            }
        }

        if (order.Count != _vertices.Count)
        {
            // Find the cycle for a meaningful error message.
            var remaining = new List<string>();
            foreach (var kvp in inDegrees)
            {
                if (kvp.Value > 0)
                    remaining.Add(kvp.Key);
            }
            remaining.Sort(StringComparer.Ordinal);

            throw new ComposeInvalidException(
                $"Cannot perform topological sort: dependency cycle detected involving vertices: {string.Join(", ", remaining)}");
        }

        return order;
    }

    /// <summary>
    /// Iterates vertices in topological (dependency-first) order, calling
    /// <paramref name="action"/> for each vertex.
    /// </summary>
    /// <param name="action">The action to invoke for each vertex.</param>
    /// <exception cref="InvalidOperationException">The graph contains a cycle.</exception>
    public void ForEachInDependencyOrder(Action<string, T> action)
    {
        var order = TopologicalSort();
        foreach (var key in order)
        {
            action(key, _vertices[key].Data);
        }
    }

    /// <summary>
    /// Returns all vertices reachable from the given vertex (transitive closure
    /// of outgoing edges).
    /// </summary>
    /// <param name="key">The starting vertex key.</param>
    /// <returns>A set of vertex keys reachable from the given vertex (excluding itself unless there is a self-loop).</returns>
    /// <exception cref="KeyNotFoundException">The vertex does not exist.</exception>
    public HashSet<string> TransitiveDependencies(string key)
    {
        if (!_vertices.ContainsKey(key))
        throw new ComposeNotFoundException($"Vertex '{key}' not found in graph.");

        var result = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();

        foreach (var edge in _adjacency[key])
        {
            stack.Push(edge.Target);
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current))
                continue;

            foreach (var edge in _adjacency[current])
            {
                if (!result.Contains(edge.Target))
                {
                    stack.Push(edge.Target);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns all leaf vertices (vertices with no outgoing edges).
    /// </summary>
    /// <returns>A list of leaf vertex keys.</returns>
    public List<string> Leaves()
    {
        var result = new List<string>();
        foreach (var kvp in _adjacency)
        {
            if (kvp.Value.Count == 0)
            {
                result.Add(kvp.Key);
            }
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    /// Returns all root vertices (vertices with no incoming edges).
    /// </summary>
    /// <returns>A list of root vertex keys.</returns>
    public List<string> Roots()
    {
        var result = new List<string>();
        foreach (var kvp in _reverseAdjacency)
        {
            if (kvp.Value.Count == 0)
            {
                result.Add(kvp.Key);
            }
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    #region Private helpers

    /// <summary>
    /// DFS helper for cycle detection. Tracks the current path to report the
    /// full cycle when found.
    /// </summary>
    private bool DetectCycleDfs(
        string key,
        HashSet<string> visited,
        HashSet<string> visiting,
        List<string> path,
        out List<string> cyclePath)
    {
        cyclePath = new List<string>();

        if (visited.Contains(key))
            return false;

        if (visiting.Contains(key))
        {
            // Build cycle path from the point where the cycle starts.
            var cycleStart = path.IndexOf(key);
            if (cycleStart >= 0)
            {
                for (var i = cycleStart; i < path.Count; i++)
                {
                    cyclePath.Add(path[i]);
                }
                cyclePath.Add(key); // Close the cycle.
            }
            else
            {
                cyclePath.Add(key);
            }
            return true;
        }

        visiting.Add(key);
        path.Add(key);

        foreach (var edge in _adjacency[key])
        {
            if (DetectCycleDfs(edge.Target, visited, visiting, path, out cyclePath))
                return true;
        }

        path.RemoveAt(path.Count - 1);
        visiting.Remove(key);
        visited.Add(key);
        return false;
    }

    /// <summary>
    /// DFS helper for topological sorting. Dependencies are added to the order
    /// list before their dependents.
    /// </summary>
    private void TopologicalVisit(string key, HashSet<string> visited, List<string> order)
    {
        if (visited.Contains(key))
            return;

        visited.Add(key);

        // Sort neighbor keys for deterministic output.
        var neighbors = new List<string>();
        foreach (var edge in _adjacency[key])
        {
            neighbors.Add(edge.Target);
        }
        neighbors.Sort(StringComparer.Ordinal);

        foreach (var neighbor in neighbors)
        {
            TopologicalVisit(neighbor, visited, order);
        }

        order.Add(key);
    }

    #endregion
}
