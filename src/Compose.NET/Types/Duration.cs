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
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Compose.NET.Types
{
    /// <summary>
    /// Represents a duration value compatible with Go/Docker-style duration strings.
    /// Supports units: ns (nanoseconds), us/Âµs (microseconds), ms (milliseconds),
    /// s (seconds), m (minutes), h (hours).
    /// Examples: "1h30m", "10s", "2.5s", "300ms", "1h30m10s", "0", "1us", "1ns".
    /// </summary>
    public readonly struct Duration : IEquatable<Duration>, IComparable<Duration>
    {
        private static readonly Regex DurationPattern = new Regex(
            @"^[-+]?(?:0|(?:\d+(?:\.\d*)?|\.\d+)(?:ns|us|Âµs|ms|s|m|h))+$",
            RegexOptions.Compiled);

        private static readonly Regex TokenPattern = new Regex(
            @"(\d+(?:\.\d*)?|\.\d+)(ns|us|Âµs|ms|s|m|h)",
            RegexOptions.Compiled);

        /// <summary>
        /// A <see cref="Duration"/> representing zero time.
        /// </summary>
        public static readonly Duration Zero = new Duration(TimeSpan.Zero);

        private readonly TimeSpan _value;

        /// <summary>
        /// Initializes a new <see cref="Duration"/> with the specified <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="value">The time span value.</param>
        public Duration(TimeSpan value)
        {
            _value = value;
        }

        /// <summary>
        /// Gets the underlying <see cref="TimeSpan"/> value.
        /// </summary>
        public TimeSpan Value => _value;

        /// <summary>
        /// Attempts to parse a Go/Docker-style duration string into a <see cref="Duration"/>.
        /// </summary>
        /// <param name="input">The duration string to parse.</param>
        /// <param name="result">When this method returns, contains the parsed duration if successful.</param>
        /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
        public static bool TryParse(string? input, out Duration result)
        {
            result = Zero;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var trimmed = input.Trim();

            // Handle plain "0"
            if (trimmed == "0")
            {
                result = Zero;
                return true;
            }

            // Determine sign
            bool negative = false;
            var toParse = trimmed;
            if (toParse[0] == '-')
            {
                negative = true;
                toParse = toParse.Substring(1);
            }
            else if (toParse[0] == '+')
            {
                toParse = toParse.Substring(1);
            }

            if (toParse.Length == 0)
                return false;

            if (!DurationPattern.IsMatch(trimmed))
                return false;

            var matches = TokenPattern.Matches(toParse);
            if (matches.Count == 0)
                return false;

            // Verify that the tokens cover the entire string (no gaps / invalid chars)
            int expectedPos = 0;
            foreach (Match m in matches)
            {
                if (m.Index != expectedPos)
                    return false;
                expectedPos = m.Index + m.Length;
            }
            if (expectedPos != toParse.Length)
                return false;

            double totalNanoseconds = 0;

            foreach (Match match in matches)
            {
                if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    return false;

                var unit = match.Groups[2].Value;
                double multiplier;

                switch (unit)
                {
                    case "ns":
                        multiplier = 1.0;
                        break;
                    case "us":
                    case "Âµs":
                        multiplier = 1_000.0;
                        break;
                    case "ms":
                        multiplier = 1_000_000.0;
                        break;
                    case "s":
                        multiplier = 1_000_000_000.0;
                        break;
                    case "m":
                        multiplier = 60.0 * 1_000_000_000.0;
                        break;
                    case "h":
                        multiplier = 3600.0 * 1_000_000_000.0;
                        break;
                    default:
                        return false;
                }

                totalNanoseconds += number * multiplier;
            }

            if (negative)
                totalNanoseconds = -totalNanoseconds;

            // Convert nanoseconds to ticks (1 tick = 100 ns)
            var ticks = (long)Math.Round(totalNanoseconds / 100.0);
            result = new Duration(new TimeSpan(ticks));
            return true;
        }

        /// <summary>
        /// Parses a Go/Docker-style duration string into a <see cref="Duration"/>.
        /// </summary>
        /// <param name="input">The duration string to parse.</param>
        /// <returns>The parsed <see cref="Duration"/>.</returns>
        /// <exception cref="FormatException">Thrown when <paramref name="input"/> is not a valid duration string.</exception>
        public static Duration Parse(string input)
        {
            if (!TryParse(input, out var result))
                throw new FormatException($"Invalid duration string: '{input}'");
            return result;
        }

        /// <summary>
        /// Converts this duration to a Go-style duration string (e.g. "1h30m10s").
        /// </summary>
        /// <returns>A Go-style duration string representation.</returns>
        public override string ToString()
        {
            if (_value == TimeSpan.Zero)
                return "0s";

            var sb = new StringBuilder();
            var ticks = _value.Ticks;

            if (ticks < 0)
            {
                sb.Append('-');
                ticks = -ticks;
            }

            var totalNanoseconds = ticks * 100L;

            var hours = totalNanoseconds / (3600L * 1_000_000_000L);
            totalNanoseconds %= 3600L * 1_000_000_000L;

            var minutes = totalNanoseconds / (60L * 1_000_000_000L);
            totalNanoseconds %= 60L * 1_000_000_000L;

            var seconds = totalNanoseconds / 1_000_000_000L;
            totalNanoseconds %= 1_000_000_000L;

            var milliseconds = totalNanoseconds / 1_000_000L;
            totalNanoseconds %= 1_000_000L;

            var microseconds = totalNanoseconds / 1_000L;
            var nanoseconds = totalNanoseconds % 1_000L;

            if (hours > 0)
                sb.Append(hours).Append('h');
            if (minutes > 0)
                sb.Append(minutes).Append('m');
            if (seconds > 0)
                sb.Append(seconds).Append('s');
            if (milliseconds > 0)
                sb.Append(milliseconds).Append("ms");
            if (microseconds > 0)
                sb.Append(microseconds).Append("us");
            if (nanoseconds > 0)
                sb.Append(nanoseconds).Append("ns");

            return sb.ToString();
        }

        /// <summary>
        /// Implicitly converts a <see cref="Duration"/> to a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="duration">The duration to convert.</param>
        public static implicit operator TimeSpan(Duration duration) => duration._value;

        /// <summary>
        /// Implicitly converts a <see cref="TimeSpan"/> to a <see cref="Duration"/>.
        /// </summary>
        /// <param name="timeSpan">The time span to convert.</param>
        public static implicit operator Duration(TimeSpan timeSpan) => new Duration(timeSpan);

        /// <inheritdoc />
        public bool Equals(Duration other) => _value.Equals(other._value);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Duration other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _value.GetHashCode();

        /// <inheritdoc />
        public int CompareTo(Duration other) => _value.CompareTo(other._value);

        /// <summary>Equality operator.</summary>
        public static bool operator ==(Duration left, Duration right) => left.Equals(right);

        /// <summary>Inequality operator.</summary>
        public static bool operator !=(Duration left, Duration right) => !left.Equals(right);

        /// <summary>Less-than operator.</summary>
        public static bool operator <(Duration left, Duration right) => left.CompareTo(right) < 0;

        /// <summary>Less-than-or-equal operator.</summary>
        public static bool operator <=(Duration left, Duration right) => left.CompareTo(right) <= 0;

        /// <summary>Greater-than operator.</summary>
        public static bool operator >(Duration left, Duration right) => left.CompareTo(right) > 0;

        /// <summary>Greater-than-or-equal operator.</summary>
        public static bool operator >=(Duration left, Duration right) => left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Represents a byte quantity compatible with Docker byte strings.
    /// Supports suffixes: b, k/kb, m/mb, g/gb, t/tb (case-insensitive).
    /// Examples: "1024", "1k", "1kb", "1m", "1mb", "1g", "1gb", "1t", "1tb".
    /// </summary>
    public readonly struct UnitBytes : IEquatable<UnitBytes>, IComparable<UnitBytes>
    {
        private static readonly Regex BytesPattern = new Regex(
            @"^\s*(\d+(?:\.\d+)?)\s*(b|k|kb|m|mb|g|gb|t|tb)?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// A <see cref="UnitBytes"/> representing zero bytes.
        /// </summary>
        public static readonly UnitBytes Zero = new UnitBytes(0);

        private readonly long _bytes;

        /// <summary>
        /// Initializes a new <see cref="UnitBytes"/> with the specified byte count.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        public UnitBytes(long bytes)
        {
            _bytes = bytes;
        }

        /// <summary>
        /// Gets the number of bytes.
        /// </summary>
        public long Bytes => _bytes;

        /// <summary>
        /// Attempts to parse a Docker-style byte string into a <see cref="UnitBytes"/>.
        /// </summary>
        /// <param name="input">The byte string to parse.</param>
        /// <param name="result">When this method returns, contains the parsed value if successful.</param>
        /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
        public static bool TryParse(string? input, out UnitBytes result)
        {
            result = Zero;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var match = BytesPattern.Match(input);
            if (!match.Success)
                return false;

            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return false;

            var suffix = match.Groups[2].Success ? match.Groups[2].Value.ToLowerInvariant() : string.Empty;

            long multiplier;
            switch (suffix)
            {
                case "":
                case "b":
                    multiplier = 1L;
                    break;
                case "k":
                case "kb":
                    multiplier = 1024L;
                    break;
                case "m":
                case "mb":
                    multiplier = 1024L * 1024L;
                    break;
                case "g":
                case "gb":
                    multiplier = 1024L * 1024L * 1024L;
                    break;
                case "t":
                case "tb":
                    multiplier = 1024L * 1024L * 1024L * 1024L;
                    break;
                default:
                    return false;
            }

            result = new UnitBytes((long)(number * multiplier));
            return true;
        }

        /// <summary>
        /// Parses a Docker-style byte string into a <see cref="UnitBytes"/>.
        /// </summary>
        /// <param name="input">The byte string to parse.</param>
        /// <returns>The parsed <see cref="UnitBytes"/>.</returns>
        /// <exception cref="FormatException">Thrown when <paramref name="input"/> is not a valid byte string.</exception>
        public static UnitBytes Parse(string input)
        {
            if (!TryParse(input, out var result))
                throw new FormatException($"Invalid byte string: '{input}'");
            return result;
        }

        /// <summary>
        /// Converts this byte quantity to a human-readable string using the largest
        /// whole-unit suffix that divides evenly, or bytes if none does.
        /// </summary>
        /// <returns>A human-readable byte string representation.</returns>
        public override string ToString()
        {
            const long kb = 1024L;
            const long mb = kb * 1024L;
            const long gb = mb * 1024L;
            const long tb = gb * 1024L;

            var abs = Math.Abs(_bytes);

            if (abs == 0)
                return "0B";

            if (abs >= tb && _bytes % tb == 0)
                return (_bytes / tb).ToString(CultureInfo.InvariantCulture) + "TB";
            if (abs >= gb && _bytes % gb == 0)
                return (_bytes / gb).ToString(CultureInfo.InvariantCulture) + "GB";
            if (abs >= mb && _bytes % mb == 0)
                return (_bytes / mb).ToString(CultureInfo.InvariantCulture) + "MB";
            if (abs >= kb && _bytes % kb == 0)
                return (_bytes / kb).ToString(CultureInfo.InvariantCulture) + "KB";

            return _bytes.ToString(CultureInfo.InvariantCulture) + "B";
        }

        /// <summary>
        /// Implicitly converts a <see cref="UnitBytes"/> to a <see cref="long"/> (byte count).
        /// </summary>
        /// <param name="unitBytes">The unit bytes to convert.</param>
        public static implicit operator long(UnitBytes unitBytes) => unitBytes._bytes;

        /// <summary>
        /// Implicitly converts a <see cref="long"/> to a <see cref="UnitBytes"/>.
        /// </summary>
        /// <param name="bytes">The byte count to convert.</param>
        public static implicit operator UnitBytes(long bytes) => new UnitBytes(bytes);

        /// <inheritdoc />
        public bool Equals(UnitBytes other) => _bytes == other._bytes;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is UnitBytes other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _bytes.GetHashCode();

        /// <inheritdoc />
        public int CompareTo(UnitBytes other) => _bytes.CompareTo(other._bytes);

        /// <summary>Equality operator.</summary>
        public static bool operator ==(UnitBytes left, UnitBytes right) => left.Equals(right);

        /// <summary>Inequality operator.</summary>
        public static bool operator !=(UnitBytes left, UnitBytes right) => !left.Equals(right);

        /// <summary>Less-than operator.</summary>
        public static bool operator <(UnitBytes left, UnitBytes right) => left.CompareTo(right) < 0;

        /// <summary>Less-than-or-equal operator.</summary>
        public static bool operator <=(UnitBytes left, UnitBytes right) => left.CompareTo(right) <= 0;

        /// <summary>Greater-than operator.</summary>
        public static bool operator >(UnitBytes left, UnitBytes right) => left.CompareTo(right) > 0;

        /// <summary>Greater-than-or-equal operator.</summary>
        public static bool operator >=(UnitBytes left, UnitBytes right) => left.CompareTo(right) >= 0;
    }
}
