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
using System.Globalization;

namespace Compose.NET.Types;

/// <summary>
/// Stores <c>x-*</c> custom extension fields found in Compose files.
/// Wraps a <see cref="Dictionary{TKey,TValue}"/> of <c>string</c> â†’ <c>object</c>.
/// </summary>
public class Extensions : IDictionary<string, object>, IEquatable<Extensions>
{
    private readonly Dictionary<string, object> _inner;

    /// <summary>
    /// Initializes a new, empty <see cref="Extensions"/> instance.
    /// </summary>
    public Extensions()
    {
        _inner = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new <see cref="Extensions"/> instance from an existing dictionary.
    /// </summary>
    /// <param name="source">The source dictionary to copy entries from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public Extensions(IDictionary<string, object> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        _inner = new Dictionary<string, object>(source, StringComparer.Ordinal);
    }

    /// <summary>
    /// Retrieves an extension value by name and casts it to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The expected type of the extension value.</typeparam>
    /// <param name="name">The extension key (e.g. <c>"x-my-ext"</c>).</param>
    /// <returns>The value cast to <typeparamref name="T"/>.</returns>
    /// <exception cref="KeyNotFoundException">The key does not exist.</exception>
    /// <exception cref="InvalidCastException">The value cannot be cast to <typeparamref name="T"/>.</exception>
    public T Get<T>(string name)
    {
        if (!_inner.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"Extension '{name}' not found.");
        }

        return (T)value;
    }

    /// <summary>
    /// Tries to retrieve an extension value by name and cast it to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The expected type of the extension value.</typeparam>
    /// <param name="name">The extension key.</param>
    /// <param name="value">When this method returns, contains the value if found and castable; otherwise, the default value.</param>
    /// <returns><c>true</c> if the key was found and the value is of type <typeparamref name="T"/>; otherwise, <c>false</c>.</returns>
    public bool TryGet<T>(string name, out T value)
    {
        if (_inner.TryGetValue(name, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Retrieves an extension value by name and decodes it into an instance
    /// of <typeparamref name="T"/> using reflection-based property mapping.
    /// This mirrors compose-go's <c>Extensions.Get(name, target)</c> which
    /// uses <c>mapstructure.Decode</c> under the hood.
    /// <para>
    /// The extension value is expected to be a <see cref="Dictionary{String, Object}"/>
    /// (or an <see cref="Extensions"/>). Each dictionary key is matched to a property
    /// on <typeparamref name="T"/> using case-insensitive snake_case â†’ PascalCase mapping.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The target type to decode into. Must have a parameterless constructor.</typeparam>
    /// <param name="name">The extension key (e.g. <c>"x-my-config"</c>).</param>
    /// <returns>A new instance of <typeparamref name="T"/> populated from the extension value.</returns>
    /// <exception cref="KeyNotFoundException">The key does not exist.</exception>
    /// <exception cref="InvalidOperationException">
    /// The value is not a dictionary-like object or cannot be decoded.
    /// </exception>
    public T Decode<T>(string name) where T : new()
    {
        if (!_inner.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"Extension '{name}' not found.");
        }

        return ExtensionDecoder.Decode<T>(value);
    }

    /// <summary>
    /// Tries to retrieve an extension value by name and decode it into an instance
    /// of <typeparamref name="T"/> using reflection-based property mapping.
    /// </summary>
    /// <typeparam name="T">The target type to decode into. Must have a parameterless constructor.</typeparam>
    /// <param name="name">The extension key.</param>
    /// <param name="value">When this method returns, contains the decoded value if successful; otherwise, the default value.</param>
    /// <returns><c>true</c> if the key was found and successfully decoded; otherwise, <c>false</c>.</returns>
    public bool TryDecode<T>(string name, out T value) where T : new()
    {
        if (!_inner.TryGetValue(name, out var raw))
        {
            value = default!;
            return false;
        }

        try
        {
            value = ExtensionDecoder.Decode<T>(raw);
            return true;
        }
        catch
        {
            value = default!;
            return false;
        }
    }

    /// <summary>
    /// Creates a deep copy of this <see cref="Extensions"/> instance.
    /// Nested <see cref="Extensions"/> values are recursively deep-copied;
    /// all other values are shallow-copied.
    /// </summary>
    /// <returns>A new <see cref="Extensions"/> instance that is a deep copy of this one.</returns>
    public Extensions DeepCopy()
    {
        var copy = new Extensions();
        foreach (var kvp in _inner)
        {
            copy._inner[kvp.Key] = kvp.Value switch
            {
                Extensions nested => nested.DeepCopy(),
                IDictionary<string, object> dict => new Extensions(dict).DeepCopy(),
                ICloneable cloneable => cloneable.Clone(),
                _ => kvp.Value
            };
        }

        return copy;
    }

    #region IDictionary<string, object> implementation

    /// <inheritdoc />
    public object this[string key]
    {
        get => _inner[key];
        set => _inner[key] = value;
    }

    /// <inheritdoc />
    public ICollection<string> Keys => _inner.Keys;

    /// <inheritdoc />
    public ICollection<object> Values => _inner.Values;

    /// <inheritdoc />
    public int Count => _inner.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public void Add(string key, object value) => _inner.Add(key, value);

    /// <inheritdoc />
    public void Add(KeyValuePair<string, object> item) =>
        ((ICollection<KeyValuePair<string, object>>)_inner).Add(item);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public bool Contains(KeyValuePair<string, object> item) =>
        ((ICollection<KeyValuePair<string, object>>)_inner).Contains(item);

    /// <inheritdoc />
    public bool ContainsKey(string key) => _inner.ContainsKey(key);

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<string, object>>)_inner).CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(string key) => _inner.Remove(key);

    /// <inheritdoc />
    public bool Remove(KeyValuePair<string, object> item) =>
        ((ICollection<KeyValuePair<string, object>>)_inner).Remove(item);

    /// <inheritdoc />
    public bool TryGetValue(string key, out object value) => _inner.TryGetValue(key, out value!);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(Extensions? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_inner.Count != other._inner.Count) return false;

        foreach (var kvp in _inner)
        {
            if (!other._inner.TryGetValue(kvp.Key, out var otherValue)) return false;
            if (!Equals(kvp.Value, otherValue)) return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Extensions);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in _inner.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }

        return hash.ToHashCode();
    }

    #endregion
}

/// <summary>
/// Represents Compose service labels as a <c>string â†’ string</c> mapping.
/// Can be created from a list of <c>"key=value"</c> strings or from a <see cref="MappingWithEquals"/>.
/// </summary>
public class Labels : IDictionary<string, string>, IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _inner;

    /// <summary>
    /// Initializes a new, empty <see cref="Labels"/> instance.
    /// </summary>
    public Labels()
    {
        _inner = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new <see cref="Labels"/> instance from an existing dictionary.
    /// </summary>
    /// <param name="source">The source dictionary.</param>
    public Labels(IDictionary<string, string> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        _inner = new Dictionary<string, string>(source, StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates a new <see cref="Labels"/> from a list of <c>"key=value"</c> strings.
    /// If no <c>=</c> is present, the value defaults to an empty string.
    /// </summary>
    /// <param name="values">The list of key=value strings.</param>
    /// <returns>A new <see cref="Labels"/> instance.</returns>
    public static Labels NewLabels(IEnumerable<string> values)
    {
        var labels = new Labels();
        foreach (var entry in values)
        {
            var eqIndex = entry.IndexOf('=');
            if (eqIndex < 0)
            {
                labels._inner[entry] = string.Empty;
            }
            else
            {
                labels._inner[entry.Substring(0, eqIndex)] = entry.Substring(eqIndex + 1);
            }
        }

        return labels;
    }

    /// <summary>
    /// Creates a new <see cref="Labels"/> from a <see cref="MappingWithEquals"/>.
    /// Null values in the mapping are converted to empty strings.
    /// </summary>
    /// <param name="mapping">The source mapping.</param>
    /// <returns>A new <see cref="Labels"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mapping"/> is <c>null</c>.</exception>
    public static Labels NewLabelsFromMapping(MappingWithEquals mapping)
    {
        if (mapping == null) throw new ArgumentNullException(nameof(mapping));

        var labels = new Labels();
        foreach (var kvp in mapping)
        {
            labels._inner[kvp.Key] = kvp.Value ?? string.Empty;
        }

        return labels;
    }

    /// <summary>
    /// Converts this <see cref="Labels"/> to a <see cref="MappingWithEquals"/>.
    /// </summary>
    /// <returns>A new <see cref="MappingWithEquals"/> with the same key-value pairs.</returns>
    public MappingWithEquals ToMappingWithEquals()
    {
        var mapping = new MappingWithEquals();
        foreach (var kvp in _inner)
        {
            mapping[kvp.Key] = kvp.Value;
        }

        return mapping;
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
    public void Add(string key, string value) => _inner[key] = value;

    /// <inheritdoc />
    public void Add(KeyValuePair<string, string> item) =>
        ((ICollection<KeyValuePair<string, string>>)_inner).Add(item);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public bool Contains(KeyValuePair<string, string> item) =>
        ((ICollection<KeyValuePair<string, string>>)_inner).Contains(item);

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
    public bool Remove(KeyValuePair<string, string> item) =>
        ((ICollection<KeyValuePair<string, string>>)_inner).Remove(item);

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
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) =>
        _inner.TryGetValue(key, out value);
#endif

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(Labels? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_inner.Count != other._inner.Count) return false;

        foreach (var kvp in _inner)
        {
            if (!other._inner.TryGetValue(kvp.Key, out var otherValue)) return false;
            if (kvp.Value != otherValue) return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Labels);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in _inner.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }

        return hash.ToHashCode();
    }

    #endregion
}

/// <summary>
/// A list of strings that can be constructed from either a single string or a collection of strings.
/// </summary>
public class StringList : IList<string>, IEquatable<StringList>
{
    private readonly List<string> _inner;

    /// <summary>
    /// Initializes a new, empty <see cref="StringList"/>.
    /// </summary>
    public StringList()
    {
        _inner = new List<string>();
    }

    /// <summary>
    /// Initializes a new <see cref="StringList"/> containing a single element.
    /// </summary>
    /// <param name="value">The single string value.</param>
    public StringList(string value)
    {
        _inner = new List<string> { value ?? throw new ArgumentNullException(nameof(value)) };
    }

    /// <summary>
    /// Initializes a new <see cref="StringList"/> from a collection of strings.
    /// </summary>
    /// <param name="values">The string values.</param>
    public StringList(IEnumerable<string> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        _inner = new List<string>(values);
    }

    #region IList<string> implementation

    /// <inheritdoc />
    public string this[int index]
    {
        get => _inner[index];
        set => _inner[index] = value;
    }

    /// <inheritdoc />
    public int Count => _inner.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public void Add(string item) => _inner.Add(item);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public bool Contains(string item) => _inner.Contains(item);

    /// <inheritdoc />
    public void CopyTo(string[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(string item) => _inner.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, string item) => _inner.Insert(index, item);

    /// <inheritdoc />
    public bool Remove(string item) => _inner.Remove(item);

    /// <inheritdoc />
    public void RemoveAt(int index) => _inner.RemoveAt(index);

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(StringList? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _inner.SequenceEqual(other._inner);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StringList);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _inner)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    #endregion
}

/// <summary>
/// A list of strings that can also accept numeric values, converting them to their string representation.
/// </summary>
public class StringOrNumberList : IList<string>, IEquatable<StringOrNumberList>
{
    private readonly List<string> _inner;

    /// <summary>
    /// Initializes a new, empty <see cref="StringOrNumberList"/>.
    /// </summary>
    public StringOrNumberList()
    {
        _inner = new List<string>();
    }

    /// <summary>
    /// Initializes a new <see cref="StringOrNumberList"/> from a single string.
    /// </summary>
    /// <param name="value">The single string value.</param>
    public StringOrNumberList(string value)
    {
        _inner = new List<string> { value ?? throw new ArgumentNullException(nameof(value)) };
    }

    /// <summary>
    /// Initializes a new <see cref="StringOrNumberList"/> from a collection of strings.
    /// </summary>
    /// <param name="values">The string values.</param>
    public StringOrNumberList(IEnumerable<string> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        _inner = new List<string>(values);
    }

    /// <summary>
    /// Adds an integer value, converting it to its string representation.
    /// </summary>
    /// <param name="value">The integer value.</param>
    public void Add(int value) => _inner.Add(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Adds a long value, converting it to its string representation.
    /// </summary>
    /// <param name="value">The long value.</param>
    public void Add(long value) => _inner.Add(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Adds a double value, converting it to its string representation.
    /// </summary>
    /// <param name="value">The double value.</param>
    public void Add(double value) => _inner.Add(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Adds a value that may be a string or a numeric type. Numeric types are converted to strings.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <exception cref="ArgumentException">The value is not a string or numeric type.</exception>
    public void AddObject(object value)
    {
        switch (value)
        {
            case string s:
                _inner.Add(s);
                break;
            case int i:
                _inner.Add(i.ToString(CultureInfo.InvariantCulture));
                break;
            case long l:
                _inner.Add(l.ToString(CultureInfo.InvariantCulture));
                break;
            case float f:
                _inner.Add(f.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                _inner.Add(d.ToString(CultureInfo.InvariantCulture));
                break;
            case decimal m:
                _inner.Add(m.ToString(CultureInfo.InvariantCulture));
                break;
            case IFormattable formattable:
                _inner.Add(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                throw new ArgumentException(
                    $"Value must be a string or numeric type, but was {value?.GetType().Name ?? "null"}.",
                    nameof(value));
        }
    }

    #region IList<string> implementation

    /// <inheritdoc />
    public string this[int index]
    {
        get => _inner[index];
        set => _inner[index] = value;
    }

    /// <inheritdoc />
    public int Count => _inner.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public void Add(string item) => _inner.Add(item);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public bool Contains(string item) => _inner.Contains(item);

    /// <inheritdoc />
    public void CopyTo(string[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(string item) => _inner.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, string item) => _inner.Insert(index, item);

    /// <inheritdoc />
    public bool Remove(string item) => _inner.Remove(item);

    /// <inheritdoc />
    public void RemoveAt(int index) => _inner.RemoveAt(index);

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(StringOrNumberList? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _inner.SequenceEqual(other._inner);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StringOrNumberList);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _inner)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    #endregion
}

/// <summary>
/// Represents a Compose command that can be either a shell-form string
/// (e.g. <c>"echo hello"</c>) or an exec-form list of arguments
/// (e.g. <c>["echo", "hello"]</c>).
/// </summary>
public class ShellCommand : IEquatable<ShellCommand>
{
    private readonly string[] _parts;

    private ShellCommand(string[] parts, bool isShellForm)
    {
        _parts = parts ?? throw new ArgumentNullException(nameof(parts));
        IsShellForm = isShellForm;
    }

    /// <summary>
    /// Gets a value indicating whether this command is in shell form
    /// (a single string to be interpreted by <c>/bin/sh -c</c>).
    /// When <c>false</c>, the command is in exec form (a list of arguments).
    /// </summary>
    public bool IsShellForm { get; }

    /// <summary>
    /// Gets the parts of the command.
    /// For shell form, this is a single-element array containing the full command string.
    /// For exec form, this is the array of individual arguments.
    /// </summary>
    public string[] Parts => (string[])_parts.Clone();

    /// <summary>
    /// Creates a <see cref="ShellCommand"/> in shell form from a single command string.
    /// </summary>
    /// <param name="command">The shell command string.</param>
    /// <returns>A new <see cref="ShellCommand"/> in shell form.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.</exception>
    public static ShellCommand FromShellForm(string command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        return new ShellCommand(new[] { command }, isShellForm: true);
    }

    /// <summary>
    /// Creates a <see cref="ShellCommand"/> in exec form from a list of arguments.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>A new <see cref="ShellCommand"/> in exec form.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="args"/> is empty.</exception>
    public static ShellCommand FromExecForm(IEnumerable<string> args)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        var parts = args.ToArray();
        if (parts.Length == 0)
            throw new ArgumentException("Exec form command must have at least one argument.", nameof(args));
        return new ShellCommand(parts, isShellForm: false);
    }

    /// <summary>
    /// Parses a command from either a single string (shell form) or a list of strings (exec form).
    /// </summary>
    /// <param name="value">
    /// A <see cref="string"/> for shell form, or an <see cref="IEnumerable{String}"/> for exec form.
    /// </param>
    /// <returns>A new <see cref="ShellCommand"/>.</returns>
    /// <exception cref="ArgumentException">The value type is not supported.</exception>
    public static ShellCommand Parse(object value)
    {
        return value switch
        {
            string s => FromShellForm(s),
            IEnumerable<string> list => FromExecForm(list),
            _ => throw new ArgumentException(
                $"Cannot parse ShellCommand from {value?.GetType().Name ?? "null"}. Expected a string or a list of strings.",
                nameof(value))
        };
    }

    /// <summary>
    /// Returns the command as a display string.
    /// For shell form, returns the command string directly.
    /// For exec form, returns the arguments joined by spaces.
    /// </summary>
    /// <returns>A string representation of the command.</returns>
    public override string ToString()
    {
        return IsShellForm ? _parts[0] : string.Join(" ", _parts);
    }

    #region Equality

    /// <inheritdoc />
    public bool Equals(ShellCommand? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return IsShellForm == other.IsShellForm && _parts.SequenceEqual(other._parts);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ShellCommand);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IsShellForm);
        foreach (var part in _parts)
        {
            hash.Add(part);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two <see cref="ShellCommand"/> instances are equal.
    /// </summary>
    public static bool operator ==(ShellCommand? left, ShellCommand? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two <see cref="ShellCommand"/> instances are not equal.
    /// </summary>
    public static bool operator !=(ShellCommand? left, ShellCommand? right) => !Equals(left, right);

    #endregion
}

/// <summary>
/// Represents a host entry for <c>extra_hosts</c> in a Compose service.
/// </summary>
public readonly struct HostEntry : IEquatable<HostEntry>
{
    /// <summary>
    /// Initializes a new <see cref="HostEntry"/>.
    /// </summary>
    /// <param name="hostname">The hostname.</param>
    /// <param name="ip">The IP address.</param>
    public HostEntry(string hostname, string ip)
    {
        Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        IP = ip ?? throw new ArgumentNullException(nameof(ip));
    }

    /// <summary>
    /// Gets the hostname.
    /// </summary>
    public string Hostname { get; }

    /// <summary>
    /// Gets the IP address.
    /// </summary>
    public string IP { get; }

    /// <summary>
    /// Deconstructs this entry into its hostname and IP components.
    /// </summary>
    public void Deconstruct(out string hostname, out string ip)
    {
        hostname = Hostname;
        ip = IP;
    }

    /// <inheritdoc />
    public bool Equals(HostEntry other) =>
        string.Equals(Hostname, other.Hostname, StringComparison.Ordinal) &&
        string.Equals(IP, other.IP, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is HostEntry other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Hostname, StringComparer.Ordinal);
        hash.Add(IP, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => $"{Hostname}:{IP}";

    /// <summary>Determines whether two <see cref="HostEntry"/> values are equal.</summary>
    public static bool operator ==(HostEntry left, HostEntry right) => left.Equals(right);

    /// <summary>Determines whether two <see cref="HostEntry"/> values are not equal.</summary>
    public static bool operator !=(HostEntry left, HostEntry right) => !left.Equals(right);
}

/// <summary>
/// Stores <c>extra_hosts</c> entries as a list of <see cref="HostEntry"/> (hostname â†’ IP pairs).
/// Can parse from both list form (<c>"host:ip"</c> or <c>"host=ip"</c>) and map form.
/// </summary>
public class HostsList : IList<HostEntry>, IEquatable<HostsList>
{
    private readonly List<HostEntry> _inner;

    /// <summary>
    /// Initializes a new, empty <see cref="HostsList"/>.
    /// </summary>
    public HostsList()
    {
        _inner = new List<HostEntry>();
    }

    /// <summary>
    /// Initializes a new <see cref="HostsList"/> from an existing collection.
    /// </summary>
    /// <param name="entries">The host entries.</param>
    public HostsList(IEnumerable<HostEntry> entries)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        _inner = new List<HostEntry>(entries);
    }

    /// <summary>
    /// Parses a <see cref="HostsList"/> from a list of strings.
    /// Each string may use either <c>"hostname:ip"</c> or <c>"hostname=ip"</c> format.
    /// </summary>
    /// <param name="values">The list of host strings.</param>
    /// <returns>A new <see cref="HostsList"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">A string is not in a valid <c>host:ip</c> or <c>host=ip</c> format.</exception>
    public static HostsList FromStrings(IEnumerable<string> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));

        var list = new HostsList();
        foreach (var entry in values)
        {
            list.Add(ParseEntry(entry));
        }

        return list;
    }

    /// <summary>
    /// Parses a <see cref="HostsList"/> from a dictionary mapping hostnames to IP addresses.
    /// </summary>
    /// <param name="mapping">The hostname â†’ IP mapping.</param>
    /// <returns>A new <see cref="HostsList"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mapping"/> is <c>null</c>.</exception>
    public static HostsList FromMapping(IDictionary<string, string> mapping)
    {
        if (mapping == null) throw new ArgumentNullException(nameof(mapping));

        var list = new HostsList();
        foreach (var kvp in mapping)
        {
            list._inner.Add(new HostEntry(kvp.Key, kvp.Value));
        }

        return list;
    }

    /// <summary>
    /// Parses a single host entry string in <c>"hostname:ip"</c> or <c>"hostname=ip"</c> format.
    /// For IPv6 addresses, the <c>=</c> separator is preferred. When using <c>:</c>, the first
    /// colon is treated as the separator (matching compose-go behavior), with the remainder
    /// being the IP address.
    /// </summary>
    /// <param name="entry">The entry string to parse.</param>
    /// <returns>A <see cref="HostEntry"/>.</returns>
    /// <exception cref="FormatException">The string is not in a valid format.</exception>
    private static HostEntry ParseEntry(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            throw new FormatException("Host entry cannot be empty.");

        // Prefer '=' as separator (unambiguous, even for IPv6)
        var eqIndex = entry.IndexOf('=');
        if (eqIndex > 0)
        {
            var hostname = entry.Substring(0, eqIndex).Trim();
            var ip = entry.Substring(eqIndex + 1).Trim();
            return new HostEntry(hostname, ip);
        }

        // Fall back to ':' â€” compose-go splits on the first colon for the hostname,
        // with the remainder being the IP address.
        var colonIndex = entry.IndexOf(':');
        if (colonIndex > 0)
        {
            var hostname = entry.Substring(0, colonIndex).Trim();
            var ip = entry.Substring(colonIndex + 1).Trim();
            return new HostEntry(hostname, ip);
        }

        throw new FormatException(
            $"Invalid host entry '{entry}'. Expected format 'hostname:ip' or 'hostname=ip'.");
    }

    #region IList<HostEntry> implementation

    /// <inheritdoc />
    public HostEntry this[int index]
    {
        get => _inner[index];
        set => _inner[index] = value;
    }

    /// <inheritdoc />
    public int Count => _inner.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public void Add(HostEntry item) => _inner.Add(item);

    /// <summary>
    /// Adds a host entry with the specified hostname and IP.
    /// </summary>
    /// <param name="hostname">The hostname.</param>
    /// <param name="ip">The IP address.</param>
    public void Add(string hostname, string ip) => _inner.Add(new HostEntry(hostname, ip));

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public bool Contains(HostEntry item) => _inner.Contains(item);

    /// <inheritdoc />
    public void CopyTo(HostEntry[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(HostEntry item) => _inner.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, HostEntry item) => _inner.Insert(index, item);

    /// <inheritdoc />
    public bool Remove(HostEntry item) => _inner.Remove(item);

    /// <inheritdoc />
    public void RemoveAt(int index) => _inner.RemoveAt(index);

    /// <inheritdoc />
    public IEnumerator<HostEntry> GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(HostsList? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _inner.SequenceEqual(other._inner);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as HostsList);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _inner)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    #endregion
}
