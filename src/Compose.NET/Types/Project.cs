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

namespace Compose.NET.Types;

/// <summary>
/// Project is the result of loading a set of Compose files.
/// Mirrors compose-go's types.Project.
/// </summary>
public class Project
{
    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the working directory for the project (the directory containing the Compose file).
    /// </summary>
    public string WorkingDir { get; set; } = "";

    /// <summary>
    /// Gets or sets the services defined in the project.
    /// </summary>
    public Services Services { get; set; } = new();

    /// <summary>
    /// Gets or sets the networks defined in the project.
    /// </summary>
    public Networks Networks { get; set; } = new();

    /// <summary>
    /// Gets or sets the volumes defined in the project.
    /// </summary>
    public Volumes Volumes { get; set; } = new();

    /// <summary>
    /// Gets or sets the secrets defined in the project.
    /// </summary>
    public Secrets Secrets { get; set; } = new();

    /// <summary>
    /// Gets or sets the configs defined in the project.
    /// </summary>
    public Configs Configs { get; set; } = new();

    /// <summary>
    /// Gets or sets the AI/ML models defined in the project.
    /// </summary>
    public Models Models { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom <c>x-*</c> extension fields at the project level.
    /// </summary>
    public Extensions Extensions { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of Compose file paths that were merged to produce this project.
    /// </summary>
    public List<string> ComposeFiles { get; set; } = new();

    /// <summary>
    /// Gets or sets the environment variables available during interpolation.
    /// </summary>
    public Mapping Environment { get; set; } = new();

    /// <summary>
    /// Gets or sets the services that are disabled (e.g. by profile filtering).
    /// </summary>
    public Services DisabledServices { get; set; } = new();

    /// <summary>
    /// Gets or sets the active profiles for this project.
    /// </summary>
    public List<string> Profiles { get; set; } = new();

    /// <summary>
    /// Returns the sorted list of service names defined in this project.
    /// </summary>
    /// <returns>A sorted list of service names.</returns>
    public List<string> ServiceNames()
    {
        var names = new List<string>(Services.Keys);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Returns the sorted list of disabled service names.
    /// </summary>
    /// <returns>A sorted list of disabled service names.</returns>
    public List<string> DisabledServiceNames()
    {
        var names = new List<string>(DisabledServices.Keys);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Returns the sorted list of volume names defined in this project.
    /// </summary>
    /// <returns>A sorted list of volume names.</returns>
    public List<string> VolumeNames()
    {
        var names = new List<string>(Volumes.Keys);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Returns the sorted list of network names defined in this project.
    /// </summary>
    /// <returns>A sorted list of network names.</returns>
    public List<string> NetworkNames()
    {
        var names = new List<string>(Networks.Keys);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Returns the sorted list of secret names defined in this project.
    /// </summary>
    /// <returns>A sorted list of secret names.</returns>
    public List<string> SecretNames()
    {
        var names = new List<string>(Secrets.Keys);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Returns the sorted list of config names defined in this project.
    /// </summary>
    /// <returns>A sorted list of config names.</returns>
    public List<string> ConfigNames()
    {
        var names = new List<string>(Configs.Keys);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Returns the sorted list of model names defined in this project.
    /// </summary>
    /// <returns>A sorted list of model names.</returns>
    public List<string> ModelNames()
    {
        var names = new List<string>(Models.Keys);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Creates a deep copy of this <see cref="Project"/>.
    /// All nested objects, lists, and dictionaries are recursively copied.
    /// Mirrors compose-go's code-generated <c>deepCopy</c> method.
    /// </summary>
    /// <returns>A new <see cref="Project"/> that is a completely independent copy.</returns>
    public Project DeepCopy()
    {
        return new Project
        {
            Name = Name,
            WorkingDir = WorkingDir,
            Services = DeepCopyHelper.CopyServices(Services),
            Networks = DeepCopyHelper.CopyNetworks(Networks),
            Volumes = DeepCopyHelper.CopyVolumes(Volumes),
            Secrets = DeepCopyHelper.CopySecrets(Secrets),
            Configs = DeepCopyHelper.CopyConfigs(Configs),
            Models = DeepCopyHelper.CopyModels(Models),
            Extensions = DeepCopyHelper.CopyExtensions(Extensions),
            ComposeFiles = new List<string>(ComposeFiles),
            Environment = (Mapping?)DeepCopyHelper.CopyMapping(Environment) ?? new Mapping(),
            DisabledServices = DeepCopyHelper.CopyServices(DisabledServices),
            Profiles = new List<string>(Profiles)
        };
    }

    /// <summary>
    /// Gets a service by name.
    /// </summary>
    /// <param name="name">The service name to look up.</param>
    /// <returns>The <see cref="ServiceConfig"/> for the named service.</returns>
    /// <exception cref="ComposeDisabledException">The service exists but is disabled by profile filtering.</exception>
    /// <exception cref="ComposeNotFoundException">No service with the given name exists in the project.</exception>
    public ServiceConfig GetService(string name)
    {
        if (Services.TryGetValue(name, out var service))
            return service;

        if (DisabledServices.ContainsKey(name))
            throw new ComposeDisabledException(name);

        throw new ComposeNotFoundException($"No such service: '{name}'.");
    }

    /// <summary>
    /// Tries to get a service by name.
    /// </summary>
    /// <param name="name">The service name to look up.</param>
    /// <param name="service">
    /// When this method returns, contains the <see cref="ServiceConfig"/> if found;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if the service was found; otherwise, <c>false</c>.</returns>
    public bool TryGetService(string name, out ServiceConfig service)
    {
        return Services.TryGetValue(name, out service!);
    }

    /// <summary>
    /// Returns a merged view of <see cref="Services"/> and <see cref="DisabledServices"/>.
    /// If both contain a service with the same name, the enabled service takes precedence.
    /// </summary>
    /// <returns>A new <see cref="Services"/> containing all services.</returns>
    public Services AllServices()
    {
        var all = new Services();
        foreach (var kvp in DisabledServices)
        {
            all[kvp.Key] = kvp.Value;
        }

        // Enabled services override disabled ones with the same name.
        foreach (var kvp in Services)
        {
            all[kvp.Key] = kvp.Value;
        }

        return all;
    }

    /// <summary>
    /// Returns a new <see cref="Project"/> with only services matching the active profiles.
    /// A service matches if it has no profiles defined, or if at least one of its profiles
    /// is in the given <paramref name="profiles"/> set.
    /// Services that do not match are moved to <see cref="DisabledServices"/>.
    /// </summary>
    /// <param name="profiles">The set of active profile names.</param>
    /// <returns>A new <see cref="Project"/> with profile filtering applied.</returns>
    public Project WithProfiles(IEnumerable<string> profiles)
    {
        var activeProfiles = new HashSet<string>(profiles, StringComparer.Ordinal);
        var enabled = new Services();
        var disabled = new Services(DisabledServices);
        var isWildcard = activeProfiles.Contains("*");

        foreach (var kvp in Services)
        {
            var svc = kvp.Value;
            if (svc.Profiles == null || svc.Profiles.Count == 0 || isWildcard || svc.Profiles.Any(p => activeProfiles.Contains(p)))
            {
                enabled[kvp.Key] = svc;
            }
            else
            {
                disabled[kvp.Key] = svc;
            }
        }

        return ShallowCopyWith(enabled, disabled, activeProfiles.ToList());
    }

    /// <summary>
    /// Returns a new <see cref="Project"/> restricted to the selected services and,
    /// optionally, their transitive dependencies.
    /// </summary>
    /// <param name="names">The names of the services to select.</param>
    /// <param name="includeDependencies">
    /// When <c>true</c>, transitive dependencies of the selected services are also included.
    /// </param>
    /// <returns>A new <see cref="Project"/> containing only the selected services.</returns>
    /// <exception cref="ComposeNotFoundException">A requested service name does not exist.</exception>
    public Project WithSelectedServices(IEnumerable<string> names, bool includeDependencies = true)
    {
        var requested = new HashSet<string>(names, StringComparer.Ordinal);
        if (requested.Count == 0)
            return ShallowCopyWith(new Services(Services), new Services(DisabledServices), Profiles);

        // Validate that all requested names exist.
        foreach (var name in requested)
        {
            if (!Services.ContainsKey(name))
                throw new ComposeNotFoundException($"No such service: '{name}'.");
        }

        var selected = new HashSet<string>(requested, StringComparer.Ordinal);
        if (includeDependencies)
        {
            ResolveDependencies(selected);
        }

        var enabled = new Services();
        var disabled = new Services(DisabledServices);

        foreach (var kvp in Services)
        {
            if (selected.Contains(kvp.Key))
            {
                enabled[kvp.Key] = kvp.Value;
            }
            else
            {
                disabled[kvp.Key] = kvp.Value;
            }
        }

        return ShallowCopyWith(enabled, disabled, Profiles);
    }

    /// <summary>
    /// Returns a new <see cref="Project"/> with only resources (networks, volumes, secrets, configs)
    /// that are actually referenced by the active services. Unreferenced resources are removed.
    /// </summary>
    /// <returns>A new <see cref="Project"/> with unreferenced resources removed.</returns>
    public Project WithoutUnnecessaryResources()
    {
        var usedNetworks = new HashSet<string>(StringComparer.Ordinal);
        var usedVolumes = new HashSet<string>(StringComparer.Ordinal);
        var usedSecrets = new HashSet<string>(StringComparer.Ordinal);
        var usedConfigs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kvp in Services)
        {
            var svc = kvp.Value;

            // Collect referenced networks.
            if (svc.Networks != null)
            {
                foreach (var netName in svc.Networks.Keys)
                {
                    usedNetworks.Add(netName);
                }
            }

            // Collect referenced volumes (named volumes only).
            if (svc.Volumes != null)
            {
                foreach (var vol in svc.Volumes)
                {
                    if (vol.Type == VolumeTypes.Volume && !string.IsNullOrEmpty(vol.Source))
                    {
                        usedVolumes.Add(vol.Source);
                    }
                }
            }

            // Collect referenced secrets.
            if (svc.Secrets != null)
            {
                foreach (var secret in svc.Secrets)
                {
                    usedSecrets.Add(secret.Source);
                }
            }

            // Collect referenced configs.
            if (svc.Configs != null)
            {
                foreach (var cfg in svc.Configs)
                {
                    usedConfigs.Add(cfg.Source);
                }
            }
        }

        var filteredNetworks = new Networks();
        foreach (var kvp in Networks)
        {
            if (usedNetworks.Contains(kvp.Key))
                filteredNetworks[kvp.Key] = kvp.Value;
        }

        var filteredVolumes = new Volumes();
        foreach (var kvp in Volumes)
        {
            if (usedVolumes.Contains(kvp.Key))
                filteredVolumes[kvp.Key] = kvp.Value;
        }

        var filteredSecrets = new Secrets();
        foreach (var kvp in Secrets)
        {
            if (usedSecrets.Contains(kvp.Key))
                filteredSecrets[kvp.Key] = kvp.Value;
        }

        var filteredConfigs = new Configs();
        foreach (var kvp in Configs)
        {
            if (usedConfigs.Contains(kvp.Key))
                filteredConfigs[kvp.Key] = kvp.Value;
        }

        return new Project
        {
            Name = Name,
            WorkingDir = WorkingDir,
            Services = new Services(Services),
            Networks = filteredNetworks,
            Volumes = filteredVolumes,
            Secrets = filteredSecrets,
            Configs = filteredConfigs,
            Models = new Models(Models),
            Extensions = Extensions,
            ComposeFiles = new List<string>(ComposeFiles),
            Environment = Environment,
            DisabledServices = new Services(DisabledServices),
            Profiles = new List<string>(Profiles),
        };
    }

    /// <summary>
    /// Iterates services in dependency order, calling <paramref name="fn"/> for each.
    /// Services are visited such that all dependencies of a service are visited before
    /// the service itself (topological order).
    /// </summary>
    /// <param name="names">
    /// The service names to iterate. If empty, all services in the project are iterated.
    /// </param>
    /// <param name="fn">The action to invoke for each service (name, config).</param>
    /// <param name="includeDependencies">
    /// When <c>true</c>, transitive dependencies of the named services are also included.
    /// </param>
    /// <exception cref="InvalidOperationException">A dependency cycle is detected.</exception>
    public void ForEachService(IEnumerable<string> names, Action<string, ServiceConfig> fn, bool includeDependencies = true)
    {
        var requested = new HashSet<string>(names, StringComparer.Ordinal);
        if (requested.Count == 0)
        {
            requested = new HashSet<string>(Services.Keys, StringComparer.Ordinal);
        }

        if (includeDependencies)
        {
            ResolveDependencies(requested);
        }

        // Topological sort using DFS.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var name in requested)
        {
            TopologicalVisit(name, requested, visited, visiting, order);
        }

        foreach (var name in order)
        {
            if (Services.TryGetValue(name, out var svc))
            {
                fn(name, svc);
            }
        }
    }

    /// <summary>
    /// Resolves a path relative to the project's working directory.
    /// If the path is already absolute, it is returned as-is.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The resolved absolute path.</returns>
    public string RelativePath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.GetFullPath(Path.Combine(WorkingDir, path));
    }

    /// <summary>
    /// Returns the names of services that depend on the given service.
    /// </summary>
    /// <param name="service">The service to find dependents for.</param>
    /// <returns>A sorted list of service names that depend on the given service.</returns>
    public List<string> GetDependentsForService(ServiceConfig service)
    {
        var dependents = new List<string>();
        var targetName = FindServiceName(service);
        if (targetName == null)
            return dependents;

        foreach (var kvp in Services)
        {
            if (kvp.Value.DependsOn == null)
                continue;

            if (kvp.Value.DependsOn.ContainsKey(targetName))
            {
                dependents.Add(kvp.Key);
            }
        }

        dependents.Sort(StringComparer.Ordinal);
        return dependents;
    }

    #region Private helpers

    /// <summary>
    /// Finds the key name for a <see cref="ServiceConfig"/> instance within the
    /// <see cref="Services"/> dictionary by reference equality.
    /// </summary>
    private string? FindServiceName(ServiceConfig service)
    {
        foreach (var kvp in Services)
        {
            if (ReferenceEquals(kvp.Value, service))
                return kvp.Key;
        }

        // Fall back to Name property.
        if (!string.IsNullOrEmpty(service.Name))
        {
            foreach (var kvp in Services)
            {
                if (string.Equals(kvp.Value.Name, service.Name, StringComparison.Ordinal))
                    return kvp.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves transitive dependencies for the given set of service names,
    /// adding all dependency names into <paramref name="selected"/>.
    /// </summary>
    private void ResolveDependencies(HashSet<string> selected)
    {
        var queue = new Queue<string>(selected);
        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!Services.TryGetValue(name, out var svc))
                continue;

            if (svc.DependsOn == null)
                continue;

            foreach (var depName in svc.DependsOn.Keys)
            {
                if (selected.Add(depName))
                {
                    queue.Enqueue(depName);
                }
            }
        }
    }

    /// <summary>
    /// Performs a DFS visit for topological sorting.
    /// </summary>
    /// <exception cref="ComposeInvalidException">A dependency cycle is detected.</exception>
    private void TopologicalVisit(
        string name,
        HashSet<string> scope,
        HashSet<string> visited,
        HashSet<string> visiting,
        List<string> order)
    {
        if (visited.Contains(name))
            return;

        if (visiting.Contains(name))
            throw new ComposeInvalidException($"Dependency cycle detected involving service '{name}'.");

        visiting.Add(name);

        if (Services.TryGetValue(name, out var svc) && svc.DependsOn != null)
        {
            foreach (var depName in svc.DependsOn.Keys)
            {
                if (scope.Contains(depName))
                {
                    TopologicalVisit(depName, scope, visited, visiting, order);
                }
            }
        }

        visiting.Remove(name);
        visited.Add(name);
        order.Add(name);
    }

    /// <summary>
    /// Creates a shallow copy of this project with the specified services and profiles.
    /// </summary>
    private Project ShallowCopyWith(Services services, Services disabled, List<string> profiles)
    {
        return new Project
        {
            Name = Name,
            WorkingDir = WorkingDir,
            Services = services,
            Networks = Networks,
            Volumes = Volumes,
            Secrets = Secrets,
            Configs = Configs,
            Models = Models,
            Extensions = Extensions,
            ComposeFiles = new List<string>(ComposeFiles),
            Environment = Environment,
            DisabledServices = disabled,
            Profiles = new List<string>(profiles),
        };
    }

    #endregion
}
