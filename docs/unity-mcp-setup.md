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
- Go to **Edit > Project Settings > AI > Unity MCP**
- Confirm **Unity Bridge** status shows **Running** (green)

### Step 2: Open the project in VS Code

Open the project in VS Code with the GitHub Copilot extension installed. VS Code auto-detects `.vscode/mcp.json`, which is already committed to the repo and points to `scripts/unity-mcp-launcher.js`.

### Step 3: Approve the connection in Unity

When VS Code Copilot connects for the first time, Unity shows a **Pending Connection**:
1. Go to **Edit > Project Settings > AI > Unity MCP**
2. Under **Pending Connections**, find the client
3. Select **Accept**

Previously approved clients reconnect automatically.

### Step 4: Verify

In VS Code Copilot chat, try:
```
Read the Unity console messages and summarize any warnings or errors.
```

The client should call the `Unity_ReadConsole` MCP tool and return Unity's console output.

## Troubleshooting

**"Relay binary not found"**
The relay binary hasn't been installed yet. Open the project in Unity once with `com.unity.ai.assistant` installed. The binary lands at:
- Windows: `%USERPROFILE%\.unity\relay\relay_win.exe`
- macOS (ARM): `~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64`
- macOS (x64): `~/.unity/relay/relay_mac_x64.app/Contents/MacOS/relay_mac_x64`
- Linux: `~/.unity/relay/relay_linux`

**"Unity Bridge status: Stopped"**
In Unity, go to **Edit > Project Settings > AI > Unity MCP** and click **Start**.

**Client connects but no tools appear**
Make sure you approved the pending connection in Unity (Step 3). Check that tools are enabled in the Unity MCP settings page (tool list with enable/disable toggles).

**Node.js not installed**
Install Node.js 18+ from https://nodejs.org/ and verify with `node --version`.

---

_Relocated from AGENTS.md during the rebase onto dev. That filename is now the repository's agent-instruction file; this Unity MCP setup guide lives here so both documents survive._
