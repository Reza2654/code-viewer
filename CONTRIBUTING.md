# Contributing to Code Viewer

Thank you for your interest in contributing to **Code Viewer**!

Code Viewer is an ultra-fast, modern, lightweight code viewer and editor designed to sit in the sweet spot between Notepad and full IDEs:
> **"Open quickly. See the code. Make a small change. Save. Close."**

---

## Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer
- Windows 10 / Windows 11 (64-bit recommended)
- Optional: [Inno Setup 6](https://jrsoftware.org/isdl.php) (for building the installer)

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/<your-username>/code-viewer.git
cd code-viewer

# Restore dependencies
dotnet restore CodeViewer.slnx

# Build the solution
dotnet build CodeViewer.slnx

# Run unit tests
dotnet test tests/CodeViewer.Tests/CodeViewer.Tests.csproj

# Run the app locally
dotnet run --project src/CodeViewer/CodeViewer.csproj
```

---

## Creating & Importing Themes

Code Viewer supports both **Application UI Themes** (`.json`) and **Syntax Highlighting Definitions** (`.xshd`).

### 1. Custom UI Themes (`.json`)
Themes can be dropped into `%LocalAppData%\CodeViewer\Themes\` or imported via the menu: **Themes -> Import Color Theme (.json)...**.

Example theme format:
```json
{
  "id": "my-theme",
  "name": "My Custom Dark",
  "isDark": true,
  "windowBackground": "#181A1F",
  "foreground": "#ABB2BF",
  "menuBackground": "#21252B",
  "menuForeground": "#CCCCCC",
  "tabBarBackground": "#21252B",
  "tabItemActiveBackground": "#181A1F",
  "tabItemInactiveBackground": "#282C34",
  "tabItemActiveForeground": "#FFFFFF",
  "tabItemInactiveForeground": "#7F848E",
  "editorBackground": "#181A1F",
  "editorForeground": "#ABB2BF",
  "lineNumbersForeground": "#4B5263",
  "selectionBackground": "#3E4451",
  "statusBarBackground": "#61AFEF",
  "statusBarForeground": "#181A1F",
  "accentColor": "#61AFEF",
  "borderColor": "#1E2227"
}
```

### 2. Custom Syntax Definitions (`.xshd`)
AvaloniaEdit XML syntax definitions can be placed in `%LocalAppData%\CodeViewer\Themes\` or imported via: **Themes -> Import Syntax Definition (.xshd)...**.

---

## Developing Plugins

Code Viewer features an extensible plugin system. You can write your own developer tools by implementing the `IPlugin` interface.

### Example Plugin
Create a new C# Class Library targeting `.NET 8`:
```csharp
using System.Threading.Tasks;
using CodeViewer.Plugins;

namespace MyPlugins;

public class Rot13Plugin : IPlugin
{
    public string Id => "custom-rot13";
    public string Name => "ROT13 Cipher";
    public string Category => "Text Utilities";
    public string Description => "Applies ROT13 Caesar cipher to text.";
    public string Author => "Your Name";
    public string Version => "1.0.0";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c >= 'a' && c <= 'z') chars[i] = (char)((c - 'a' + 13) % 26 + 'a');
            else if (c >= 'A' && c <= 'Z') chars[i] = (char)((c - 'A' + 13) % 26 + 'A');
        }

        var result = new string(chars);
        if (context.HasSelection)
            context.ReplaceSelectedText(result);
        else
            context.ReplaceAllText(result);

        return Task.CompletedTask;
    }
}
```

Build your project and copy the `.dll` into `%LocalAppData%\CodeViewer\Plugins\` or import it using **Tools -> Import Plugin (.dll)...**.

---

## Packaging Releases
To build the standalone portable ZIP and the Inno Setup Windows installer:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
```
The resulting files will be generated in `dist/`:
- `CodeViewer-v1.0-Setup.exe`
- `CodeViewer-v1.0-win-x64-portable.zip`
- `checksums.sha256`

---

## Submitting Pull Requests
1. Fork the repo and create your branch from `main`.
2. Ensure `dotnet test` passes with 0 failures.
3. Keep startup time fast and dependencies minimal.
4. Open a Pull Request referencing any related issues.
