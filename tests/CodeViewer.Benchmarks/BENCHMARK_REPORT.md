# CodeViewer Performance & Profiling Report

- **Date (UTC):** 2026-11-11 15:41:55Z
- **Runtime:** .NET 8.0.26
- **OS:** Microsoft Windows 10.0.26300 (X64)
- **CPUs:** 12
- **Total Runtime:** 6.57s

## 1. File I/O & Encoding

| Benchmark | Mean | Median (P50) | P95 | Ops/sec | Alloc/op | GC (0/1/2) | Notes |
|---|---:|---:|---:|---:|---:|---:|---|
| OpenFileAsync (10 KB C#) | 0.383 ms | 0.323 ms | 0.590 ms | 2,609.6 | 299.1 KB | 1/1/1 | UTF-8 seq read + line-ending scan |
| OpenFileAsync (1 MB Dart) | 3.12 ms | 3.10 ms | 4.00 ms | 320.9 | 4.28 MB | 13/13/13 | 64KB buffered stream |
| OpenFileAsync (10 MB Text) | 39.47 ms | 39.01 ms | 46.92 ms | 25.3 | 40.31 MB | 6/6/6 | Large file cold/warm memory profile |
| SaveFileAsync (500 KB atomic write) | 27.67 ms | 29.01 ms | 29.78 ms | 36.1 | 1.98 MB | 3/3/3 | Temp file + atomic File.Move |

## 2. Themes & Syntax Highlighting

| Benchmark | Mean | Median (P50) | P95 | Ops/sec | Alloc/op | GC (0/1/2) | Notes |
|---|---:|---:|---:|---:|---:|---:|---|
| BuiltInThemes.GetAll (20 themes) | 4.18 us | 3.30 us | 9.20 us | 239,320.3 | 5.1 KB | 0/0/0 | ColorTheme dictionary instantiation |
| ThemeService Cold Initialization | 0.035 ms | 0.030 ms | 0.098 ms | 28,275.2 | 6.0 KB | 0/0/0 | Sync theme lookup + async IO defer |
| LanguageService.DetectLanguage (10 exts) | 1.20 us | 0.95 us | 1.60 us | 832,408.4 | 320 B | 0/0/0 | 10 extension classifications per op |
| HighlightingThemeApplicator (7 langs) | 0.035 ms | 0.031 ms | 0.054 ms | 28,511.1 | 10.1 KB | 0/0/0 | Live syntax brush switch across open tabs |

## 3. Document Symbol Extraction

| Benchmark | Mean | Median (P50) | P95 | Ops/sec | Alloc/op | GC (0/1/2) | Notes |
|---|---:|---:|---:|---:|---:|---:|---|
| ExtractSymbols C# (~1,000 lines) | 0.666 ms | 0.530 ms | 0.862 ms | 1,501.1 | 330.2 KB | 4/0/0 | Compiled regex + class/method/prop pass |
| ExtractSymbols C# (~5,000 lines) | 2.55 ms | 2.45 ms | 3.35 ms | 392.1 | 1.62 MB | 8/4/0 | Large source file outline scaling |
| ExtractSymbols Python (~1,000 lines) | 0.909 ms | 0.691 ms | 1.21 ms | 1,100.4 | 753.9 KB | 9/3/0 |  |
| ExtractSymbols JS/TS (~1,000 lines) | 0.511 ms | 0.417 ms | 1.15 ms | 1,955.9 | 219.3 KB | 2/0/0 |  |
| ExtractSymbols Markdown (1,000 lines) | 0.333 ms | 0.151 ms | 0.534 ms | 3,000.0 | 260.9 KB | 5/2/0 |  |
| ExtractSymbols JSON (1,000+ lines) | 0.213 ms | 0.147 ms | 0.311 ms | 4,691.1 | 365.0 KB | 4/2/0 |  |

## 4. Markdown Live Preview

| Benchmark | Mean | Median (P50) | P95 | Ops/sec | Alloc/op | GC (0/1/2) | Notes |
|---|---:|---:|---:|---:|---:|---:|---|
| Render Rich Markdown (Small, ~45 lines) | 3.34 ms | 3.01 ms | 6.56 ms | 299.3 | 597.1 KB | 7/2/0 | Headings, table, tasks, code, inline formatting |
| Render Rich Markdown (Medium, ~525 lines) | 24.42 ms | 24.94 ms | 40.35 ms | 40.9 | 6.78 MB | 22/19/0 | Typ. README / design doc live preview cost |
| Render Rich Markdown (Large, ~2,400 lines) | 200.0 ms | 199.8 ms | 254.1 ms | 5.0 | 30.93 MB | 37/22/7 | Stress test: tables + code + RTL mixed blocks |
| IsRtlText Scan (1,000 strings batch) | 0.063 ms | 0.042 ms | 0.062 ms | 15,791.9 | 0 B | 0/0/0 | Bidirectional script detection hot loop |

## 5. Diff Engine & Workspace Search

| Benchmark | Mean | Median (P50) | P95 | Ops/sec | Alloc/op | GC (0/1/2) | Notes |
|---|---:|---:|---:|---:|---:|---:|---|
| DiffService LCS (400 lines, 5% diff) | 1.88 ms | 1.62 ms | 2.74 ms | 531.2 | 865.0 KB | 6/5/5 | Dynamic programming LCS path |
| DiffService Myers Fallback (3,500 lines) | 4.19 ms | 1.38 ms | 20.74 ms | 238.8 | 2.26 MB | 3/1/1 | Large file greedy diff path |
| WorkspaceSearch Plain (80 files) | 4.16 ms | 4.12 ms | 5.74 ms | 240.3 | 368.5 KB | 0/0/0 | Parallel file scan + OrdinalIgnoreCase |
| WorkspaceSearch WholeWord Regex (80 files) | 6.71 ms | 6.30 ms | 11.34 ms | 149.1 | 651.1 KB | 1/0/0 | Compiled word-boundary regex across workspace |
| CommandPalette Filter (60 cmds) | 0.037 ms | 0.034 ms | 0.051 ms | 27,253.2 | 10.2 KB | 0/0/0 | 3 queries per op |

## 6. Git & Editor Pipeline

| Benchmark | Mean | Median (P50) | P95 | Ops/sec | Alloc/op | GC (0/1/2) | Notes |
|---|---:|---:|---:|---:|---:|---:|---|
| GitService.ParsePorcelainLine (1k lines) | 0.232 ms | 0.184 ms | 0.395 ms | 4,318.4 | 200.8 KB | 6/0/0 | Zero-process porcelain parser throughput |
| GitService.GetStatusAsync (Live Repo) | 144.1 ms | 132.4 ms | 187.1 ms | 6.9 | 69.8 KB | 0/0/0 | Async git status --porcelain=v1 process |
| DocumentViewModel Init (1,000 lines C#) | 1.19 ms | 1.13 ms | 1.45 ms | 842.2 | 174.6 KB | 1/0/0 | TextDocument allocation + syntax binding |
| ToggleComment (1,000 lines) | 8.54 ms | 7.15 ms | 16.65 ms | 117.1 | 889.7 KB | 2/0/0 | UndoGroup batch line prefix toggle |
| SmartBracketEditHandler (1k keystrokes) | 4.46 ms | 1.70 ms | 20.90 ms | 224.0 | 1.40 MB | 47/14/2 | Auto-closing pair insertion latency |

