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

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Compose.NET.Types;

/// <summary>
/// A mapping type that can be converted from a list of key[=value] strings.
/// For a key with an empty value (<c>key=</c>) or a key without a value (<c>key</c>),
/// the mapped value is set to an empty string <c>""</c>.
/// </summary>
public class Mapping : IDictionary<string, string>, IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _inner;

    /// <summary>
    /// Initializes a new empty <see cref="Mapping"/>.
    /// </summary>
    public Mapping()
    {
        _inner = new Dictionary<string, string>();
    }

    /// <summary>
    /// Initializes a new <see cref="Mapping"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity.</param>
    public Mapping(int capacity)
    {
        _inner = new Dictionary<string, string>(capacity);
    }

    /// <summary>
    /// Initializes a new <see cref="Mapping"/> from an existing dictionary.
    /// </summary>
    /// <param name="dictionary">The source dictionary to copy entries from.</param>
    public Mapping(IDictionary<string, string> dictionary)
    {
        _inner = new Dictionary<string, string>(dictionary);
    }

    /// <summary>
    /// Initializes a new <see cref="Mapping"/> from an enumerable of key-value pairs.
    /// </summary>
    /// <param name="collection">The source collection of key-value pairs.</param>
    public Mapping(IEnumerable<KeyValuePair<string, string>> collection)
    {
        _inner = new Dictionary<string, string>();
        foreach (var kvp in collection)
        {
            _inner[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Builds a new <see cref="Mapping"/> from a set of <c>KEY=VALUE</c> strings.
    /// Keys without a value (no <c>=</c>) are mapped to an empty string.
    /// </summary>
    /// <param name="values">The collection of <c>KEY=VALUE</c> strings to parse.</param>
    /// <returns>A new <see cref="Mapping"/> instance.</returns>
    public static Mapping NewMapping(IEnumerable<string> values)
    {
        var mapping = new Mapping();
        foreach (var value in values)
        {
            var eqIndex = value.IndexOf('=');
            if (eqIndex < 0)
            {
                mapping._inner[value] = "";
            }
            else
            {
                var key = value.Substring(0, eqIndex);
                var val = value.Substring(eqIndex + 1);
                mapping._inner[key] = val;
            }
        }
        return mapping;
    }

    /// <summary>
    /// Converts values into a sorted list of <c>KEY=VALUE</c> strings.
    /// </summary>
    /// <returns>A sorted list of <c>KEY=VALUE</c> formatted strings.</returns>
    public List<string> ToValues()
    {
        var values = new List<string>(_inner.Count);
        foreach (var kvp in _inner)
        {
            values.Add($"{kvp.Key}={kvp.Value}");
        }
        values.Sort(StringComparer.Ordinal);
        return values;
    }

    /// <summary>
    /// Converts this <see cref="Mapping"/> into a <see cref="MappingWithEquals"/>
    /// where each value is non-null.
    /// </summary>
    /// <returns>A new <see cref="MappingWithEquals"/> instance.</returns>
    public MappingWithEquals ToMappingWithEquals()
    {
        var mapping = new MappingWithEquals();
        foreach (var kvp in _inner)
        {
            mapping[kvp.Key] = kvp.Value;
        }
        return mapping;
    }

    /// <summary>
    /// Resolves a key, returning its value and whether the key was found.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>A tuple of (value, found). If not found, value is an empty string.</returns>
    public (string Value, bool Found) Resolve(string key)
    {
        if (_inner.TryGetValue(key, out var value))
        {
            return (value, true);
        }
        return ("", false);
    }

    /// <summary>
    /// Creates a shallow clone of this <see cref="Mapping"/>.
    /// </summary>
    /// <returns>A new <see cref="Mapping"/> with the same key-value pairs.</returns>
    public Mapping Clone()
    {
        return new Mapping(_inner);
    }

    /// <summary>
    /// Adds all values from <paramref name="other"/> which are not already defined
    /// in this mapping.
    /// </summary>
    /// <param name="other">The mapping to merge from.</param>
    /// <returns>This mapping instance for chaining.</returns>
    public Mapping Merge(Mapping other)
    {
        foreach (var kvp in other._inner)
        {
            if (!_inner.ContainsKey(kvp.Key))
            {
                _inner[kvp.Key] = kvp.Value;
            }
        }
        return this;
    }

    #region IDictionary<string, string> implementation

    /// <inheritdoc />
    public string this[string key]
    {
        get => _inner[key];
        set => _inner[key] = value;
    }

    /// <inheritdoc />
    public ICollection<string> Keys => _inner.Keys;

    /// <inheritdoc />
    ICollection<string> IDictionary<string, string>.Values => _inner.Values;

    /// <inheritdoc />
    public int Count => _inner.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => _inner.Keys;

    /// <inheritdoc />
    IEnumerable<string> IReadOnlyDictionary<string, string>.Values => _inner.Values;

    /// <inheritdoc />
    public void Add(string key, string value) => _inner.Add(key, value);

    /// <inheritdoc />
    public void Add(KeyValuePair<string, string> item) => ((ICollection<KeyValuePair<string, string>>)_inner).Add(item);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public bool Contains(KeyValuePair<string, string> item) => ((ICollection<KeyValuePair<string, string>>)_inner).Contains(item);

    /// <inheritdoc />
    public bool ContainsKey(string key) => _inner.ContainsKey(key);

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<string, string>>)_inner).CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc />
    public bool Remove(string key) => _inner.Remove(key);

    /// <inheritdoc />
    public bool Remove(KeyValuePair<string, string> item) => ((ICollection<KeyValuePair<string, string>>)_inner).Remove(item);

#if NETSTANDARD2_1
    /// <inheritdoc />
    public bool TryGetValue(string key, out string value)
    {
        if (_inner.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }
        value = default!;
        return false;
    }
#else
    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => _inner.TryGetValue(key, out value);
#endif

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    #endregion
}

/// <summary>
/// A mapping type that can be converted from a list of <c>key[=value]</c> strings.
/// <para>
/// For a key with a value (<c>key=value</c>), the mapped value is <c>"value"</c>.
/// For a key with an empty value (<c>key=</c>), the mapped value is <c>""</c>.
/// For a key without a value (<c>key</c>), the mapped value is <see langword="null"/>.
/// </para>
/// </summary>
public class MappingWithEquals : IDictionary<string, string?>, IReadOnlyDictionary<string, string?>
{
    private readonly Dictionary<string, string?> _inner;

    /// <summary>
    /// Initializes a new empty <see cref="MappingWithEquals"/>.
    /// </summary>
    public MappingWithEquals()
    {
        _inner = new Dictionary<string, string?>();
    }

    /// <summary>
    /// Initializes a new <see cref="MappingWithEquals"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity.</param>
    public MappingWithEquals(int capacity)
    {
        _inner = new Dictionary<string, string?>(capacity);
    }

    /// <summary>
    /// Initializes a new <see cref="MappingWithEquals"/> from an existing dictionary.
    /// </summary>
    /// <param name="dictionary">The source dictionary to copy entries from.</param>
    public MappingWithEquals(IDictionary<string, string?> dictionary)
    {
        _inner = new Dictionary<string, string?>(dictionary);
    }

    /// <summary>
    /// Builds a new <see cref="MappingWithEquals"/> from a set of <c>KEY[=VALUE]</c> strings.
    /// Keys without <c>=</c> are mapped to <see langword="null"/>.
    /// Keys with <c>=</c> but no value are mapped to <c>""</c>.
    /// </summary>
    /// <param name="values">The collection of <c>KEY[=VALUE]</c> strings to parse.</param>
    /// <returns>A new <see cref="MappingWithEquals"/> instance.</returns>
    public static MappingWithEquals NewMappingWithEquals(IEnumerable<string> values)
    {
        var mapping = new MappingWithEquals();
        foreach (var env in values)
        {
            var eqIndex = env.IndexOf('=');
            if (eqIndex < 0)
            {
                mapping._inner[env] = null;
            }
            else
            {
                var key = env.Substring(0, eqIndex);
                var val = env.Substring(eqIndex + 1);
                mapping._inner[key] = val;
            }
        }
        return mapping;
    }

    /// <summary>
    /// Updates this mapping with values from <paramref name="other"/>,
    /// replacing any existing values.
    /// </summary>
    /// <param name="other">The mapping whose values take precedence.</param>
    /// <returns>This mapping instance for chaining.</returns>
    public MappingWithEquals OverrideBy(MappingWithEquals other)
    {
        foreach (var kvp in other._inner)
        {
            _inner[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Resolves keys that have a <see langword="null"/> value (i.e., keys declared
    /// without <c>=</c>) by calling <paramref name="lookupFn"/>.
    /// Keys with non-null values (including empty strings) are left unchanged.
    /// </summary>
    /// <param name="lookupFn">
    /// A function that takes a key and returns a tuple of (value, found).
    /// If found is <see langword="true"/>, the key's value is set to the returned value.
    /// </param>
    /// <returns>This mapping instance for chaining.</returns>
    public MappingWithEquals Resolve(Func<string, (string Value, bool Found)> lookupFn)
    {
        foreach (var key in new List<string>(_inner.Keys))
        {
            if (_inner[key] == null)
            {
                var (value, found) = lookupFn(key);
                if (found)
                {
                    _inner[key] = value;
                }
            }
        }
        return this;
    }

    /// <summary>
    /// Removes keys that are not associated with a value (i.e., keys whose value
    /// is <see langword="null"/>).
    /// </summary>
    /// <returns>This mapping instance for chaining.</returns>
    public MappingWithEquals RemoveEmpty()
    {
        var keysToRemove = new List<string>();
        foreach (var kvp in _inner)
        {
            if (kvp.Value == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            _inner.Remove(key);
        }
        return this;
    }

    /// <summary>
    /// Converts this <see cref="MappingWithEquals"/> to a <see cref="Mapping"/>,
    /// dropping any keys whose value is <see langword="null"/>.
    /// </summary>
    /// <returns>A new <see cref="Mapping"/> containing only entries with non-null values.</returns>
    public Mapping ToMapping()
    {
        var mapping = new Mapping();
        foreach (var kvp in _inner)
        {
            if (kvp.Value != null)
            {
                mapping[kvp.Key] = kvp.Value;
            }
        }
        return mapping;
    }

    #region IDictionary<string, string?> implementation

    /// <inheritdoc />
    public string? this[string key]
    {
        get => _inner[key];
        set => _inner[key] = value;
    }

    /// <inheritdoc />
    public ICollection<string> Keys => _inner.Keys;

    /// <inheritdoc />
    public ICollection<string?> Values => _inner.Values;

    /// <inheritdoc />
    public int Count => _inner.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    IEnumerable<string> IReadOnlyDictionary<string, string?>.Keys => _inner.Keys;

    /// <inheritdoc />
    IEnumerable<string?> IReadOnlyDictionary<string, string?>.Values => _inner.Values;

    /// <inheritdoc />
    public void Add(string key, string? value) => _inner.Add(key, value);

    /// <inheritdoc />
    public void Add(KeyValuePair<string, string?> item) => ((ICollection<KeyValuePair<string, string?>>)_inner).Add(item);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public bool Contains(KeyValuePair<string, string?> item) => ((ICollection<KeyValuePair<string, string?>>)_inner).Contains(item);

    /// <inheritdoc />
    public bool ContainsKey(string key) => _inner.ContainsKey(key);

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<string, string?>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<string, string?>>)_inner).CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, string?>> GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc />
    public bool Remove(string key) => _inner.Remove(key);

    /// <inheritdoc />
    public bool Remove(KeyValuePair<string, string?> item) => ((ICollection<KeyValuePair<string, string?>>)_inner).Remove(item);

#if NETSTANDARD2_1
    /// <inheritdoc />
    public bool TryGetValue(string key, out string? value) => _inner.TryGetValue(key, out value);
#else
    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string? value) => _inner.TryGetValue(key, out value);
#endif

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    #endregion
}
