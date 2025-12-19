---
description: Stage and commit changes by feature using conventional commits
---

Analyze current changes and create feature-based commits.

## Instructions

1. **Analyze current changes**
   - Run `git status` to see all modified/untracked files
   - Run `git diff` to see the actual changes
   - Group changes by feature or functionality

2. **Group changes by feature**
   Categorize files into logical groups based on:
   - Related functionality (e.g., Map features, EFT events, UI components)
   - Common purpose (e.g., bug fix, refactoring, new feature)
   - Affected area (e.g., Services, Pages, Models)

3. **For each feature group, create a commit**
   - Stage only the files in that group: `git add <file1> <file2> ...`
   - Create a commit with conventional commit format
   - Verify with `git status` before moving to next group

4. **Commit message format**
   Use conventional commits:
   ```
   <type>(<scope>): <description>

   [optional body with details]

   🤖 Generated with [Claude Code](https://claude.com/claude-code)

   Co-Authored-By: Claude <noreply@anthropic.com>
   ```

   Types:
   - `feat`: New feature
   - `fix`: Bug fix
   - `refactor`: Code refactoring (no functional change)
   - `docs`: Documentation changes
   - `style`: Formatting, whitespace (no code change)
   - `chore`: Maintenance tasks
   - `perf`: Performance improvements

   Scopes (examples for this project):
   - `map`: Map tracking features
   - `quest`: Quest related
   - `eft`: EFT log/raid events
   - `ui`: UI components
   - `db`: Database related
   - `prd`: PRD management

5. **Present plan before executing**
   Before staging and committing, show the user:
   - Proposed commit groups
   - Files in each group
   - Proposed commit message for each
   Ask for confirmation before proceeding.

## Example Output

```
📊 Detected Changes Analysis:

Group 1: Map Coordinate System Updates
  Files:
    - Models/Map/MapConfig.cs
    - Models/Map/MapTrackerSettings.cs
    - Pages/Map/MapPage.xaml.cs
  Proposed commit: feat(map): update coordinate system configuration

Group 2: Map Marker Improvements
  Files:
    - Pages/Map/Components/MapExtractMarkerManager.cs
    - Pages/Map/Components/MapQuestMarkerManager.cs
  Proposed commit: refactor(map): improve marker manager components

Group 3: EFT Raid Event Service
  Files:
    - Services/EftRaidEventService.cs
  Proposed commit: fix(eft): update raid event handling

Proceed with commits? [Y/n]
```

## Important Notes

- Never force push or use destructive git commands
- Always show diff summary before committing
- Ask user to confirm each commit group
- If changes are too intermingled, suggest manual intervention
- Handle deleted files appropriately with `git add -u` or explicit paths
- For untracked directories, ask if they should be added
