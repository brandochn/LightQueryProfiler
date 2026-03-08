# Troubleshooting Guide - Light Query Profiler VS Code Extension

This guide helps resolve common issues when debugging and running the Light Query Profiler extension in VS Code.

## Issue: "Show SQL Profiler" Command Does Nothing

### Symptoms
- You press F5 to debug the extension in VS Code
- The extension host window opens
- You run the command "Show SQL Profiler" (Ctrl+Shift+P)
- Nothing happens - the profiler view doesn't open

### Root Causes

This issue has **three main causes**:

#### 1. Missing Server DLL (Most Common)
The .NET JSON-RPC server DLL is not compiled or not in the expected location.

**Solution:**
```powershell
# From the root LightQueryProfiler directory:
dotnet publish src/LightQueryProfiler.JsonRpc/LightQueryProfiler.JsonRpc.csproj -c Release -o vscode-extension/bin
```

**Verification:**
Check that these files exist:
- `vscode-extension/bin/LightQueryProfiler.JsonRpc.dll`
- `vscode-extension/bin/LightQueryProfiler.JsonRpc.exe`
- `vscode-extension/bin/LightQueryProfiler.Shared.dll`

#### 2. Missing Icon Files
The extension references icon files that don't exist, which can cause activation failures.

**Solution:**
Ensure these files exist:
- `vscode-extension/media/icon.svg` (required for view container)
- `vscode-extension/media/icon.png` (optional, for marketplace)

The `icon.svg` file is created automatically by the setup process.

#### 3. TypeScript Not Compiled
The extension's TypeScript code hasn't been compiled to JavaScript.

**Solution:**
```powershell
# From vscode-extension directory:
cd vscode-extension
npm install
npm run compile
```

**Verification:**
Check that `vscode-extension/dist/extension.js` exists and is recent.

---

## Complete Setup Checklist

Before debugging the extension, ensure all these steps are completed:

### 1. Install Dependencies
```powershell
# .NET dependencies (from root)
dotnet restore

# Node.js dependencies (from vscode-extension)
cd vscode-extension
npm install
cd ..
```

### 2. Build the Server
```powershell
# Publish the JSON-RPC server to the extension's bin folder
dotnet publish src/LightQueryProfiler.JsonRpc/LightQueryProfiler.JsonRpc.csproj -c Release -o vscode-extension/bin
```

### 3. Compile TypeScript
```powershell
# Compile the extension's TypeScript code
cd vscode-extension
npm run compile
cd ..
```

### 4. Verify Files Exist
Check that these critical files are present:
- ✅ `vscode-extension/bin/LightQueryProfiler.JsonRpc.dll`
- ✅ `vscode-extension/bin/LightQueryProfiler.JsonRpc.exe`
- ✅ `vscode-extension/media/icon.svg`
- ✅ `vscode-extension/dist/extension.js`

### 5. Debug in VS Code
1. Open the `vscode-extension` folder in VS Code
2. Press F5 to start debugging
3. In the Extension Host window, press Ctrl+Shift+P
4. Type "Show SQL Profiler" and press Enter
5. The profiler view should appear in the sidebar

---

## Checking Logs

### Output Channel
When debugging, the extension writes logs to an Output Channel:

1. In the Extension Host window, go to **View > Output**
2. Select **Light Query Profiler** from the dropdown
3. Check for error messages

### Common Log Messages

**Success:**
```
Light Query Profiler extension is activating...
Server DLL path: c:\...\vscode-extension\bin\LightQueryProfiler.JsonRpc.dll
dotnet path: dotnet
Light Query Profiler extension activated successfully
Show SQL Profiler command executed
```

**Server DLL Not Found:**
```
Light Query Profiler extension is activating...
ERROR: Light Query Profiler server not found. Please ensure the extension is properly installed.
```
➡️ **Fix:** Run the publish command (see issue #1 above)

**Extension Activation Error:**
```
ERROR during activation: <error message>
```
➡️ **Fix:** Check the specific error message and verify all setup steps

---

## Testing the Server Independently

You can test the JSON-RPC server separately from the extension:

```powershell
# Navigate to the published server directory
cd vscode-extension/bin

# Run the server (it will wait for JSON-RPC input on stdin)
dotnet LightQueryProfiler.JsonRpc.dll
```

If the server starts without errors, it should wait for input. Press Ctrl+C to exit.

---

## Debugging Tips

### 1. Watch Mode for TypeScript
During development, use watch mode to automatically recompile on changes:

```powershell
cd vscode-extension
npm run watch
```

### 2. Clean and Rebuild
If you encounter strange issues, try a clean rebuild:

```powershell
# Clean .NET build artifacts
dotnet clean

# Remove old published files
Remove-Item -Recurse -Force vscode-extension/bin/*
Remove-Item -Recurse -Force vscode-extension/dist/*

# Rebuild everything
dotnet publish src/LightQueryProfiler.JsonRpc/LightQueryProfiler.JsonRpc.csproj -c Release -o vscode-extension/bin
cd vscode-extension
npm run compile
cd ..
```

### 3. Check .NET Installation
Verify that .NET 10 (or compatible version) is installed:

```powershell
dotnet --version
```

Expected output: `10.0.x` or higher

### 4. Breakpoints in Extension Code
Set breakpoints in `vscode-extension/src/extension.ts`:
- Line where `activate()` is called
- Line where the command is registered
- Line where the view provider is registered

### 5. Check VS Code Version
Ensure VS Code version is 1.85.0 or higher:
1. Help > About
2. Check version number

---

## Common Error Messages

### "Cannot find module './views/profiler-view-provider.js'"
**Cause:** TypeScript not compiled
**Fix:** Run `npm run compile` in vscode-extension folder

### "spawn dotnet ENOENT"
**Cause:** .NET is not installed or not in PATH
**Fix:** Install .NET 10 SDK from https://dotnet.microsoft.com/download

### "ENOENT: no such file or directory, open '...LightQueryProfiler.JsonRpc.dll'"
**Cause:** Server DLL not published
**Fix:** Run the publish command (see issue #1)

### Extension activates but view doesn't appear
**Cause:** Icon file missing or view container misconfigured
**Fix:** 
1. Check `media/icon.svg` exists
2. Verify `package.json` has correct viewsContainers and views sections

---

## Quick Fix Script

Create a file `setup-extension.ps1` in the root directory:

```powershell
# setup-extension.ps1
# Quick setup script for the VS Code extension

Write-Host "Setting up Light Query Profiler VS Code Extension..." -ForegroundColor Green

# Build and publish server
Write-Host "`n1. Publishing JSON-RPC server..." -ForegroundColor Yellow
dotnet publish src/LightQueryProfiler.JsonRpc/LightQueryProfiler.JsonRpc.csproj -c Release -o vscode-extension/bin

# Install npm dependencies
Write-Host "`n2. Installing npm dependencies..." -ForegroundColor Yellow
Push-Location vscode-extension
npm install

# Compile TypeScript
Write-Host "`n3. Compiling TypeScript..." -ForegroundColor Yellow
npm run compile

Pop-Location

Write-Host "`nSetup complete! Press F5 in VS Code to debug the extension." -ForegroundColor Green
```

Run it before debugging:
```powershell
.\setup-extension.ps1
```

---

## Still Having Issues?

1. Check the SETUP.md file for detailed setup instructions
2. Review the IMPLEMENTATION.md for architecture details
3. Check the Output panel in VS Code for error messages
4. Verify all dependencies are installed (see package.json and .csproj files)
5. Try restarting VS Code after making changes

---

## Related Files

- `SETUP.md` - Initial setup instructions
- `IMPLEMENTATION.md` - Technical implementation details
- `README.md` - General project information
- `.vscode/launch.json` - Debug configuration