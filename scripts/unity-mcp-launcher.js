#!/usr/bin/env node
/**
 * Cross-platform launcher for the Unity MCP relay binary.
 *
 * The relay binary is installed by Unity's com.unity.ai.assistant package to
 * ~/.unity/relay/ when the Unity Editor starts. The binary name differs per OS:
 *   - Windows:  relay_win.exe
 *   - macOS ARM: relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64
 *   - macOS x64: relay_mac_x64.app/Contents/MacOS/relay_mac_x64
 *   - Linux:     relay_linux
 *
 * This script detects the OS, locates the correct binary, and spawns it with
 * --mcp so any repo-level MCP client config can point to a single entry point
 * that works for every developer regardless of platform.
 *
 * Usage in MCP client config:
 *   { "command": "node", "args": ["scripts/unity-mcp-launcher.js"] }
 */
'use strict';

const { spawn } = require('child_process');
const path = require('path');
const os = require('os');
const fs = require('fs');

const relayDir = path.join(os.homedir(), '.unity', 'relay');

function getRelayPath() {
  const platform = process.platform;
  const arch = process.arch;

  if (platform === 'win32') {
    return path.join(relayDir, 'relay_win.exe');
  }

  if (platform === 'darwin') {
    const binaryName = arch === 'arm64' ? 'relay_mac_arm64' : 'relay_mac_x64';
    return path.join(relayDir, `${binaryName}.app`, 'Contents', 'MacOS', binaryName);
  }

  if (platform === 'linux') {
    return path.join(relayDir, 'relay_linux');
  }

  throw new Error(`Unsupported platform: ${platform}`);
}

function main() {
  let relayPath;
  try {
    relayPath = getRelayPath();
  } catch (err) {
    console.error(`[unity-mcp-launcher] ${err.message}`);
    process.exit(1);
  }

  if (!fs.existsSync(relayPath)) {
    console.error(
      `[unity-mcp-launcher] Relay binary not found at: ${relayPath}\n` +
      `[unity-mcp-launcher] Open this project in Unity (with com.unity.ai.assistant installed) at least once to install the relay binary.`
    );
    process.exit(1);
  }

  const child = spawn(relayPath, ['--mcp'], {
    stdio: ['inherit', 'inherit', 'inherit'],
  });

  child.on('error', (err) => {
    console.error(`[unity-mcp-launcher] Failed to start relay: ${err.message}`);
    process.exit(1);
  });

  child.on('exit', (code, signal) => {
    process.exit(code ?? (signal ? 128 + 1 : 1));
  });

  process.on('SIGINT', () => child.kill('SIGINT'));
  process.on('SIGTERM', () => child.kill('SIGTERM'));
}

main();
