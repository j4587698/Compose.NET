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

#if NETSTANDARD2_1

// Polyfills the trimming/AOT analysis attributes that are only available on
// newer target frameworks so the netstandard2.1 build can carry the same
// annotations as the net8.0+ builds.

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Indicates that the specified method or type contains members that use
    /// reflection and therefore cannot be safely trimmed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class
                    | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field
                    | AttributeTargets.Interface | AttributeTargets.Event, Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequiresUnreferencedCodeAttribute"/> class.
        /// </summary>
        public RequiresUnreferencedCodeAttribute(string message)
        {
            Message = message;
        }

        /// <summary>
        /// Gets a message that contains information about the reflection usage.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets or sets an optional URL that contains more information about the method.
        /// </summary>
        public string? Url { get; set; }
    }

    /// <summary>
    /// Indicates that the specified method requires the ability to generate new
    /// code at runtime, for example via Reflection.Emit or MakeGenericType.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct
                    | AttributeTargets.Constructor | AttributeTargets.Field, Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequiresDynamicCodeAttribute"/> class.
        /// </summary>
        public RequiresDynamicCodeAttribute(string message)
        {
            Message = message;
        }

        /// <summary>
        /// Gets a message that contains information about the dynamic code requirement.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets or sets an optional URL that contains more information about the method.
        /// </summary>
        public string? Url { get; set; }
    }
}

#endif
