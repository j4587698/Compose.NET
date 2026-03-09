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

using System.Globalization;

namespace Compose.NET.Types;

/// <summary>
/// Represents a CPU resource value as a fractional number of CPUs
/// (e.g. <c>0.5</c> for half a CPU, <c>2.0</c> for two CPUs).
/// Despite the name (which matches compose-go's <c>NanoCPUs</c> type),
/// the value is stored as a CPU fraction, not nanoseconds.
/// </summary>
/// <remarks>
/// Mirrors compose-go's <c>type NanoCPUs float32</c> with its
/// <c>DecodeMapstructure</c> method that accepts string, int, and float inputs.
/// </remarks>
public readonly struct NanoCPUs : IEquatable<NanoCPUs>, IComparable<NanoCPUs>
{
    /// <summary>
    /// A <see cref="NanoCPUs"/> representing zero CPUs.
    /// </summary>
    public static readonly NanoCPUs Zero = new(0f);

    private readonly float _value;

    /// <summary>
    /// Initializes a new <see cref="NanoCPUs"/> with the specified CPU fraction.
    /// </summary>
    /// <param name="value">The fractional number of CPUs.</param>
    public NanoCPUs(float value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the CPU fraction value.
    /// </summary>
    public float Value => _value;

    /// <summary>
    /// Attempts to parse a value (string, int, or float) into a <see cref="NanoCPUs"/>.
    /// Mirrors compose-go's <c>NanoCPUs.DecodeMapstructure</c>.
    /// </summary>
    /// <param name="input">The value to parse (string, int, long, float, or double).</param>
    /// <param name="result">When this method returns, contains the parsed value if successful.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(object? input, out NanoCPUs result)
    {
        result = Zero;

        switch (input)
        {
            case null:
                return false;

            case float f:
                result = new NanoCPUs(f);
                return true;

            case double d:
                result = new NanoCPUs((float)d);
                return true;

            case int i:
                result = new NanoCPUs(i);
                return true;

            case long l:
                result = new NanoCPUs(l);
                return true;

            case string s:
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    result = new NanoCPUs(parsed);
                    return true;
                }
                return false;

            default:
                var str = input.ToString();
                if (str != null && float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed2))
                {
                    result = new NanoCPUs(parsed2);
                    return true;
                }
                return false;
        }
    }

    /// <summary>
    /// Parses a value into a <see cref="NanoCPUs"/>.
    /// </summary>
    /// <param name="input">The value to parse.</param>
    /// <returns>The parsed <see cref="NanoCPUs"/>.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="input"/> cannot be parsed.</exception>
    public static NanoCPUs Parse(object input)
    {
        if (!TryParse(input, out var result))
            throw new FormatException($"Cannot parse '{input}' as NanoCPUs");
        return result;
    }

    /// <summary>
    /// Returns the string representation of the CPU fraction value.
    /// </summary>
    /// <returns>A string like <c>"0.5"</c> or <c>"2"</c>.</returns>
    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Implicitly converts a <see cref="NanoCPUs"/> to a <see cref="float"/>.
    /// </summary>
    public static implicit operator float(NanoCPUs cpus) => cpus._value;

    /// <summary>
    /// Implicitly converts a <see cref="float"/> to a <see cref="NanoCPUs"/>.
    /// </summary>
    public static implicit operator NanoCPUs(float value) => new(value);

    /// <inheritdoc />
    public bool Equals(NanoCPUs other) => _value.Equals(other._value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is NanoCPUs other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(NanoCPUs other) => _value.CompareTo(other._value);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(NanoCPUs left, NanoCPUs right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(NanoCPUs left, NanoCPUs right) => !left.Equals(right);

    /// <summary>Less-than operator.</summary>
    public static bool operator <(NanoCPUs left, NanoCPUs right) => left.CompareTo(right) < 0;

    /// <summary>Less-than-or-equal operator.</summary>
    public static bool operator <=(NanoCPUs left, NanoCPUs right) => left.CompareTo(right) <= 0;

    /// <summary>Greater-than operator.</summary>
    public static bool operator >(NanoCPUs left, NanoCPUs right) => left.CompareTo(right) > 0;

    /// <summary>Greater-than-or-equal operator.</summary>
    public static bool operator >=(NanoCPUs left, NanoCPUs right) => left.CompareTo(right) >= 0;
}
