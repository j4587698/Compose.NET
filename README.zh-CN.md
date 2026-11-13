# Compose.NET

[![NuGet](https://img.shields.io/nuget/v/Compose.NET.svg)](https://www.nuget.org/packages/Compose.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Compose.NET.svg)](https://www.nuget.org/packages/Compose.NET)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

[English](README.md) | 简体中文

Compose.NET 是 [compose-go](https://github.com/compose-spec/compose-go) 的 .NET 移植实现。
它用于解析和加载 Docker Compose 文件，并映射为强类型 C# 模型，不依赖 Docker Engine。

## 项目链接

- GitHub: https://github.com/j4587698/Compose.NET
- NuGet: https://www.nuget.org/packages/Compose.NET

## 适用场景

- 需要在 .NET 工具、服务或 CI 流程中解析 Compose 文件。
- 需要与 compose-go 功能行为保持一致，同时使用 C# API。
- 只想要解析能力，不希望引入 Docker 运行时依赖。

## 功能特性

- 解析流水线对齐 compose-go（插值、合并、include、extends、校验、归一化）。
- 强类型模型（`Project`、`ServiceConfig`、`NetworkConfig`、`VolumeConfig` 等）。
- 支持 Compose override/merge 语义和 canonical transform。
- 内置 `.env` 解析与变量展开。
- 支持跨文件 include / extends。
- 兼容性导向的 validation 与 consistency check。
- 无 Docker Engine 依赖。

## 目标框架

- `netstandard2.1`
- `net8.0`
- `net9.0`
- `net10.0`
- `net11.0`

## 安装

```bash
dotnet add package Compose.NET
```

## 快速开始

```csharp
using Compose.NET.Loader;

var result = ComposeLoader.LoadFromFile("compose.yaml");
var project = result.Project;

foreach (var (name, service) in project.Services)
{
    Console.WriteLine($"{name}: {service.Image}");
}
```

## 从 YAML 字符串加载

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

## `.env` 解析示例

```csharp
using Compose.NET.DotEnv;

var env = EnvFileParser.ParseWithLookup("FOO=bar\\nBAZ=${FOO}_suffix");
Console.WriteLine(env["BAZ"]); // bar_suffix
```

## 兼容性说明

- 项目目标是与 compose-go 在功能行为上保持一致。
- 错误文案和代码风格遵循 .NET 习惯，不强制与 Go 文案逐字一致。

## 许可证

Apache License 2.0。
