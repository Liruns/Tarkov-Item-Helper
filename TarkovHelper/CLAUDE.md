# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build

# Run WPF GUI application
dotnet run

# Run CLI mode to fetch fresh data from API
dotnet run -- --fetch
```

## Architecture Overview

This is a WPF (.NET 8) application for Escape from Tarkov quest tracking. It fetches quest and item data from the tarkov.dev GraphQL API and supports bilingual display (English/Korean).

### Data Flow

```
tarkov.dev GraphQL API
        ↓
TarkovApiService (fetches EN + KO data in parallel)
        ↓
TaskDatasetManager (merges, saves/loads JSON)
        ↓
Data/tasks.json, Data/items.json
        ↓
MainWindow (UI display)
```

### Key Components

**Models/**
- `TaskData.cs` - Quest model with EN/KO names, prerequisites, follow-ups, and objectives
- `ItemData.cs` - Item model with EN/KO names, wiki links, icons
- `TaskObjective.cs` - Quest objective with required items, counts, and FIR (Found in Raid) flag
- `GraphQL/TarkovApiResponse.cs` - API response DTOs

**Services/**
- `TarkovApiService.cs` - GraphQL client for tarkov.dev API, handles bilingual data fetching
- `TaskDatasetManager.cs` - JSON persistence, prerequisite chain resolution (`GetAllPrerequisites`, `GetTasksToAutoComplete`)

**Entry Point**
- `Program.cs` - Custom Main with `[STAThread]` for WPF; supports `--fetch` CLI mode

### Important Patterns

- Quest prerequisites are stored as ID lists; use `TaskDatasetManager.GetAllPrerequisites()` for recursive chain resolution
- Item objectives track `FoundInRaid` boolean to distinguish FIR requirements from regular item submissions
- Data is cached locally; delete `Data/*.json` files to force API refresh on next launch
