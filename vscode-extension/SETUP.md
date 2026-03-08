# Setup Guide - Light Query Profiler VS Code Extension

This guide walks you through setting up the development environment and deploying the Light Query Profiler VS Code extension.

## 🚀 Quick Setup (Recommended)

If you just want to get started quickly, use the automated setup script:

```powershell
# From the root LightQueryProfiler directory
.\setup-extension.ps1
```

This script will:
- ✅ Check prerequisites (.NET SDK, Node.js, npm)
- ✅ Restore .NET dependencies
- ✅ Build and publish the JSON-RPC server
- ✅ Install npm dependencies
- ✅ Compile TypeScript
- ✅ Run tests
- ✅ Verify all required files exist

**Options:**
```powershell
# Clean build (removes old artifacts first)
.\setup-extension.ps1 -Clean

# Skip tests for faster setup
.\setup-extension.ps1 -SkipTests

# Verbose output for debugging
.\setup-extension.ps1 -Verbose

# Combine options
.\setup-extension.ps1 -Clean -Verbose
```

After the script completes:
1. Open the `vscode-extension` folder in VS Code
2. Press **F5** to start debugging
3. In the Extension Host window, press **Ctrl+Shift+P**
4. Type **"Show SQL Profiler"** and press Enter

**Troubleshooting:** If something goes wrong, see `vscode-extension/TROUBLESHOOTING.md` for detailed solutions.

---

## 📋 Manual Setup (Advanced)

If you prefer to set up manually or the script doesn't work, follow the steps below.

### Prerequisites

### Required Software

1. **Node.js** (v18.0.0 or higher)
   ```bash
   node --version
   # Should show: v18.x.x or higher
   ```

2. **.NET 10 SDK**
   ```bash
   dotnet --version
   # Should show: 10.0.x
   ```

3. **Visual Studio Code** (v1.85.0 or higher)
   ```bash
   code --version
   # Should show: 1.85.0 or higher
   ```

4. **SQL Server** or **Azure SQL Database**
   - SQL Server 2016+ (for on-premises)
   - Azure SQL Database (for cloud)

### Optional Tools

- **Git** - For version control
- **SSMS** or **Azure Data Studio** - For testing queries
- **vsce** - For packaging extensions (`npm install -g vsce`)

## 🚀 Manual Initial Setup

### Step 1: Clone the Repository

```bash
git clone https://github.com/your-repo/light-query-profiler.git
cd light-query-profiler
```

### Step 2: Install Node Dependencies

```bash
npm install
```

This will install:
- TypeScript compiler
- ESLint and related plugins
- vscode-jsonrpc
- VS Code extension testing tools
- Other dev dependencies

### Step 3: Build the .NET Backend Server

```powershell
# From the root LightQueryProfiler directory
dotnet publish src/LightQueryProfiler.JsonRpc/LightQueryProfiler.JsonRpc.csproj -c Release -o vscode-extension/bin
```

This creates the `LightQueryProfiler.JsonRpc.dll` in the `vscode-extension/bin/` folder.

**Verify the server DLL exists:**
```powershell
# Should show the DLL file
dir vscode-extension\bin\LightQueryProfiler.JsonRpc.dll
```

### Step 4: Compile TypeScript

```powershell
cd vscode-extension
npm run compile
```

This compiles TypeScript files from `src/` to JavaScript in `dist/`.

**Verify the extension was compiled:**
```powershell
# Should show the compiled extension
dir dist\extension.js
```

## 🔧 Development Workflow

### Running the Extension in Debug Mode

1. Open the `vscode-extension` folder in VS Code:
   ```bash
   code .
   ```

2. Press **F5** or go to **Run > Start Debugging**

3. A new "Extension Development Host" window will open

4. In the new window:
   - Press **Ctrl+Shift+P** (Windows/Linux) or **Cmd+Shift+P** (macOS)
   - Type: `Light Query Profiler: Show SQL Profiler`
   - Press Enter

5. The profiler panel should appear in the Activity Bar

### Making Changes

#### TypeScript Changes

1. Edit files in `src/`
2. Save the file
3. Run **Developer: Reload Window** in the Extension Development Host
   - Or restart debugging (Ctrl+Shift+F5)

#### For Continuous Compilation

In a terminal:
```bash
npm run watch
```

This automatically recompiles TypeScript on file changes.

#### .NET Server Changes

If you modify the JSON-RPC server:

1. Rebuild the server:
   ```bash
   cd ../src/LightQueryProfiler.JsonRpc
   dotnet publish -c Release -o ../../vscode-extension/bin
   cd ../../vscode-extension
   ```

2. Restart the extension debugging session

### Running Linter

```bash
npm run lint
```

Fix auto-fixable issues:
```bash
npm run lint -- --fix
```

## 🧪 Testing

### Manual Testing Checklist

1. **Connection Settings**
   - [ ] Windows Authentication works
   - [ ] SQL Server Authentication works
   - [ ] Azure SQL Database works
   - [ ] Validation errors show for missing fields
   - [ ] Credential fields hide/show based on auth mode

2. **Start Profiling**
   - [ ] Start button initiates profiling
   - [ ] Success message appears
   - [ ] Status indicator turns green
   - [ ] Buttons update (Start disabled, Stop/Pause enabled)

3. **Event Collection**
   - [ ] Events appear in real-time
   - [ ] Event counter increments
   - [ ] Table shows correct columns
   - [ ] Timestamps are formatted correctly

4. **Event Details**
   - [ ] Clicking row shows query text
   - [ ] Query details panel displays full SQL
   - [ ] Long queries are scrollable

5. **Controls**
   - [ ] Pause button stops polling
   - [ ] Resume button restarts polling
   - [ ] Stop button ends session
   - [ ] Clear button removes all events

6. **Error Handling**
   - [ ] Invalid connection shows error
   - [ ] Missing .NET SDK shows error
   - [ ] Connection failures show user-friendly messages

### Test with Real Database

1. Start the extension
2. Configure connection to your test database
3. Click Start
4. Execute queries in SSMS/Azure Data Studio:
   ```sql
   SELECT * FROM sys.tables;
   SELECT DB_NAME();
   ```
5. Verify events appear in the extension

## 📦 Packaging for Distribution

### Step 1: Prepare for Packaging

Ensure everything is built:
```bash
# Compile TypeScript
npm run compile

# Build .NET server
cd ../src/LightQueryProfiler.JsonRpc
dotnet publish -c Release -o ../../vscode-extension/bin
cd ../../vscode-extension
```

### Step 2: Update Version

Edit `package.json`:
```json
{
  "version": "1.0.0"
}
```

Update `CHANGELOG.md` with release notes.

### Step 3: Create VSIX Package

```bash
npm run package
```

This creates: `light-query-profiler-1.0.0.vsix`

### Step 4: Test the Package

Install the VSIX manually:

1. Open VS Code
2. Go to Extensions view (Ctrl+Shift+X)
3. Click `...` menu → `Install from VSIX...`
4. Select the `.vsix` file
5. Reload VS Code
6. Test the extension thoroughly

## 🚀 Publishing to Marketplace

### Prerequisites

1. Create a Microsoft account (if you don't have one)
2. Create an Azure DevOps organization
3. Get a Personal Access Token (PAT) with Marketplace scope

### Step 1: Create Publisher

```bash
vsce create-publisher your-publisher-name
```

### Step 2: Login

```bash
vsce login your-publisher-name
```

Enter your PAT when prompted.

### Step 3: Publish

```bash
vsce publish
```

Or publish a specific version:
```bash
vsce publish 1.0.1
```

### Step 4: Verify

- Go to https://marketplace.visualstudio.com/vscode
- Search for "Light Query Profiler"
- Verify the extension appears

## 🔐 Security Setup

### Secrets Management

**Never commit:**
- Connection strings
- Passwords
- Personal Access Tokens
- API keys

Add to `.gitignore`:
```
.env
secrets.json
*.key
*.pem
```

### CSP Headers

The webview already includes Content Security Policy headers:
```typescript
meta http-equiv="Content-Security-Policy" 
  content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; 
  script-src ${webview.cspSource} 'unsafe-inline';"
```

## 🐛 Troubleshooting

### Extension Won't Activate

**Check:**
1. .NET 10 is installed: `dotnet --version`
2. Server DLL exists: `ls bin/LightQueryProfiler.JsonRpc.dll`
3. Output channel for errors: View → Output → "Light Query Profiler"

### TypeScript Compilation Errors

```bash
# Clean and rebuild
rm -rf dist/
npm run compile
```

### .NET Server Not Found

```bash
# Verify server was published
ls -la bin/

# Re-publish
cd ../src/LightQueryProfiler.JsonRpc
dotnet publish -c Release -o ../../vscode-extension/bin
```

### Events Not Appearing

1. Check Output channel for errors
2. Verify database permissions:
   ```sql
   GRANT ALTER ANY EVENT SESSION TO [YourUser];
   GRANT VIEW SERVER STATE TO [YourUser];
   ```
3. Execute test queries in SSMS
4. Check profiler status indicator is green

### Server Process Won't Start

**Windows:**
```cmd
# Test dotnet directly
cd bin
dotnet LightQueryProfiler.JsonRpc.dll
```

**Linux/macOS:**
```bash
cd bin
dotnet LightQueryProfiler.JsonRpc.dll
```

If it starts successfully, the issue is in the extension's spawn logic.

## 📂 Project Structure Reference

```
vscode-extension/
├── bin/                          # .NET server DLL (published here)
│   └── LightQueryProfiler.JsonRpc.dll
├── dist/                         # Compiled TypeScript
│   └── extension.js
├── media/                        # Icons and images
│   ├── icon.png
│   └── icon.svg
├── src/                          # TypeScript source
│   ├── models/
│   ├── services/
│   ├── views/
│   └── extension.ts
├── node_modules/                 # npm dependencies
├── .vscode/                      # VS Code config
│   ├── launch.json
│   └── tasks.json
├── package.json                  # Extension manifest
├── tsconfig.json                 # TypeScript config
├── .eslintrc.json               # ESLint config
└── README.md                     # User documentation
```

## 🎓 Learning Path

### For TypeScript Beginners

1. Read: [TypeScript Handbook](https://www.typescriptlang.org/docs/handbook/intro.html)
2. Review: `src/models/` (simplest files)
3. Study: `src/services/profiler-client.ts` (async patterns)
4. Understand: `src/views/profiler-view-provider.ts` (webview)

### For VS Code Extension Beginners

1. Tutorial: [Your First Extension](https://code.visualstudio.com/api/get-started/your-first-extension)
2. Guide: [Webview API](https://code.visualstudio.com/api/extension-guides/webview)
3. Review: `src/extension.ts` (activation pattern)

### For .NET/C# Integration

1. Read: `../docs/JSONRPC_CLIENT_EXAMPLE.md`
2. Study: `src/services/profiler-client.ts`
3. Debug: Run server standalone and inspect stdin/stdout

## 🔄 CI/CD Setup

### GitHub Actions Example

Create `.github/workflows/build.yml`:

```yaml
name: Build Extension

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup Node.js
      uses: actions/setup-node@v3
      with:
        node-version: '18'
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Install dependencies
      run: npm ci
      working-directory: vscode-extension
    
    - name: Lint
      run: npm run lint
      working-directory: vscode-extension
    
    - name: Compile TypeScript
      run: npm run compile
      working-directory: vscode-extension
    
    - name: Build .NET Server
      run: |
        dotnet publish -c Release -o vscode-extension/bin
      working-directory: src/LightQueryProfiler.JsonRpc
    
    - name: Package Extension
      run: npm run package
      working-directory: vscode-extension
    
    - name: Upload VSIX
      uses: actions/upload-artifact@v3
      with:
        name: vsix
        path: vscode-extension/*.vsix
```

## 📊 Performance Tips

### Reducing Extension Size

1. Exclude unnecessary files in `.vscodeignore`
2. Use `npm prune --production` before packaging
3. Minimize `bin/` folder (include only required .NET assemblies)

### Optimizing Polling

Adjust polling interval in `profiler-view-provider.ts`:
```typescript
private readonly pollingIntervalMs = 900; // Increase to reduce load
```

### Memory Management

- Clear events periodically for long-running sessions
- Limit Set size for seen events:
  ```typescript
  if (this.seenEventKeys.size > 10000) {
    this.seenEventKeys.clear(); // Reset after threshold
  }
  ```

## ✅ Pre-Release Checklist

- [ ] All TypeScript compiles without errors
- [ ] ESLint shows no errors
- [ ] .NET server builds successfully
- [ ] Version updated in `package.json`
- [ ] CHANGELOG.md updated
- [ ] README.md reviewed
- [ ] Manual testing completed
- [ ] Test on Windows, Linux, macOS
- [ ] Test with SQL Server and Azure SQL
- [ ] VSIX package created
- [ ] VSIX tested via manual install

## 🎉 You're Ready!

If you've followed all steps, you should have:
- ✅ Working development environment
- ✅ Extension running in debug mode
- ✅ .NET server integrated
- ✅ Build and package pipeline
- ✅ Ready for distribution

**Next Steps:**
1. Test with real workloads
2. Gather user feedback
3. Iterate on features
4. Publish to marketplace

---

**Support:**
- 📧 Email: support@example.com
- 🐛 Issues: https://github.com/your-repo/issues
- 💬 Discussions: https://github.com/your-repo/discussions

**Last Updated:** January 2025