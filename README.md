# Compose.NET

[![NuGet](https://img.shields.io/nuget/v/Compose.NET.svg)](https://www.nuget.org/packages/Compose.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Compose.NET.svg)](https://www.nuget.org/packages/Compose.NET)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

English | [简体中文](https://github.com/j4587698/Compose.NET/blob/main/README.zh-CN.md)

Compose.NET is a .NET port of [compose-go](https://github.com/compose-spec/compose-go).
It parses and loads Docker Compose files into strongly-typed C# models, without requiring Docker Engine.

## Project Links

- GitHub: https://github.com/j4587698/Compose.NET
- NuGet: https://www.nuget.org/packages/Compose.NET

## Why This Project

- You need Compose file parsing in .NET tools, services, or CI automation.
- You want behavior aligned with compose-go, but with idiomatic C# APIs.
- You want a pure library dependency (no Docker runtime dependency).

## Features

- Compose parsing pipeline aligned with compose-go (interpolation, merge, include, extends, validation, normalization).
- Strongly-typed domain models (`Project`, `ServiceConfig`, `NetworkConfig`, `VolumeConfig`, etc.).
- Compose override/merge behavior and canonical transform support.
- `.env` parser with variable expansion support.
- Cross-file include/extends resolution.
- Compatibility-oriented validation and consistency checks.
- No Docker Engine dependency.

## Target Frameworks

- `netstandard2.1`
- `net8.0`
- `net9.0`
- `net10.0`
- `net11.0`

## Installation

```bash
dotnet add package Compose.NET
```

## Quick Start

```csharp
using Compose.NET.Loader;

var result = ComposeLoader.LoadFromFile("compose.yaml");
var project = result.Project;

foreach (var (name, service) in project.Services)
{
    Console.WriteLine($"{name}: {service.Image}");
}
```

## Load from YAML Content

```csharp
using Compose.NET.Loader;

var yaml = """
services:
  web:
    image: nginx:latest
    ports:
      - "8080:80"
""";

var result = ComposeLoader.LoadFromContent(yaml);
Console.WriteLine(result.Project.Services["web"].Image);
```

## Parse `.env`

```csharp
using Compose.NET.DotEnv;

var env = EnvFileParser.ParseWithLookup("FOO=bar\\nBAZ=${FOO}_suffix");
Console.WriteLine(env["BAZ"]); // bar_suffix
```

## Notes on Compatibility

- The project is focused on functional behavior parity with compose-go.
- Error message text and code style intentionally follow .NET conventions.

## License

Apache License 2.0.
