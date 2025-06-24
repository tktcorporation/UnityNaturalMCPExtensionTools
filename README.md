# Unity Natural MCP Extension Tools

Custom MCP (Model Context Protocol) tools that extend [Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP) server capabilities for comprehensive Unity Editor automation.

## Overview

This package provides a collection of custom MCP tools designed to enhance the Unity Natural MCP server with additional Unity Editor operation capabilities. It extends the base functionality provided by Unity Natural MCP to enable more comprehensive automation workflows.

## Base Project

This is an extension of [Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP) by notargs, which provides MCP server integration for Unity Editor.

## Features

### 🎯 Scene Object Management
- Create empty GameObjects and primitives
- Duplicate and delete objects
- Set parent-child relationships
- Transform property manipulation

### 🎨 Material Operations
- Create materials with custom shaders
- Set material properties (color, metallic, smoothness, emission)
- Assign materials to renderers
- List and filter materials

### 📷 Scene Capture
- Capture Unity scene views to PNG images
- Custom camera positioning and resolution settings
- Automatic timestamp-based file naming
- List and manage captured screenshots

### 🔧 Component Properties
- Configure renderer properties
- Set up colliders (Box, Sphere, Capsule)
- Configure audio sources
- Add components dynamically

### ✨ Particle Systems
- Create particle systems
- Configure main module properties
- Set up emission and shape modules
- Control playback

### 📦 Asset Management
- Create folders
- Convert GameObjects to prefabs
- Instantiate prefabs
- List and delete assets

## Installation

1. Ensure you have [Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP) installed and configured
2. Add this package to your Unity project via Package Manager
3. The tools will be automatically registered when the MCP server starts

## Tool Categories

| Category | Tools | Methods |
|----------|-------|---------|
| Object Operations | McpUnifiedObjectTool | 5 methods |
| Asset & Material Management | McpUnifiedAssetTool | 5 methods |
| Effect Systems | McpUnifiedEffectTool | 2 methods |
| Scene Capture | McpSceneCaptureTool | 2 methods |
| **Total** | **4 Tools** | **14 Methods** |

## Requirements

- Unity 2021.3 or later
- Unity Natural MCP server
- UniTask package
- Model Context Protocol SDK

## Implementation Details

Each tool follows a consistent pattern:
- Decorated with `[McpServerToolType]` attribute
- Methods decorated with `[McpServerTool]` attribute
- Async operations using UniTask
- Proper error handling and logging
- Main thread synchronization for Unity API calls

## License

MIT License - This project extends Unity Natural MCP which is also licensed under MIT.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## Acknowledgments

- [notargs](https://github.com/notargs) for creating Unity Natural MCP
- Unity Technologies for the Unity Editor API
- Anthropic for the Model Context Protocol

## Support

For issues specific to these extension tools, please open an issue in this repository.
For Unity Natural MCP core functionality, please refer to the [original repository](https://github.com/notargs/UnityNaturalMCP).