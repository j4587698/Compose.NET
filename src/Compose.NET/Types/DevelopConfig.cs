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

namespace Compose.NET.Types;

#region WatchAction

/// <summary>
/// Defines the action to take when a file matching a watch trigger changes.
/// Mirrors compose-go's <c>WatchAction</c> type.
/// </summary>
public static class WatchActions
{
    /// <summary>Sync the changed files into the running container.</summary>
    public const string Sync = "sync";

    /// <summary>Rebuild the service image and recreate the container.</summary>
    public const string Rebuild = "rebuild";

    /// <summary>Restart the service container.</summary>
    public const string Restart = "restart";

    /// <summary>Sync the changed files and then restart the container.</summary>
    public const string SyncRestart = "sync+restart";

    /// <summary>Sync the changed files and then execute a command.</summary>
    public const string SyncExec = "sync+exec";
}

#endregion

#region DevelopConfig

/// <summary>
/// Represents the <c>develop</c> section of a Compose service,
/// providing file watching and live sync configuration.
/// Mirrors compose-go's <c>DevelopConfig</c> struct.
/// </summary>
public class DevelopConfig
{
    /// <summary>
    /// Gets or sets the list of file watch triggers.
    /// </summary>
    public List<Trigger> Watch { get; set; } = new();

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

#endregion

#region Trigger

/// <summary>
/// Represents a single file watch trigger within a <see cref="DevelopConfig"/>.
/// When files matching the <see cref="Path"/> (and optional <see cref="Include"/>/<see cref="Ignore"/> patterns)
/// change, the specified <see cref="Action"/> is executed.
/// Mirrors compose-go's <c>Trigger</c> struct.
/// </summary>
public class Trigger
{
    /// <summary>
    /// Gets or sets the path to watch for changes.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Gets or sets the action to take when a change is detected.
    /// See <see cref="WatchActions"/> for well-known values.
    /// </summary>
    public string Action { get; set; } = "";

    /// <summary>
    /// Gets or sets the target path in the container for sync operations.
    /// Required when <see cref="Action"/> is <see cref="WatchActions.Sync"/> or
    /// <see cref="WatchActions.SyncRestart"/>.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Gets or sets the exec hook to run when <see cref="Action"/> is <see cref="WatchActions.SyncExec"/>.
    /// </summary>
    public ServiceHook? Exec { get; set; }

    /// <summary>
    /// Gets or sets glob patterns to include in the watch.
    /// If empty, all files under <see cref="Path"/> are watched.
    /// </summary>
    public List<string> Include { get; set; } = new();

    /// <summary>
    /// Gets or sets glob patterns to exclude from the watch.
    /// </summary>
    public List<string> Ignore { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to perform an initial sync when starting.
    /// </summary>
    public bool InitialSync { get; set; }

    /// <summary>
    /// Gets or sets the <c>x-*</c> custom extension fields.
    /// </summary>
    public Extensions Extensions { get; set; } = new();
}

#endregion
