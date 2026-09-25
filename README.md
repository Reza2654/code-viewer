# Code Viewer

[![CI Build](https://github.com/Reza2654/code-viewer/actions/workflows/ci.yml/badge.svg)](https://github.com/Reza2654/code-viewer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/UI-Avalonia%2011.2-1877F2.svg)](https://avaloniaui.net/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D6.svg)](https://microsoft.com/windows)

> **"Open quickly. See the code. Make a small change. Save. Close."**

**Code Viewer** is an ultra-fast, modern, lightweight code viewer and editor for Windows. Built natively with C#, .NET 8, and Avalonia UI, it occupies the sweet spot between basic Notepad and heavyweight IDEs like Visual Studio or VS Code.

---

## Key Features

- ⚡ **Instant Startup**: Sub-25ms startup time; opens directly to your code without loading screens or bloated background tasks.
- 🪟 **Native Windows 11 Context Menu**: Directly integrates into the modern Windows 11 right-click context menu (no "Show more options" extra click required) powered by a native `IExplorerCommand` Sparse Package shell extension.
- 🎨 **Rich Theme System**:
  - **6 Built-in Themes**: Dark+ (VS Code style), One Dark Pro, Monokai, Dracula, Solarized Dark, and GitHub Light.
  - **Dynamic App & Editor Styling**: Instant live theme switching without restarting.
  - **Theme Importer**: Easily import custom UI themes (`.json`) or custom syntax highlighting definitions (`.xshd`).
- 🧩 **Extensible Plugin & Tool System**:
  - **JSON Tools**: Format with 2 spaces, minify, and validate JSON.
  - **Encodings**: Base64 Encode & Decode, URL Encode & Decode.
  - **Line Utilities**: Sort Lines (A-Z and Z-A), Remove Duplicate Lines, Reverse Lines.
  - **Case Transform**: Convert text to UPPERCASE, lowercase, and Title Case.
  - **Text Statistics**: Comprehensive character, word, line, non-empty line, and byte counts.
  - **Dynamic DLL Plugin Importer**: Drop third-party `.dll` assemblies implementing `IPlugin` into the plugins directory or load them via **Tools -> Import Plugin (.dll)...** without locking the file on disk.
- 📑 **Modern Multi-Tab Editor**: Open multiple documents simultaneously with modification markers (`*`), drag-and-drop tab reordering, and close verification.
- 🔍 **Interactive Floating Search**: Find in document with `Ctrl+F`, match case, and wrap-around search navigation.
- 💾 **Safe Local Single-Instance IPC**: Right-clicking multiple files opens them as new tabs inside the existing window using a high-performance local Named Pipe, preventing duplicate heavy processes.
- 📦 **Professional Installer & Portable Package**: One-click Inno Setup installer (`CodeViewer-v1.0-Setup.exe`) and portable standalone `.zip` archive.
- 🔓 **100% Free, Local & Open Source**: No accounts, no telemetry, no network calls, fully MIT-licensed.

---

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + N` | New Document |
| `Ctrl + O` | Open File |
| `Ctrl + S` | Save File |
| `Ctrl + Shift + S` | Save As |
| `Ctrl + W` | Close Active Tab |
| `Ctrl + F` | Find in Document |
| `Ctrl + Z` | Undo |
| `Ctrl + Y` | Redo |
| `Ctrl + +` | Zoom In |
| `Ctrl + -` | Zoom Out |
| `Ctrl + 0` | Reset Zoom (14 pt) |
| `Alt + Z` | Toggle Word Wrap |
| `Alt + F4` | Exit |

---

## Installation & Packaging

### Option 1: Installer (Recommended)
Download and run `CodeViewer-v1.0-Setup.exe` from the [Releases](https://github.com/your-username/code-viewer/releases) page.
- Installs Code Viewer to your system.
- Creates Start Menu and Desktop shortcuts.
- Automatically registers the modern Windows 11 right-click context menu.

### Option 2: Portable ZIP
Download `CodeViewer-v1.0.1-beta.5-win-x64-portable.zip`, extract anywhere, and run `CodeViewer.exe`.
To enable the modern Windows 11 context menu for portable use, right-click `scripts\register-windows11-context-menu.ps1` and select **Run with PowerShell**.

### Option 3: Windows Package Manager (WinGet)
Install directly via WinGet:
```powershell
# From local manifest:
winget install --manifest packaging/winget

# Or once submitted to winget-pkgs:
winget install Reza2654.CodeViewer
```

---

## Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 or 11 (x64)
- Optional: [Inno Setup 6](https://jrsoftware.org/isdl.php) (for compiling the installer)

### Quick Start
```powershell
# Clone the repository
git clone https://github.com/your-username/code-viewer.git
cd code-viewer

# Restore dependencies
dotnet restore CodeViewer.slnx

# Build all projects
dotnet build CodeViewer.slnx

# Run all 48 unit tests
dotnet test tests/CodeViewer.Tests/CodeViewer.Tests.csproj

# Run the app locally
dotnet run --project src/CodeViewer/CodeViewer.csproj
```

### Packaging Release & Installer
To generate both the portable ZIP and the Inno Setup executable installer:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
```
Output artifacts are saved in `dist/`:
- `CodeViewer-v1.0-Setup.exe`
- `CodeViewer-v1.0-win-x64-portable.zip`
- `checksums.sha256`

---

## Creating Plugins & Themes

- **Custom Themes**: Place `.json` color files in `%LocalAppData%\CodeViewer\Themes\` or use **Themes -> Import Color Theme (.json)...**.
- **Custom Syntax Definitions**: Place `.xshd` syntax files in `%LocalAppData%\CodeViewer\Themes\` or use **Themes -> Import Syntax Definition (.xshd)...**.
- **Developing Plugins**: Implement the `IPlugin` interface in a .NET 8 class library and load your `.dll` via **Tools -> Import Plugin (.dll)...**. See [CONTRIBUTING.md](CONTRIBUTING.md) for full developer walkthrough and examples.

---

## Contributing

Contributions, feature ideas, and pull requests are welcome! Please check out [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidelines.

---

## License

This project is licensed under the [MIT License](LICENSE).
