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

namespace Compose.NET.Errors
{
    /// <summary>
    /// Base exception for all Compose.NET errors.
    /// Mirrors compose-go's errdefs package.
    /// </summary>
    public class ComposeException : Exception
    {
        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        public ComposeException(string message) : base(message) { }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ComposeException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a requested resource (service, network, volume, secret, config) is not found.
    /// Mirrors compose-go's <c>errdefs.ErrNotFound</c>.
    /// </summary>
    public class ComposeNotFoundException : ComposeException
    {
        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        public ComposeNotFoundException(string message) : base(message) { }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ComposeNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a Compose configuration is invalid.
    /// Mirrors compose-go's <c>errdefs.ErrInvalid</c>.
    /// </summary>
    public class ComposeInvalidException : ComposeException
    {
        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        public ComposeInvalidException(string message) : base(message) { }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ComposeInvalidException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when an unsupported feature or option is used.
    /// Mirrors compose-go's <c>errdefs.ErrUnsupported</c>.
    /// </summary>
    public class ComposeUnsupportedException : ComposeException
    {
        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        public ComposeUnsupportedException(string message) : base(message) { }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ComposeUnsupportedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when incompatible options or configurations are detected.
    /// Mirrors compose-go's <c>errdefs.ErrIncompatible</c>.
    /// </summary>
    public class ComposeIncompatibleException : ComposeException
    {
        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        public ComposeIncompatibleException(string message) : base(message) { }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ComposeIncompatibleException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when attempting to access a service that is disabled (e.g., by profile filtering).
    /// Mirrors compose-go's <c>errdefs.ErrDisabled</c>.
    /// </summary>
    public class ComposeDisabledException : ComposeException
    {
        /// <summary>
        /// Gets the name of the disabled service.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>Initializes a new instance for the specified disabled service.</summary>
        /// <param name="serviceName">The name of the disabled service.</param>
        public ComposeDisabledException(string serviceName)
            : base($"service '{serviceName}' is disabled")
        {
            ServiceName = serviceName;
        }

        /// <summary>Initializes a new instance for the specified disabled service with a custom message.</summary>
        /// <param name="serviceName">The name of the disabled service.</param>
        /// <param name="message">A custom error message.</param>
        public ComposeDisabledException(string serviceName, string message)
            : base(message)
        {
            ServiceName = serviceName;
        }
    }

    /// <summary>
    /// Provides static helper methods for checking Compose error types.
    /// Mirrors compose-go's <c>errdefs.Is*()</c> helper functions.
    /// </summary>
    public static class ComposeErrorHelpers
    {
        /// <summary>
        /// Checks whether the exception is or wraps a <see cref="ComposeNotFoundException"/>.
        /// </summary>
        public static bool IsNotFound(Exception? ex) => IsOrWraps<ComposeNotFoundException>(ex);

        /// <summary>
        /// Checks whether the exception is or wraps a <see cref="ComposeInvalidException"/>.
        /// </summary>
        public static bool IsInvalid(Exception? ex) => IsOrWraps<ComposeInvalidException>(ex);

        /// <summary>
        /// Checks whether the exception is or wraps a <see cref="ComposeUnsupportedException"/>.
        /// </summary>
        public static bool IsUnsupported(Exception? ex) => IsOrWraps<ComposeUnsupportedException>(ex);

        /// <summary>
        /// Checks whether the exception is or wraps a <see cref="ComposeIncompatibleException"/>.
        /// </summary>
        public static bool IsIncompatible(Exception? ex) => IsOrWraps<ComposeIncompatibleException>(ex);

        /// <summary>
        /// Checks whether the exception is or wraps a <see cref="ComposeDisabledException"/>.
        /// </summary>
        public static bool IsDisabled(Exception? ex) => IsOrWraps<ComposeDisabledException>(ex);

        /// <summary>
        /// Walks the InnerException chain looking for an exception of type <typeparamref name="T"/>.
        /// Mirrors Go's <c>errors.Is()</c> / <c>errors.As()</c> unwrapping behaviour.
        /// </summary>
        private static bool IsOrWraps<T>(Exception? ex) where T : Exception
        {
            while (ex != null)
            {
                if (ex is T)
                    return true;
                ex = ex.InnerException;
            }

            return false;
        }
    }
}
