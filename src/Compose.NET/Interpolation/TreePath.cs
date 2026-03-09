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
using System.Linq;

namespace Compose.NET.Interpolation
{
    /// <summary>
    /// Represents a dot-separated path in the YAML configuration tree.
    /// Used for matching specific locations in the config for type casting.
    /// Mirrors compose-go/tree.Path.
    /// </summary>
    internal readonly struct TreePath : IEquatable<TreePath>
    {
        /// <summary>
        /// Wildcard that matches any single path segment (key name).
        /// </summary>
        public const string MatchAll = "*";

        /// <summary>
        /// Wildcard that matches a list index position.
        /// </summary>
        public const string MatchList = "[]";

        private readonly string _path;

        /// <summary>
        /// The raw dot-separated path string.
        /// </summary>
        public string Value => _path ?? string.Empty;

        private TreePath(string path)
        {
            _path = path;
        }

        /// <summary>
        /// Creates a new TreePath from the given segments.
        /// </summary>
        public static TreePath New(params string[] parts)
        {
            return new TreePath(string.Join(".", parts));
        }

        /// <summary>
        /// Returns a new TreePath with the given part appended.
        /// </summary>
        public TreePath Next(string part)
        {
            if (string.IsNullOrEmpty(_path))
                return new TreePath(part);
            return new TreePath(_path + "." + part);
        }

        /// <summary>
        /// Returns the parts of this path.
        /// </summary>
        public string[] Parts()
        {
            if (string.IsNullOrEmpty(_path))
                return Array.Empty<string>();
            return _path.Split('.');
        }

        /// <summary>
        /// Checks if this path matches the given pattern.
        /// Patterns can use "*" to match any single segment and "[]" to match list indices.
        /// </summary>
        public bool Matches(TreePath pattern)
        {
            var pathParts = Parts();
            var patternParts = pattern.Parts();

            if (pathParts.Length != patternParts.Length)
                return false;

            for (int i = 0; i < pathParts.Length; i++)
            {
                var pp = patternParts[i];
                var tp = pathParts[i];

                if (pp == MatchAll)
                    continue;

                if (pp == MatchList)
                {
                    // Match any numeric index
                    if (!int.TryParse(tp, out _))
                        return false;
                    continue;
                }

                if (!string.Equals(pp, tp, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        public override string ToString() => Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override bool Equals(object? obj) =>
            obj is TreePath other && Equals(other);

        public bool Equals(TreePath other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public static bool operator ==(TreePath left, TreePath right) => left.Equals(right);
        public static bool operator !=(TreePath left, TreePath right) => !left.Equals(right);
    }
}
