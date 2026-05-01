# opxBlazor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Blazor Server app with MudBlazor that previews filtered captions from T42/VBI/ANC/MXF/TS files, backed by a new structured color span API in libopx.

**Architecture:** The library gets a new `TeletextColor` enum, `ColorSpan` record, and `T42.GetColorSpans()` method that mirrors the existing `T42.GetText()` logic but returns structured data instead of ANSI strings. The Blazor app uses `FormatIO` to parse files and renders results in a virtualized MudDataGrid with colored text spans.

**Tech Stack:** .NET 10.0, Blazor Server, MudBlazor 9.1.0, libopx (project reference)

---

### Task 1: Add TeletextColor enum and ColorSpan record to libopx

**Files:**
- Create: `lib/Models/TeletextColor.cs`
- Create: `lib/Models/ColorSpan.cs`

**Step 1: Create TeletextColor enum**

Create `lib/Models/TeletextColor.cs`:

```csharp
namespace nathanbutlerDEV.libopx.Models;

/// <summary>
/// Represents the 8 standard teletext colors as defined by the ETS 300 706 specification.
/// Values correspond to the 3-bit color codes used in teletext control characters.
/// </summary>
public enum TeletextColor : byte
{
    Black = 0,
    Red = 1,
    Green = 2,
    Yellow = 3,
    Blue = 4,
    Magenta = 5,
    Cyan = 6,
    White = 7
}
```

**Step 2: Create ColorSpan record**

Create `lib/Models/ColorSpan.cs`:

```csharp
namespace nathanbutlerDEV.libopx.Models;

/// <summary>
/// Represents a span of teletext text with associated foreground and background colors.
/// Used for structured rendering of teletext content in UI applications.
/// </summary>
/// <param name="Text">The text content of this span</param>
/// <param name="Foreground">The foreground (text) color</param>
/// <param name="Background">The background color</param>
public record ColorSpan(string Text, TeletextColor Foreground, TeletextColor Background = TeletextColor.Black);
```

**Step 3: Verify it compiles**

Run: `dotnet build lib/libopx.csproj`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add lib/Models/TeletextColor.cs lib/Models/ColorSpan.cs
git commit -m "feat(lib): add TeletextColor enum and ColorSpan record"
```

---

### Task 2: Add T42.GetColorSpans() method

**Files:**
- Modify: `lib/Formats/T42.cs` (add `GetColorSpans`, `DecodeHeaderRowSpans`, `DecodeDataPacketSpans` methods)

**Step 1: Write test for GetColorSpans**

Create test in existing test file or new file `tests/Formats/T42ColorSpanTests.cs`:

```csharp
using nathanbutlerDEV.libopx.Formats;
using nathanbutlerDEV.libopx.Models;

namespace libopx.Tests.Formats;

public class T42ColorSpanTests
{
    [Fact]
    public void GetColorSpans_EmptyBytes_ReturnsEmptyList()
    {
        var result = T42.GetColorSpans([], false);
        Assert.Empty(result);
    }

    [Fact]
    public void GetColorSpans_HeaderRow_ReturnsPageNumberAndText()
    {
        // Use the first 40 bytes of T42.Sample (after stripping MRAG bytes 0-1)
        // T42.Sample starts with 2 MRAG bytes, then 40 data bytes
        byte[] sampleLine = T42.Sample.Take(42).Skip(2).ToArray();
        var spans = T42.GetColorSpans(sampleLine, isHeaderRow: true, magazine: 8, pageNumber: "01");

        Assert.NotEmpty(spans);
        // First span should contain page number text
        Assert.Contains("P801", spans[0].Text);
        // Header defaults to white on black
        Assert.Equal(TeletextColor.White, spans[0].Foreground);
        Assert.Equal(TeletextColor.Black, spans[0].Background);
    }

    [Fact]
    public void GetColorSpans_DataRow_DefaultsWhiteOnBlack()
    {
        // Create simple data: 40 bytes of 'A' (0x41) with parity
        byte[] data = Enumerable.Repeat((byte)0xC1, 40).ToArray(); // 'A' with parity bit
        var spans = T42.GetColorSpans(data, isHeaderRow: false);

        Assert.NotEmpty(spans);
        // Should all be white on black (default)
        foreach (var span in spans)
        {
            Assert.Equal(TeletextColor.White, span.Foreground);
            Assert.Equal(TeletextColor.Black, span.Background);
        }
    }

    [Fact]
    public void GetColorSpans_WithColorControlCode_ChangesColor()
    {
        // Byte 0x02 = green alpha foreground (set-after)
        // Followed by 'A' characters
        byte[] data = new byte[40];
        data[0] = 0x02; // Green foreground control code
        for (int i = 1; i < 40; i++)
            data[i] = 0x41; // 'A' without parity for simplicity

        var spans = T42.GetColorSpans(data, isHeaderRow: false);

        Assert.NotEmpty(spans);
        // After the control code, text should be green
        var greenSpans = spans.Where(s => s.Foreground == TeletextColor.Green && s.Text.Contains('A'));
        Assert.NotEmpty(greenSpans);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/libopx.Tests.csproj --filter "FullyQualifiedName~T42ColorSpanTests"`
Expected: FAIL — `GetColorSpans` method does not exist

**Step 3: Implement GetColorSpans in T42.cs**

Add these methods to `lib/Formats/T42.cs` in the `#region Public Methods` section, after `GetText`:

```csharp
/// <summary>
/// Parses teletext data to a list of colored text spans for UI rendering.
/// Mirrors the logic of GetText but returns structured ColorSpan data
/// instead of ANSI escape sequences.
/// </summary>
/// <param name="bytes">The raw teletext data bytes (should be 40 bytes after MRAG is stripped)</param>
/// <param name="isHeaderRow">True if this is a header row (row 0)</param>
/// <param name="magazine">Optional magazine number for header row display</param>
/// <param name="pageNumber">Optional page number for header row display (2-digit hex)</param>
/// <returns>List of ColorSpan representing the colored text</returns>
public static List<ColorSpan> GetColorSpans(byte[] bytes, bool isHeaderRow, int? magazine = null, string? pageNumber = null)
{
    if (bytes.Length == 0)
        return [];

    return isHeaderRow
        ? DecodeHeaderRowSpans(bytes, magazine, pageNumber)
        : DecodeDataPacketSpans(bytes);
}

/// <summary>
/// Decodes a header row to structured color spans.
/// </summary>
private static List<ColorSpan> DecodeHeaderRowSpans(byte[] bytes, int? magazine, string? pageNumber)
{
    var spans = new List<ColorSpan>();

    var pageString = magazine.HasValue && pageNumber != null
        ? $"P{magazine}{pageNumber}"
        : "P???";

    // Page number padded to 8 characters
    spans.Add(new ColorSpan(pageString.PadRight(8), TeletextColor.White, TeletextColor.Black));

    // Decode header text content (bytes 8-39)
    int headerTextStart = 8;
    int headerTextEnd = Math.Min(bytes.Length, Constants.T42_DISPLAY_WIDTH);

    var sb = new StringBuilder();
    for (int j = headerTextStart; j < headerTextEnd; j++)
    {
        int c = bytes[j] & 0x7F;
        sb.Append(c >= 0x20 && c <= 0x7F ? MapG0Latin(c) : ' ');
    }

    int currentLength = 8 + (headerTextEnd - headerTextStart);
    if (currentLength < Constants.T42_DISPLAY_WIDTH)
        sb.Append(new string(' ', Constants.T42_DISPLAY_WIDTH - currentLength));

    if (sb.Length > 0)
        spans.Add(new ColorSpan(sb.ToString(), TeletextColor.White, TeletextColor.Black));

    return spans;
}

/// <summary>
/// Decodes a data packet to structured color spans.
/// Implements Set-After color model matching DecodeDataPacket logic.
/// </summary>
private static List<ColorSpan> DecodeDataPacketSpans(byte[] bytes)
{
    var spans = new List<ColorSpan>();
    var currentText = new StringBuilder();

    int foreground = 7; // White
    int background = 0; // Black
    int pendingForeground = -1;
    int pendingBackground = -1;
    int currentFg = 7;
    int currentBg = 0;
    int boxDepth = 0;

    int endPos = Math.Min(bytes.Length, Constants.T42_DISPLAY_WIDTH);
    for (int j = 0; j < endPos; j++)
    {
        int c = bytes[j] & 0x7F;

        if (c <= 0x1F)
        {
            // Control codes — same logic as DecodeDataPacket
            if (c <= 0x07)
            {
                pendingForeground = c;
            }
            else if (c >= Constants.T42_GRAPHICS_COLOR_START && c <= Constants.T42_GRAPHICS_COLOR_END)
            {
                pendingForeground = c & 0x07;
            }
            else if (c == Constants.T42_BLOCK_START_BYTE)
            {
                boxDepth++;
            }
            else if (c == Constants.T42_NORMAL_HEIGHT)
            {
                if (boxDepth > 0) boxDepth--;
                if (boxDepth == 0)
                {
                    foreground = 7;
                    background = 0;
                    pendingForeground = -1;
                    pendingBackground = -1;
                }
            }
            else if (c == Constants.T42_BLACK_BACKGROUND)
            {
                background = 0;
                pendingBackground = -1;
            }
            else if (c == Constants.T42_BACKGROUND_CONTROL)
            {
                if (pendingForeground >= 0)
                {
                    foreground = pendingForeground;
                    pendingForeground = -1;
                }
                pendingBackground = foreground;
            }
            else
            {
                if (pendingForeground >= 0)
                {
                    foreground = pendingForeground;
                    pendingForeground = -1;
                }
                if (pendingBackground >= 0)
                {
                    background = pendingBackground;
                    pendingBackground = -1;
                }
            }

            // Emit character with current colors (control = space)
            EmitChar(spans, currentText, ' ', foreground, background, ref currentFg, ref currentBg);
        }
        else
        {
            // Printable character — apply pending (Set-After)
            if (pendingForeground >= 0)
            {
                foreground = pendingForeground;
                pendingForeground = -1;
            }
            if (pendingBackground >= 0)
            {
                background = pendingBackground;
                pendingBackground = -1;
            }

            EmitChar(spans, currentText, MapG0Latin(c), foreground, background, ref currentFg, ref currentBg);
        }
    }

    // Flush remaining text
    if (currentText.Length > 0)
        spans.Add(new ColorSpan(currentText.ToString(), (TeletextColor)currentFg, (TeletextColor)currentBg));

    return spans;
}

/// <summary>
/// Emits a character, flushing the current span if colors change.
/// </summary>
private static void EmitChar(List<ColorSpan> spans, StringBuilder currentText, char c, int fg, int bg, ref int currentFg, ref int currentBg)
{
    if (fg != currentFg || bg != currentBg)
    {
        // Flush current span
        if (currentText.Length > 0)
        {
            spans.Add(new ColorSpan(currentText.ToString(), (TeletextColor)currentFg, (TeletextColor)currentBg));
            currentText.Clear();
        }
        currentFg = fg;
        currentBg = bg;
    }
    currentText.Append(c);
}
```

Add `using nathanbutlerDEV.libopx.Models;` to the top of `T42.cs`.

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/libopx.Tests.csproj --filter "FullyQualifiedName~T42ColorSpanTests"`
Expected: All PASS

**Step 5: Commit**

```bash
git add lib/Formats/T42.cs tests/Formats/T42ColorSpanTests.cs
git commit -m "feat(lib): add T42.GetColorSpans() for structured color output"
```

---

### Task 3: Add ColorSpans property to Line

**Files:**
- Modify: `lib/Line.cs` (add `ColorSpans` property, populate in `ExtractMetadata`)

**Step 1: Write test**

Add to `tests/Formats/T42ColorSpanTests.cs` (or a new `tests/LineColorSpanTests.cs`):

```csharp
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Models;

namespace libopx.Tests;

public class LineColorSpanTests
{
    [Fact]
    public void Line_ParseLine_PopulatesColorSpans()
    {
        // Use first 42 bytes of T42.Sample as a complete T42 line
        byte[] t42Line = nathanbutlerDEV.libopx.Formats.T42.Sample.Take(42).ToArray();

        var line = new Line();
        line.ParseLine(t42Line, Format.T42);

        Assert.NotNull(line.ColorSpans);
        Assert.NotEmpty(line.ColorSpans);
    }

    [Fact]
    public void Line_ParseLine_UnknownFormat_ReturnsEmptyColorSpans()
    {
        var line = new Line();
        // A line with unknown format should have empty color spans
        Assert.NotNull(line.ColorSpans);
        Assert.Empty(line.ColorSpans);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/libopx.Tests.csproj --filter "FullyQualifiedName~LineColorSpanTests"`
Expected: FAIL — `ColorSpans` property does not exist

**Step 3: Add ColorSpans property and populate in ExtractMetadata**

In `lib/Line.cs`, add the property after the `Text` property:

```csharp
/// <summary>
/// Gets or sets the structured color span data for UI rendering.
/// Populated alongside Text during ExtractMetadata.
/// </summary>
public List<ColorSpan> ColorSpans { get; set; } = [];
```

Add `using nathanbutlerDEV.libopx.Models;` to the top of `Line.cs`.

In the `ExtractMetadata` method, update the T42 case to also populate ColorSpans:

```csharp
case Format.T42:
    if (Data.Length >= Constants.T42_LINE_SIZE && Data.Any(b => b != 0))
    {
        Magazine = T42.GetMagazine(Data[0]);
        Row = T42.GetRow([.. Data.Take(2)]);
        var pageNumber = Row == 0 ? T42.GetPageNumber(Data) : null;
        Text = T42.GetText([.. Data.Skip(2)], Row == 0, Magazine, pageNumber);
        ColorSpans = T42.GetColorSpans([.. Data.Skip(2)], Row == 0, Magazine, pageNumber);
    }
    else
    {
        Magazine = -1;
        Row = -1;
        Text = Constants.T42_BLANK_LINE;
        ColorSpans = [];
    }
    break;
```

**Step 4: Run tests**

Run: `dotnet test tests/libopx.Tests.csproj --filter "FullyQualifiedName~LineColorSpanTests"`
Expected: All PASS

**Step 5: Run full test suite to ensure no regressions**

Run: `dotnet test tests/libopx.Tests.csproj`
Expected: All PASS

**Step 6: Commit**

```bash
git add lib/Line.cs tests/LineColorSpanTests.cs
git commit -m "feat(lib): add ColorSpans property to Line, populated during ExtractMetadata"
```

---

### Task 4: Scaffold opxBlazor project

**Files:**
- Create: `apps/opxBlazor/opxBlazor.csproj`
- Create: `apps/opxBlazor/Program.cs`
- Create: `apps/opxBlazor/App.razor`
- Create: `apps/opxBlazor/Routes.razor`
- Create: `apps/opxBlazor/_Imports.razor`
- Create: `apps/opxBlazor/wwwroot/css/app.css`
- Create: `apps/opxBlazor/Properties/launchSettings.json`
- Modify: `libopx.sln` (add project via `dotnet sln add`)

**Step 1: Create project directory**

Run: `mkdir -p apps/opxBlazor/Properties apps/opxBlazor/wwwroot/css apps/opxBlazor/Layout apps/opxBlazor/Pages apps/opxBlazor/Services apps/opxBlazor/Models`

**Step 2: Create opxBlazor.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../lib/libopx.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="MudBlazor" Version="9.1.0" />
  </ItemGroup>

</Project>
```

**Step 3: Create Program.cs**

```csharp
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<opxBlazor.App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

**Step 4: Create App.razor**

```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>opxBlazor</title>
    <base href="/" />
    <link href="css/app.css" rel="stylesheet" />
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
</body>
</html>
```

**Step 5: Create Routes.razor**

```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

**Step 6: Create _Imports.razor**

```razor
@using System.Net.Http
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop
@using MudBlazor
@using nathanbutlerDEV.libopx.Models
```

**Step 7: Create wwwroot/css/app.css**

```css
/* Teletext color mappings for ColorSpan rendering */
.tt-fg-black   { color: #000000; }
.tt-fg-red     { color: #ff0000; }
.tt-fg-green   { color: #00ff00; }
.tt-fg-yellow  { color: #ffff00; }
.tt-fg-blue    { color: #0000ff; }
.tt-fg-magenta { color: #ff00ff; }
.tt-fg-cyan    { color: #00ffff; }
.tt-fg-white   { color: #ffffff; }

.tt-bg-black   { background-color: #000000; }
.tt-bg-red     { background-color: #ff0000; }
.tt-bg-green   { background-color: #00ff00; }
.tt-bg-yellow  { background-color: #ffff00; }
.tt-bg-blue    { background-color: #0000ff; }
.tt-bg-magenta { background-color: #ff00ff; }
.tt-bg-cyan    { background-color: #00ffff; }
.tt-bg-white   { background-color: #ffffff; }

.tt-text {
    font-family: 'Cascadia Mono', 'Consolas', 'Courier New', monospace;
    white-space: pre;
}
```

**Step 8: Create Properties/launchSettings.json**

```json
{
  "profiles": {
    "opxBlazor": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7100;http://localhost:5100",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Step 9: Add to solution**

Run: `dotnet sln libopx.sln add apps/opxBlazor/opxBlazor.csproj --solution-folder apps`

**Step 10: Verify it builds**

Run: `dotnet build apps/opxBlazor/opxBlazor.csproj`
Expected: Build succeeded

**Step 11: Commit**

```bash
git add apps/opxBlazor/ libopx.sln
git commit -m "feat: scaffold opxBlazor Blazor Server project with MudBlazor"
```

---

### Task 5: Create MainLayout and NavMenu

**Files:**
- Create: `apps/opxBlazor/Layout/MainLayout.razor`
- Create: `apps/opxBlazor/Layout/NavMenu.razor`

**Step 1: Create MainLayout.razor**

```razor
@inherits LayoutComponentBase

<MudThemeProvider IsDarkMode="true" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start"
                       OnClick="@ToggleDrawer" />
        <MudText Typo="Typo.h6">opxBlazor</MudText>
    </MudAppBar>
    <MudDrawer @bind-Open="_drawerOpen" Elevation="2" Variant="DrawerVariant.Mini"
               OpenMiniOnHover="true">
        <NavMenu />
    </MudDrawer>
    <MudMainContent Class="pa-4">
        @Body
    </MudMainContent>
</MudLayout>

@code {
    private bool _drawerOpen = true;

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }
}
```

**Step 2: Create NavMenu.razor**

```razor
<MudNavMenu>
    <MudNavLink Href="/filter" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.FilterAlt">
        Filter
    </MudNavLink>
</MudNavMenu>
```

**Step 3: Verify it builds**

Run: `dotnet build apps/opxBlazor/opxBlazor.csproj`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add apps/opxBlazor/Layout/
git commit -m "feat(opxBlazor): add MainLayout with MudBlazor sidebar and NavMenu"
```

---

### Task 6: Create CaptionLineViewModel and FilterService

**Files:**
- Create: `apps/opxBlazor/Models/CaptionLineViewModel.cs`
- Create: `apps/opxBlazor/Services/FilterService.cs`

**Step 1: Create CaptionLineViewModel**

```csharp
using nathanbutlerDEV.libopx.Models;

namespace opxBlazor.Models;

/// <summary>
/// View model representing a single caption line for display in the filter results grid.
/// </summary>
public class CaptionLineViewModel
{
    public string Timecode { get; init; } = "";
    public int Magazine { get; init; }
    public int Row { get; init; }
    public List<ColorSpan> ColorSpans { get; init; } = [];
}
```

**Step 2: Create FilterService**

```csharp
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;
using opxBlazor.Models;

namespace opxBlazor.Services;

/// <summary>
/// Service that wraps FormatIO to parse teletext files and return view models for the UI.
/// </summary>
public class FilterService
{
    /// <summary>
    /// Parses a teletext file and returns caption line view models.
    /// </summary>
    /// <param name="filePath">Absolute path to the input file</param>
    /// <param name="magazine">Optional magazine filter (1-8)</param>
    /// <param name="rows">Optional row filter array</param>
    /// <param name="pageNumber">Optional page number filter</param>
    /// <param name="useCaps">Whether to filter empty caption rows</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of caption line view models</returns>
    public async Task<List<CaptionLineViewModel>> FilterAsync(
        string filePath,
        int? magazine,
        int[]? rows,
        string? pageNumber,
        bool useCaps,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CaptionLineViewModel>();

        using var io = FormatIO.Open(filePath);

        var filterRows = rows ?? (useCaps ? Constants.CAPTION_ROWS : Constants.DEFAULT_ROWS);
        io.Filter(magazine, filterRows)
          .WithPageNumber(pageNumber)
          .WithUseCaps(useCaps);

        var inputFormat = io.DetectedFormat;

        if (inputFormat is Format.ANC or Format.TS or Format.MXF)
        {
            await foreach (var packet in io.ParsePacketsAsync(cancellationToken))
            {
                foreach (var line in packet.Lines)
                {
                    results.Add(new CaptionLineViewModel
                    {
                        Timecode = packet.Timecode.ToString(),
                        Magazine = line.Magazine,
                        Row = line.Row,
                        ColorSpans = line.ColorSpans
                    });
                }
            }
        }
        else
        {
            await foreach (var line in io.ParseLinesAsync(cancellationToken))
            {
                results.Add(new CaptionLineViewModel
                {
                    Timecode = line.LineTimecode?.ToString() ?? line.LineNumber.ToString().PadLeft(11),
                    Magazine = line.Magazine,
                    Row = line.Row,
                    ColorSpans = line.ColorSpans
                });
            }
        }

        return results;
    }
}
```

**Step 3: Register FilterService in DI**

In `Program.cs`, add before `var app = builder.Build();`:

```csharp
builder.Services.AddScoped<opxBlazor.Services.FilterService>();
```

**Step 4: Verify it builds**

Run: `dotnet build apps/opxBlazor/opxBlazor.csproj`
Expected: Build succeeded

> **Note:** The `DetectedFormat` property on FormatIO may not exist. If it doesn't, we'll need to check what's available. The format is auto-detected internally — we may need to check if there's a public property or determine format from the file extension in the service instead. Adjust accordingly during implementation.

**Step 5: Commit**

```bash
git add apps/opxBlazor/Models/CaptionLineViewModel.cs apps/opxBlazor/Services/FilterService.cs apps/opxBlazor/Program.cs
git commit -m "feat(opxBlazor): add FilterService and CaptionLineViewModel"
```

---

### Task 7: Create Filter page

**Files:**
- Create: `apps/opxBlazor/Pages/Filter.razor`

**Step 1: Create Filter.razor**

```razor
@page "/filter"
@page "/"
@using opxBlazor.Models
@using opxBlazor.Services
@using nathanbutlerDEV.libopx
@inject FilterService FilterService

<MudText Typo="Typo.h5" Class="mb-4">Filter</MudText>

<MudPaper Class="pa-4 mb-4" Elevation="1">
    <MudGrid>
        <MudItem xs="12" md="6">
            <MudTextField @bind-Value="_filePath" Label="File Path" Variant="Variant.Outlined"
                          Placeholder="/path/to/file.t42"
                          Adornment="Adornment.End" AdornmentIcon="@Icons.Material.Filled.FolderOpen" />
        </MudItem>
        <MudItem xs="6" md="1">
            <MudSelect T="int?" @bind-Value="_magazine" Label="Magazine" Variant="Variant.Outlined"
                       Clearable="true">
                @for (int i = 1; i <= 8; i++)
                {
                    <MudSelectItem T="int?" Value="@i">@i</MudSelectItem>
                }
            </MudSelect>
        </MudItem>
        <MudItem xs="6" md="2">
            <MudTextField @bind-Value="_rowsString" Label="Rows" Variant="Variant.Outlined"
                          Placeholder="1,2,5-8" />
        </MudItem>
        <MudItem xs="6" md="1">
            <MudTextField @bind-Value="_pageNumber" Label="Page" Variant="Variant.Outlined"
                          Placeholder="01" />
        </MudItem>
        <MudItem xs="3" md="1" Class="d-flex align-center">
            <MudSwitch T="bool" @bind-Value="_useCaps" Label="Caps" Color="Color.Primary" />
        </MudItem>
        <MudItem xs="3" md="1" Class="d-flex align-center">
            <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="LoadFile"
                       Disabled="_loading">
                @if (_loading)
                {
                    <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
                }
                Load
            </MudButton>
        </MudItem>
    </MudGrid>
</MudPaper>

@if (!string.IsNullOrEmpty(_error))
{
    <MudAlert Severity="Severity.Error" Class="mb-4" ShowCloseIcon="true"
              CloseIconClicked="() => _error = null">@_error</MudAlert>
}

@if (_results.Count > 0)
{
    <MudText Typo="Typo.body2" Class="mb-2">@_results.Count lines</MudText>

    <MudDataGrid T="CaptionLineViewModel" Items="_results" Virtualize="true" FixedHeader="true"
                 Height="calc(100vh - 340px)" Dense="true" Hover="true">
        <Columns>
            <PropertyColumn Property="x => x.Timecode" Title="Timecode"
                            CellStyle="font-family: monospace;" />
            <PropertyColumn Property="x => x.Magazine" Title="Mag" />
            <PropertyColumn Property="x => x.Row" Title="Row" />
            <TemplateColumn Title="Text">
                <CellTemplate>
                    <span class="tt-text">
                        @foreach (var span in context.Item.ColorSpans)
                        {
                            <span class="tt-fg-@span.Foreground.ToString().ToLower() tt-bg-@span.Background.ToString().ToLower()">@span.Text</span>
                        }
                    </span>
                </CellTemplate>
            </TemplateColumn>
        </Columns>
    </MudDataGrid>
}

@code {
    private string? _filePath;
    private int? _magazine;
    private string? _rowsString;
    private string? _pageNumber;
    private bool _useCaps;
    private bool _loading;
    private string? _error;
    private List<CaptionLineViewModel> _results = [];

    private async Task LoadFile()
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            _error = "Please enter a file path.";
            return;
        }

        if (!File.Exists(_filePath))
        {
            _error = $"File not found: {_filePath}";
            return;
        }

        _loading = true;
        _error = null;
        _results = [];

        try
        {
            int[]? rows = null;
            if (!string.IsNullOrWhiteSpace(_rowsString))
            {
                rows = FilterHelpers.ParseRowsString(_rowsString);
            }

            _results = await FilterService.FilterAsync(
                _filePath, _magazine, rows, _pageNumber, _useCaps);
        }
        catch (Exception ex)
        {
            _error = $"Error: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }
}
```

**Step 2: Verify it builds**

Run: `dotnet build apps/opxBlazor/opxBlazor.csproj`
Expected: Build succeeded

**Step 3: Run the app and verify manually**

Run: `dotnet run --project apps/opxBlazor/opxBlazor.csproj`
Expected: App starts, navigate to https://localhost:7100, see Filter page with sidebar

**Step 4: Commit**

```bash
git add apps/opxBlazor/Pages/Filter.razor
git commit -m "feat(opxBlazor): add Filter page with controls and virtualized data grid"
```

---

### Task 8: Integration testing and polish

**Step 1: Verify full solution builds**

Run: `dotnet build libopx.sln`
Expected: All projects build successfully

**Step 2: Run all tests**

Run: `dotnet test tests/libopx.Tests.csproj`
Expected: All tests pass

**Step 3: Manual smoke test**

Run: `dotnet run --project apps/opxBlazor/opxBlazor.csproj`

Test with:
1. Load a T42 file — verify colored text renders in the grid
2. Load a VBI file — verify lines appear with timecodes
3. Apply magazine filter — verify only matching lines show
4. Toggle caps — verify empty rows are filtered
5. Enter an invalid path — verify error message shows

**Step 4: Fix any issues found during smoke test**

Adjust FilterService, Filter.razor, or T42.GetColorSpans as needed.

**Step 5: Commit any fixes**

```bash
git add -A
git commit -m "fix(opxBlazor): address issues found during integration testing"
```

---

### Implementation Notes

**FormatIO.DetectedFormat:** This property may not exist publicly. During Task 6, check what's available on FormatIO. If needed, determine format from file extension using the same logic the CLI uses (in `CommandHelpers.DetermineFormatFromFile`), or add a public `InputFormat` property to FormatIO. The simplest approach: check if `FormatIO` already exposes the format, and if not, detect it from the file extension in `FilterService` using a simple switch on `Path.GetExtension()`.

**Packet filtering for ANC/MXF:** The existing `FilterAsync` in `Functions.cs` shows that for ANC/MXF, packets contain multiple lines that need individual filtering by magazine/row. The `FormatIO` filter pipeline already handles this internally, so the lines in each packet should already be filtered. Verify during Task 8.

**T42.GetColorSpans parity with GetText:** The GetColorSpans method must match GetText exactly in control code handling. The plan includes all the same branching logic. During testing, compare GetColorSpans output character counts with GetText output to ensure they produce the same character positions.
