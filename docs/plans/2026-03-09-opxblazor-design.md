# opxBlazor Design

## Overview

Blazor Server application serving as the unified GUI for libopx. Replaces and extends
the functionality of simpleRestriper over time. Uses MudBlazor for UI components.

## Hosting Model

Blazor Server — runs server-side with SignalR streaming. Direct file system access,
no API layer needed. The library runs natively on the server.

## Application Shell

- MudBlazor `MudLayout` with sidebar navigation and top app bar
- Sidebar (`MudNavMenu`) shows available tools — **Filter only** for initial release
- Future sidebar items: Restripe, Extract, Convert (eventually replacing simpleRestriper)
- Each tool is a separate Blazor page/route

## Library Change: Structured Color Spans

New types added to libopx to support UI rendering of teletext text:

```csharp
public enum TeletextColor : byte
{
    Black = 0, Red = 1, Green = 2, Yellow = 3,
    Blue = 4, Magenta = 5, Cyan = 6, White = 7
}

public record ColorSpan(string Text, TeletextColor Foreground, TeletextColor? Background = null);
```

- `T42.GetColorSpans()` — same logic as `GetText()` but returns `List<ColorSpan>`
  instead of ANSI escape strings
- `Line.ColorSpans` property — populated during `ExtractMetadata()` alongside `Text`
- Additive change, no impact on existing consumers

## Filter Page (`/filter`)

### Controls
1. File path input (`MudTextField`) + Load button — auto-detects format from extension
2. Filter row:
   - Magazine select (`MudSelect`, 1-8, optional)
   - Row filter text input (comma-separated, ranges like "1,2,5-8")
   - Page number input
   - Caption rows toggle (`MudSwitch`)
   - Apply button

### Results
- `MudDataGrid` with virtualization (Blazor `<Virtualize>`)
- Columns: Timecode, Magazine, Row, Text
- Text column renders `ColorSpan` data as styled `<span>` elements with
  teletext foreground/background colors

### Data Flow
```
File path → FormatIO.Open(path).Filter(mag, rows).ParseLines()/ParsePackets()
  → List<CaptionLineViewModel> { Timecode, Magazine, Row, ColorSpans }
  → MudDataGrid with virtual scrolling
```

## Service Layer

`FilterService` — injected via DI, wraps `FormatIO` calls, returns view models.
Keeps page components thin.

## Project Structure

```
apps/opxBlazor/
├── opxBlazor.csproj
├── Program.cs
├── App.razor
├── Routes.razor
├── Layout/
│   ├── MainLayout.razor
│   └── NavMenu.razor
├── Pages/
│   └── Filter.razor
├── Services/
│   └── FilterService.cs
├── Models/
│   └── CaptionLineViewModel.cs
├── wwwroot/css/app.css
└── Properties/launchSettings.json
```

## Tech Stack

- .NET 10.0
- Blazor Server
- MudBlazor (NuGet)
- libopx (project reference)

## Out of Scope (for now)

- Restripe/Extract/Convert pages
- File upload
- Lines-per-frame or format override controls
