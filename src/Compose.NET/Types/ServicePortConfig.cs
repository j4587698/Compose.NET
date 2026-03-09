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
/// Represents the port configuration for a Compose service.
/// Mirrors compose-go's <c>ServicePortConfig</c> struct.
/// </summary>
public class ServicePortConfig
{
    /// <summary>
    /// Gets or sets the name of the port mapping.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the port mode. Defaults to <c>"ingress"</c>.
    /// Valid values are <c>"ingress"</c> and <c>"host"</c>.
    /// </summary>
    public string Mode { get; set; } = "ingress";

    /// <summary>
    /// Gets or sets the host IP address to bind to.
    /// </summary>
    public string? HostIP { get; set; }

    /// <summary>
    /// Gets or sets the target (container) port number.
    /// </summary>
    public uint Target { get; set; }

    /// <summary>
    /// Gets or sets the published (host) port. This is a string to support
    /// port ranges such as <c>"8080-8090"</c>.
    /// </summary>
    public string? Published { get; set; }

    /// <summary>
    /// Gets or sets the transport protocol. Defaults to <c>"tcp"</c>.
    /// </summary>
    public string Protocol { get; set; } = "tcp";

    /// <summary>
    /// Gets or sets the application protocol.
    /// </summary>
    public string? AppProtocol { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new Extensions();
}

/// <summary>
/// Parses Docker Compose short-syntax port strings into <see cref="ServicePortConfig"/> instances.
/// </summary>
public static class PortParser
{
    /// <summary>
    /// Parses a short-syntax port string into a list of <see cref="ServicePortConfig"/>.
    /// <para>
    /// Supported formats:
    /// <list type="bullet">
    ///   <item><c>"80"</c> â€” container port only</item>
    ///   <item><c>"80:80"</c> â€” host:container</item>
    ///   <item><c>"8080:80"</c> â€” different host:container</item>
    ///   <item><c>"127.0.0.1:80:80"</c> â€” ip:host:container</item>
    ///   <item><c>"127.0.0.1:80:80/udp"</c> â€” with protocol</item>
    ///   <item><c>"8080-8090:80-90"</c> â€” port ranges (expanded)</item>
    ///   <item><c>"[::1]:80:80"</c> â€” IPv6 host IP</item>
    ///   <item><c>"80/udp"</c> â€” container port with protocol</item>
    ///   <item><c>"127.0.0.1::80"</c> â€” ip with empty host port</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="value">The port string to parse.</param>
    /// <returns>A list of <see cref="ServicePortConfig"/> instances.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null or whitespace.</exception>
    /// <exception cref="FormatException">The port string is not in a valid format.</exception>
    public static List<ServicePortConfig> ParsePortConfig(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Port config value cannot be null or empty.", nameof(value));

        // Extract protocol suffix (e.g., "/tcp", "/udp")
        var protocol = "tcp";
        var remaining = value.Trim();

        var slashIndex = remaining.LastIndexOf('/');
        if (slashIndex >= 0)
        {
            var proto = remaining.Substring(slashIndex + 1);
            if (IsValidProtocol(proto))
            {
                protocol = proto.ToLowerInvariant();
                remaining = remaining.Substring(0, slashIndex);
            }
        }

        // Split into: [hostIP], [hostPort], containerPort
        SplitPortString(remaining, out var hostIP, out var hostPort, out var containerPort);

        if (string.IsNullOrEmpty(containerPort))
            throw new FormatException($"Invalid port configuration: '{value}'. Missing container port.");

        // Parse container port (may be a range like "80-90")
        ParsePortRange(containerPort, out var targetStart, out var targetEnd);

        // Parse host port range if present
        uint publishedStart = 0;
        uint publishedEnd = 0;
        var hasPublished = !string.IsNullOrEmpty(hostPort);

        if (hasPublished)
        {
            ParsePortRange(hostPort!, out publishedStart, out publishedEnd);

            var targetCount = targetEnd - targetStart + 1;
            var publishedCount = publishedEnd - publishedStart + 1;

            if (targetCount != publishedCount)
            {
                throw new FormatException(
                    $"Invalid port configuration: '{value}'. " +
                    $"Port range mismatch: target range has {targetCount} ports " +
                    $"but published range has {publishedCount} ports.");
            }
        }

        var results = new List<ServicePortConfig>();
        var rangeSize = targetEnd - targetStart + 1;

        for (uint i = 0; i < rangeSize; i++)
        {
            var config = new ServicePortConfig
            {
                Target = targetStart + i,
                Protocol = protocol,
                HostIP = string.IsNullOrEmpty(hostIP) ? null : hostIP,
            };

            if (hasPublished)
            {
                config.Published = (publishedStart + i).ToString(CultureInfo.InvariantCulture);
            }

            results.Add(config);
        }

        return results;
    }

    /// <summary>
    /// Splits a port string (without protocol suffix) into host IP, host port, and container port components.
    /// Handles IPv6 addresses wrapped in brackets (e.g., <c>[::1]</c>).
    /// </summary>
    private static void SplitPortString(string input, out string? hostIP, out string? hostPort, out string containerPort)
    {
        hostIP = null;
        hostPort = null;
        containerPort = input;

        // Handle IPv6 host IP: [ip]:hostPort:containerPort or [ip]::containerPort
        if (input.StartsWith("["))
        {
            var closeBracket = input.IndexOf(']');
            if (closeBracket < 0)
                throw new FormatException($"Invalid port configuration: '{input}'. Unterminated IPv6 address.");

            hostIP = input.Substring(1, closeBracket - 1);

            // After "]", expect ":" followed by hostPort:containerPort
            var afterBracket = input.Substring(closeBracket + 1);
            if (afterBracket.Length == 0)
            {
                // Just "[::1]" â€” treat as container port? No, this doesn't make sense.
                throw new FormatException($"Invalid port configuration: '{input}'. Missing port after IPv6 address.");
            }

            if (afterBracket[0] != ':')
                throw new FormatException($"Invalid port configuration: '{input}'. Expected ':' after IPv6 address.");

            afterBracket = afterBracket.Substring(1); // remove leading ':'

            // Now afterBracket is "hostPort:containerPort" or ":containerPort" (empty host port)
            var colonIndex = afterBracket.IndexOf(':');
            if (colonIndex < 0)
            {
                // [::1]:containerPort â€” only container port, no host port
                containerPort = afterBracket;
                hostPort = null;
            }
            else
            {
                hostPort = afterBracket.Substring(0, colonIndex);
                containerPort = afterBracket.Substring(colonIndex + 1);

                // Empty host port means it was explicitly omitted (e.g., "[::1]::80")
                if (string.IsNullOrEmpty(hostPort))
                    hostPort = null;
            }

            return;
        }

        // Count colons to determine format
        var parts = SplitOnColons(input);

        switch (parts.Length)
        {
            case 1:
                // "80" or "80-90"
                containerPort = parts[0];
                break;

            case 2:
                // "80:80" or "8080-8090:80-90"
                hostPort = parts[0];
                containerPort = parts[1];

                // Empty host port: "host::container" won't reach here as that yields 3 parts.
                if (string.IsNullOrEmpty(hostPort))
                    hostPort = null;
                break;

            case 3:
                // "127.0.0.1:80:80" or "127.0.0.1::80"
                hostIP = parts[0];
                hostPort = parts[1];
                containerPort = parts[2];

                if (string.IsNullOrEmpty(hostPort))
                    hostPort = null;
                break;

            default:
                // Could be an IPv6 address without brackets, or an invalid format.
                // Try to detect "IPv6:hostPort:containerPort" by checking if the last
                // two parts look like port numbers/ranges and everything before is the IP.
                if (parts.Length >= 3)
                {
                    // Check if last part is a valid port/range
                    var lastPart = parts[parts.Length - 1];
                    var secondLast = parts[parts.Length - 2];

                    if (IsPortOrRange(lastPart))
                    {
                        containerPort = lastPart;

                        if (IsPortOrRange(secondLast))
                        {
                            hostPort = secondLast;
                            if (string.IsNullOrEmpty(hostPort))
                                hostPort = null;
                            hostIP = string.Join(":", parts, 0, parts.Length - 2);
                        }
                        else
                        {
                            hostIP = string.Join(":", parts, 0, parts.Length - 1);
                        }
                    }
                    else
                    {
                        throw new FormatException(
                            $"Invalid port configuration: '{input}'. Cannot parse port components.");
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Splits a string on colon characters.
    /// </summary>
    private static string[] SplitOnColons(string input)
    {
        return input.Split(':');
    }

    /// <summary>
    /// Determines whether a string looks like a port number or port range (e.g., "80" or "80-90").
    /// </summary>
    private static bool IsPortOrRange(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var dashIndex = value.IndexOf('-');
        if (dashIndex < 0)
        {
            return uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        }

        // Port range: "start-end"
        var startStr = value.Substring(0, dashIndex);
        var endStr = value.Substring(dashIndex + 1);
        return uint.TryParse(startStr, NumberStyles.None, CultureInfo.InvariantCulture, out _) &&
               uint.TryParse(endStr, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Parses a port or port range string (e.g., "80" or "80-90") into start and end values.
    /// For a single port, start and end are equal.
    /// </summary>
    private static void ParsePortRange(string portStr, out uint start, out uint end)
    {
        var dashIndex = portStr.IndexOf('-');
        if (dashIndex < 0)
        {
            if (!uint.TryParse(portStr, NumberStyles.None, CultureInfo.InvariantCulture, out start))
                throw new FormatException($"Invalid port number: '{portStr}'.");
            end = start;
            return;
        }

        var startStr = portStr.Substring(0, dashIndex);
        var endStr = portStr.Substring(dashIndex + 1);

        if (!uint.TryParse(startStr, NumberStyles.None, CultureInfo.InvariantCulture, out start))
            throw new FormatException($"Invalid port range start: '{startStr}'.");
        if (!uint.TryParse(endStr, NumberStyles.None, CultureInfo.InvariantCulture, out end))
            throw new FormatException($"Invalid port range end: '{endStr}'.");

        if (end < start)
            throw new FormatException($"Invalid port range: '{portStr}'. End port must be >= start port.");
    }

    /// <summary>
    /// Determines whether a string is a valid transport protocol identifier.
    /// </summary>
    private static bool IsValidProtocol(string proto)
    {
        return string.Equals(proto, "tcp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(proto, "udp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(proto, "sctp", StringComparison.OrdinalIgnoreCase);
    }
}
