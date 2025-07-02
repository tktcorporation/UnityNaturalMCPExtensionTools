# Unity Natural MCP Extension Tools

[日本語](README.ja.md)

Custom MCP (Model Context Protocol) tools that extend [Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP) server capabilities for comprehensive Unity Editor automation.

> [!WARNING]
> **Most of this repository has been created by Claude Code, so functionality cannot be guaranteed.**

## Base Project

This is an extension of [Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP) by notargs, which provides MCP server integration for Unity Editor.

**Unity Natural MCP must be installed for this to function**

## Features

### 🎯 Unified Object Management (McpUnifiedObjectTool)
- Create empty GameObjects, primitives, and prefab instances
- Transform, duplicate, delete objects, and set parent-child relationships
- Add and configure components (with JSON structured configuration support)
- Get detailed GameObject information
- List and filter scene objects

### 🎨 Unified Asset & Material Management (McpUnifiedAssetTool)
- Create and update materials (with JSON structured configuration support)
- Assign materials to renderers
- List and filter materials
- Create folders, create prefabs, delete assets
- List prefabs and get detailed prefab information

### ✨ Unified Effect Systems (McpUnifiedEffectTool)
- Comprehensive particle system configuration (with JSON structured configuration support)
- Control particle system playback (play/stop)
- Get detailed particle system information

### 📷 Scene Capture Functionality (McpSceneCaptureTool)
- Capture screenshots from scene view
- Capture screenshots from game view
- Capture prefabs in prefab edit mode
- List and manage captured screenshots

### 📦 Prefab Edit Mode Management (McpPrefabEditTool)
- Start, save, and exit prefab edit mode
- Check prefab editing status
- Apply prefab instance changes to source prefab
- Revert prefab instance changes to source state
- Display prefab instance change information and overrides

### 🏗️ Scene Management Functionality (McpSceneManagementTool)
- Create new scenes (supports Empty, 3D, 2D, UI templates)
- Save scenes and save with new names
- Load scenes (supports Single and Additive modes)
- List scenes in project
- Get active scene detailed information
- Close scenes (multi-scene support)

### ⚙️ Project Settings Management (McpProjectSettingsTool)
- Manage project layers (list, set names, remove)

### 🔧 Common Infrastructure System
- Unified error handling and logging
- Automatic Prefab mode/Scene mode detection
- Type-safe JSON configuration parsing and validation
- Automatic conversion of Unity-specific types (Vector, Color, Quaternion, etc.)
- Nested property setting (dot notation support)

## Requirements

Follows the requirements of [Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP/tree/main?tab=readme-ov-file#requirements)

## Installation

1. Ensure you have [Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP) installed and configured
2. Add this package to your Unity project via Package Manager
   
  ```
  https://github.com/sack-kazu/UnityNaturalMCPExtensionTools.git
  ```

3. The tools will be automatically registered when the MCP server starts

## License

MIT License - This project extends Unity Natural MCP which is also licensed under MIT.

## Acknowledgments

- [notargs](https://github.com/notargs) for creating Unity Natural MCP
- Unity Technologies for the Unity Editor API
- Anthropic for the Model Context Protocol
