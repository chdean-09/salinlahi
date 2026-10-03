# Unity MCP — Repo-Level Setup

This repo includes repo-level MCP configuration so any developer can connect VS Code Copilot to the Unity Editor via the official Unity MCP server.

## How it works

```
VS Code Copilot
    │  MCP protocol (stdio)
    ▼
scripts/unity-mcp-launcher.js   ← cross-platform launcher (this repo)
    │  spawns relay binary with --mcp
    ▼
Relay binary (~/.unity/relay/)  ← installed by Unity on first run
    │  IPC (named pipe / Unix socket)
    ▼
Unity Editor (MCP Bridge)       ← shipped by com.unity.ai.assistant
    │  McpToolRegistry
    ▼
Unity tools (scene mgmt, asset ops, script editing, console access, ...)
```

The `com.unity.ai.assistant` package (already in `Packages/manifest.json`) ships Unity's official MCP server. When Unity starts, it installs a relay binary to `~/.unity/relay/` and launches an MCP bridge inside the editor. The relay binary is what AI clients connect to.

Because the relay binary path differs per OS (Windows, macOS ARM, macOS x64, Linux), this repo includes `scripts/unity-mcp-launcher.js` — a cross-platform Node.js wrapper that auto-detects the OS and spawns the correct relay binary with `--mcp`. The repo-level MCP config (`.vscode/mcp.json`) points to this single script.

## Prerequisites

1. **Unity 6 (6000.0) or later** — this project uses 6000.0.76f1.
2. **Node.js 18+** — required to run the launcher script. Verify with `node --version`.
3. **VS Code with the GitHub Copilot extension** (MCP support enabled).

## Setup

### Step 1: Open the project in Unity (one-time)

Open this project in Unity at least once with the `com.unity.ai.assistant` package installed. This installs the relay binary to `~/.unity/relay/`.

Verify the bridge is running:
- Go to **Edit > Project Settings > AI > Unity MCP Server**
- Confirm **Unity Bridge** status shows **Running** (green)

### Step 2: Open the project in VS Code

Open the project in VS Code with the GitHub Copilot extension installed. VS Code auto-detects `.vscode/mcp.json`, which is already committed to the repo and points to `scripts/unity-mcp-launcher.js`.

### Step 3: Approve the connection in Unity

When VS Code Copilot connects for the first time, Unity shows a **Pending Connection**:
1. Go to **Edit > Project Settings > AI > Unity MCP Server**
2. Under **Pending Connections**, find the client
3. Select **Accept**

Previously approved clients reconnect automatically.

### Step 4: Verify

In VS Code Copilot chat, try:
```
Read the Unity console messages and summarize any warnings or errors.
```

The client should call the `Unity_GetConsoleLogs` MCP tool and return Unity's console output.

## Available tools

Unity MCP ships with **51 built-in tools**, but only **7 are enabled by default**. The remaining 44 must be turned on manually.

### Enabled by default (7 tools)

| Tool | Group | Purpose |
|---|---|---|
| `Unity.RunCommand` | Core | Compile & execute C# in the Unity Editor |
| `Unity.GetConsoleLogs` | Debug | Read Unity console logs (messages, warnings, errors, stack traces) |
| `Unity.Camera.Capture` | Editor | Render an image from a scene camera by GameObject instance ID |
| `Unity.SceneView.Capture2DScene` | Editor | Capture a rectangular region of a 2D scene (orthographic) |
| `Unity.SceneView.CaptureMultiAngleSceneView` | Editor | Capture a 4-angle grid view of the current Scene View |
| `Unity.AssetGeneration.GenerateAsset` | Assets | AI asset generation (**consumes Unity Credits**) |
| `Unity.AssetGeneration.GetModels` | Assets | List available AI generation models |

### Enabling additional tools

The default 7 tools cover console reading, scene capture, and AI asset generation. For full Editor control (scene management, GameObject manipulation, script editing, asset operations), enable the additional tools:

1. In Unity, go to **Edit > Project Settings > AI > Unity MCP Server**
2. Click the **Tools** tab
3. Check the box next to each tool you want to enable
4. Restart the MCP connection in your AI client (disconnect and reconnect in VS Code, or restart VS Code)

### Recommended tools to enable for development

These are the most useful additional tools for day-to-day Unity development:

| Tool | Group | Purpose |
|---|---|---|
| `Unity.ManageScene` | Core | Load, save, create scenes, query hierarchy, manage build settings |
| `Unity.ManageGameObject` | Core | Create, modify, delete GameObjects; add/remove components; set properties |
| `Unity.CreateScript` | Core | Create a new C# script at a specified path |
| `Unity.DeleteScript` | Core | Delete a C# script by URI or path |
| `Unity.ManageScript` | Core | Read/modify existing C# scripts |
| `Unity.ManageAsset` | Assets | Import, export, create, delete, and modify assets |

### Full tool groups (51 tools total)

| Group | Count | Examples |
|---|---|---|
| Core | 18 | ManageScene, ManageGameObject, CreateScript, DeleteScript, ManageScript, RunCommand |
| Assets | 12 | ManageAsset, AssetGeneration.GenerateAsset, AssetGeneration.GetModels |
| Assistant | 1 | Assistant integration tool |
| Debug & Diagnostics | 14 | GetConsoleLogs, Profiler tools, build diagnostics |
| Editor | 7 | Camera.Capture, SceneView.Capture2DScene, SceneView.CaptureMultiAngleSceneView |

> **Note:** `AssetGeneration.GenerateAsset` and `AssetGeneration.GetModels` are enabled by default but **consume Unity Credits**. Disable them if you don't need AI asset generation.
> **Note:** Profiler tools require captured data in the Unity Profiler window to return meaningful results.

## Troubleshooting

**"Relay binary not found"**
The relay binary hasn't been installed yet. Open the project in Unity once with `com.unity.ai.assistant` installed. The binary lands at:
- Windows: `%USERPROFILE%\.unity\relay\relay_win.exe`
- macOS (ARM): `~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64`
- macOS (x64): `~/.unity/relay/relay_mac_x64.app/Contents/MacOS/relay_mac_x64`
- Linux: `~/.unity/relay/relay_linux`

**"Unity Bridge status: Stopped"**
In Unity, go to **Edit > Project Settings > AI > Unity MCP Server** and click **Start**.

**Client connects but no tools appear**
Make sure you approved the pending connection in Unity (Step 3). Check that tools are enabled in the Unity MCP Server settings page (**Edit > Project Settings > AI > Unity MCP Server > Tools** tab). Only 7 tools are enabled by default — see [Available tools](#available-tools) above for enabling more.

**Node.js not installed**
Install Node.js 18+ from https://nodejs.org/ and verify with `node --version`.

---

_Relocated from AGENTS.md during the rebase onto dev. That filename is now the repository's agent-instruction file; this Unity MCP setup guide lives here so both documents survive._
