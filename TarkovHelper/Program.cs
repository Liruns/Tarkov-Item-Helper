using System.IO;
using TarkovHelper.Debug;
using TarkovHelper.Services;

namespace TarkovHelper;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Run migration before anything else
        MigrationService.RunMigrationIfNeeded();
        // CLI mode: --fetch-tasks
        if (args.Length > 0 && args[0] == "--fetch-tasks")
        {
            RunFetchTasksAsync().GetAwaiter().GetResult();
            return;
        }

        // CLI mode: --fetch-master-data
        if (args.Length > 0 && args[0] == "--fetch-master-data")
        {
            RunFetchMasterDataAsync().GetAwaiter().GetResult();
            return;
        }

        // CLI mode: --quest-graph [quest-name]
        if (args.Length > 0 && args[0] == "--quest-graph")
        {
            var questName = args.Length > 1 ? args[1] : null;
            RunQuestGraphAsync(questName).GetAwaiter().GetResult();
            return;
        }

        // CLI mode: --item-requirements [item-name]
        if (args.Length > 0 && args[0] == "--item-requirements")
        {
            var itemName = args.Length > 1 ? args[1] : null;
            RunItemRequirementsAsync(itemName).GetAwaiter().GetResult();
            return;
        }

        // CLI mode: --kappa-path
        if (args.Length > 0 && args[0] == "--kappa-path")
        {
            RunKappaPathAsync().GetAwaiter().GetResult();
            return;
        }

        // CLI mode: --compare-market
        if (args.Length > 0 && args[0] == "--compare-market")
        {
            RunCompareMarketAsync().GetAwaiter().GetResult();
            return;
        }

        // CLI mode: --test-quest-markers
        if (args.Length > 0 && args[0] == "--test-quest-markers")
        {
            Tests.QuestMarkerTest.RunAllTests().GetAwaiter().GetResult();
            return;
        }

        var app = new App();
        app.InitializeComponent();

        var mainWindow = new MainWindow();

        // Debug 모드에서 Toolbox 창 표시
        if (AppEnv.IsDebugMode)
        {
            TestMenu.MainWindow = mainWindow;
            var toolbox = new ToolboxWindow
            {

            };
            mainWindow.Loaded += (s, e) => {
                toolbox.Owner = mainWindow;
                toolbox.Show();
            };
        }

        app.Run(mainWindow);
    }

    private static async Task RunFetchTasksAsync()
    {
        Console.WriteLine("Fetching tasks from tarkov.dev API...");
        var service = TarkovDataService.Instance;

        var (tasks, missingTasks) = await service.RefreshTasksDataAsync(message =>
        {
            Console.WriteLine($"  {message}");
        });

        Console.WriteLine();
        Console.WriteLine($"=== Results ===");
        Console.WriteLine($"Total matched tasks: {tasks.Count}");
        Console.WriteLine($"Missing tasks: {missingTasks.Count}");
        Console.WriteLine($"Kappa required: {tasks.Count(t => t.ReqKappa)}");
        Console.WriteLine($"With Korean translation: {tasks.Count(t => !string.IsNullOrEmpty(t.NameKo))}");
        Console.WriteLine($"With Japanese translation: {tasks.Count(t => !string.IsNullOrEmpty(t.NameJa))}");
        Console.WriteLine();
        Console.WriteLine("Sample tasks:");
        foreach (var task in tasks.Take(5))
        {
            Console.WriteLine($"  - {task.Name}");
            if (!string.IsNullOrEmpty(task.NameKo))
                Console.WriteLine($"    KO: {task.NameKo}");
            if (!string.IsNullOrEmpty(task.NameJa))
                Console.WriteLine($"    JA: {task.NameJa}");
            Console.WriteLine($"    Kappa: {task.ReqKappa}");
        }

        if (missingTasks.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Missing Tasks (first 10) ===");
            foreach (var missing in missingTasks.Take(10))
            {
                Console.WriteLine($"  - {missing.Name}");
                Console.WriteLine($"    Reason: {missing.Reason}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Saved to {AppEnv.DataPath}/tasks.json");
        if (missingTasks.Count > 0)
            Console.WriteLine($"Missing tasks saved to {AppEnv.DataPath}/tasks_missing.json");
    }

    private static async Task RunFetchMasterDataAsync()
    {
        Console.WriteLine("Fetching master data from tarkov.dev API...");
        var service = TarkovDevApiService.Instance;

        var result = await service.RefreshMasterDataAsync(message =>
        {
            Console.WriteLine($"  {message}");
        });

        Console.WriteLine();
        Console.WriteLine($"=== Results ===");
        Console.WriteLine($"Items: {result.ItemCount}");
        Console.WriteLine($"  - With Korean: {result.ItemsWithKorean}");
        Console.WriteLine($"  - With Japanese: {result.ItemsWithJapanese}");
        Console.WriteLine($"Skills: {result.SkillCount}");
        Console.WriteLine($"  - With Korean: {result.SkillsWithKorean}");
        Console.WriteLine($"  - With Japanese: {result.SkillsWithJapanese}");

        if (!result.Success)
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
            return;
        }

        // Show sample items
        var items = await service.LoadItemsFromJsonAsync();
        if (items != null && items.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Sample items:");
            foreach (var item in items.Take(5))
            {
                Console.WriteLine($"  - {item.Name}");
                if (!string.IsNullOrEmpty(item.NameKo))
                    Console.WriteLine($"    KO: {item.NameKo}");
                if (!string.IsNullOrEmpty(item.NameJa))
                    Console.WriteLine($"    JA: {item.NameJa}");
                Console.WriteLine($"    Normalized: {item.NormalizedName}");
            }
        }

        // Show sample skills
        var skills = await service.LoadSkillsFromJsonAsync();
        if (skills != null && skills.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Sample skills:");
            foreach (var skill in skills.Take(5))
            {
                Console.WriteLine($"  - {skill.Name} (ID: {skill.Id})");
                if (!string.IsNullOrEmpty(skill.NameKo))
                    Console.WriteLine($"    KO: {skill.NameKo}");
                if (!string.IsNullOrEmpty(skill.NameJa))
                    Console.WriteLine($"    JA: {skill.NameJa}");
                Console.WriteLine($"    Normalized: {skill.NormalizedName}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Saved to {AppEnv.DataPath}/items.json");
        Console.WriteLine($"Saved to {AppEnv.DataPath}/skills.json");
    }

    private static async Task RunQuestGraphAsync(string? questName)
    {
        Console.WriteLine("Loading quest graph...");
        var graphService = QuestGraphService.Instance;
        await graphService.InitializeAsync();

        if (string.IsNullOrEmpty(questName))
        {
            // Show overall stats
            var stats = graphService.GetStats();
            Console.WriteLine();
            Console.WriteLine("=== Quest Graph Statistics ===");
            Console.WriteLine($"Total quests: {stats.TotalQuests}");
            Console.WriteLine($"Quests with prerequisites: {stats.QuestsWithPrerequisites}");
            Console.WriteLine($"Quests with follow-ups: {stats.QuestsWithFollowUps}");
            Console.WriteLine($"Quests with item requirements: {stats.QuestsWithItemRequirements}");
            Console.WriteLine($"Quests with skill requirements: {stats.QuestsWithSkillRequirements}");
            Console.WriteLine($"Quests with level requirements: {stats.QuestsWithLevelRequirements}");
            Console.WriteLine($"Kappa-required quests: {stats.KappaQuests}");
            Console.WriteLine();
            Console.WriteLine($"Starter quests (no prerequisites): {stats.StarterQuests.Count}");
            Console.WriteLine($"Terminal quests (no follow-ups): {stats.TerminalQuests.Count}");

            // Check for circular dependencies
            var cycles = graphService.DetectCircularDependencies();
            if (cycles.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"WARNING: {cycles.Count} circular dependencies detected!");
                foreach (var cycle in cycles.Take(5))
                {
                    Console.WriteLine($"  - {string.Join(" -> ", cycle.Cycle)}");
                }
            }
        }
        else
        {
            // Show quest details
            var task = graphService.GetTask(questName);
            if (task == null)
            {
                Console.WriteLine($"Quest not found: {questName}");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"=== Quest: {task.Name} ===");
            Console.WriteLine($"Trader: {task.Trader}");
            Console.WriteLine($"Kappa: {(task.ReqKappa ? "Required" : "Not required")}");
            if (task.RequiredLevel.HasValue)
                Console.WriteLine($"Required Level: {task.RequiredLevel}");

            // Prerequisites
            var directPrereqs = graphService.GetDirectPrerequisites(questName);
            Console.WriteLine();
            Console.WriteLine($"Direct Prerequisites ({directPrereqs.Count}):");
            foreach (var prereq in directPrereqs)
            {
                Console.WriteLine($"  - {prereq.Name}");
            }

            var allPrereqs = graphService.GetAllPrerequisites(questName);
            if (allPrereqs.Count > directPrereqs.Count + 1)
            {
                Console.WriteLine();
                Console.WriteLine($"All Prerequisites ({allPrereqs.Count - 1} quests, excluding target):");
                foreach (var prereq in allPrereqs.Take(allPrereqs.Count - 1))
                {
                    Console.WriteLine($"  - {prereq.Name}");
                }
            }

            // Follow-ups
            var directFollowUps = graphService.GetDirectFollowUps(questName);
            Console.WriteLine();
            Console.WriteLine($"Direct Follow-ups ({directFollowUps.Count}):");
            foreach (var followUp in directFollowUps)
            {
                Console.WriteLine($"  - {followUp.Name}");
            }

            // Required items
            if (task.RequiredItems != null && task.RequiredItems.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Required Items ({task.RequiredItems.Count}):");
                foreach (var item in task.RequiredItems)
                {
                    var firText = item.FoundInRaid ? " [FIR]" : "";
                    Console.WriteLine($"  - {item.ItemNormalizedName} x{item.Amount} ({item.Requirement}){firText}");
                }
            }

            // Required skills
            if (task.RequiredSkills != null && task.RequiredSkills.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Required Skills ({task.RequiredSkills.Count}):");
                foreach (var skill in task.RequiredSkills)
                {
                    Console.WriteLine($"  - {skill.SkillNormalizedName} Lv.{skill.Level}");
                }
            }

            // Optimal path
            Console.WriteLine();
            Console.WriteLine("Optimal Path (completion order):");
            var path = graphService.GetOptimalPath(questName);
            for (int i = 0; i < path.Count; i++)
            {
                var p = path[i];
                var marker = p.NormalizedName == questName ? " <-- TARGET" : "";
                Console.WriteLine($"  {i + 1}. {p.Name}{marker}");
            }
        }
    }

    private static async Task RunItemRequirementsAsync(string? itemName)
    {
        Console.WriteLine("Loading item requirements...");
        var itemService = ItemRequirementService.Instance;
        await itemService.InitializeAsync();

        if (string.IsNullOrEmpty(itemName))
        {
            // Show overall stats
            var stats = itemService.GetStats();
            Console.WriteLine();
            Console.WriteLine("=== Item Requirement Statistics ===");
            Console.WriteLine($"Total unique items required: {stats.TotalUniqueItems}");
            Console.WriteLine($"Total item count: {stats.TotalItemCount}");
            Console.WriteLine($"Total FIR item count: {stats.TotalFIRItemCount}");
            Console.WriteLine($"Quests with item requirements: {stats.QuestsWithItemRequirements}");

            Console.WriteLine();
            Console.WriteLine("Top 10 Required Items:");
            foreach (var item in stats.TopRequiredItems)
            {
                var firText = item.TotalFIRAmount > 0 ? $" ({item.TotalFIRAmount} FIR)" : "";
                Console.WriteLine($"  - {item.DisplayName}: {item.TotalAmount}{firText} (in {item.Quests.Count} quests)");
            }

            Console.WriteLine();
            Console.WriteLine("=== FIR Items Only ===");
            var firItems = itemService.GetFIRItems().Take(10);
            foreach (var item in firItems)
            {
                Console.WriteLine($"  - {item.DisplayName}: {item.TotalAmount} FIR (in {item.Quests.Count} quests)");
            }
        }
        else
        {
            // Search for item
            var searchResults = itemService.SearchItems(itemName);
            if (searchResults.Count == 0)
            {
                Console.WriteLine($"No items found matching: {itemName}");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Found {searchResults.Count} items matching '{itemName}':");

            foreach (var item in searchResults)
            {
                var quests = itemService.GetQuestsRequiringItem(item.NormalizedName);
                if (quests.Count == 0) continue;

                Console.WriteLine();
                Console.WriteLine($"=== {item.Name} ===");
                if (!string.IsNullOrEmpty(item.NameKo))
                    Console.WriteLine($"KO: {item.NameKo}");
                if (!string.IsNullOrEmpty(item.NameJa))
                    Console.WriteLine($"JA: {item.NameJa}");
                Console.WriteLine($"Normalized: {item.NormalizedName}");
                Console.WriteLine();
                Console.WriteLine($"Required by {quests.Count} quests:");
                foreach (var quest in quests)
                {
                    var firText = quest.FoundInRaid ? " [FIR]" : "";
                    Console.WriteLine($"  - {quest.QuestName}: x{quest.Amount} ({quest.Requirement}){firText}");
                }
            }
        }
    }

    private static async Task RunCompareMarketAsync()
    {
        Console.WriteLine("Comparing Wiki/tarkov.dev data with Tarkov Market...");
        Console.WriteLine();

        var tasksJson = await File.ReadAllTextAsync(Path.Combine(AppEnv.DataPath, "tasks.json"));
        var marketJsonPath = Path.Combine(AppEnv.DataPath, "tarkov_market_raw_response.json");

        if (!File.Exists(marketJsonPath))
        {
            Console.WriteLine("Error: tarkov_market_raw_response.json not found.");
            Console.WriteLine("Run a Refresh with Force option first to fetch Tarkov Market data.");
            return;
        }

        var marketJson = await File.ReadAllTextAsync(marketJsonPath);

        var tasks = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(tasksJson)!;
        var marketQuests = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(marketJson)!;

        Console.WriteLine($"Wiki/tarkov.dev tasks: {tasks.Count}");
        Console.WriteLine($"Tarkov Market quests: {marketQuests.Count}");
        Console.WriteLine();

        // Build lookups
        var tasksByMarketUid = new Dictionary<string, System.Text.Json.JsonElement>();
        foreach (var task in tasks)
        {
            if (task.TryGetProperty("tarkovMarketQuestUid", out var uidProp) &&
                uidProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var uid = uidProp.GetString();
                if (!string.IsNullOrEmpty(uid))
                    tasksByMarketUid[uid] = task;
            }
        }

        var marketByUid = new Dictionary<string, System.Text.Json.JsonElement>();
        foreach (var q in marketQuests)
        {
            var uid = q.GetProperty("uid").GetString();
            if (!string.IsNullOrEmpty(uid))
                marketByUid[uid] = q;
        }

        Console.WriteLine($"Matched quests: {tasksByMarketUid.Count}");
        Console.WriteLine();

        // === FACTION COMPARISON ===
        Console.WriteLine("=== FACTION COMPARISON ===");
        int factionMatch = 0, factionDiff = 0, factionWikiOnly = 0, factionMarketOnly = 0;
        var factionDiffs = new List<(string name, string? wiki, string? market)>();

        foreach (var kvp in tasksByMarketUid)
        {
            var marketUid = kvp.Key;
            var wikiTask = kvp.Value;

            if (!marketByUid.TryGetValue(marketUid, out var marketQuest))
                continue;

            string? wikiFaction = null;
            if (wikiTask.TryGetProperty("faction", out var wfp) && wfp.ValueKind == System.Text.Json.JsonValueKind.String)
                wikiFaction = wfp.GetString()?.ToLower();

            string? marketSide = null;
            if (marketQuest.TryGetProperty("side", out var msp) && msp.ValueKind == System.Text.Json.JsonValueKind.String)
                marketSide = msp.GetString()?.ToLower();

            // Normalize
            if (marketSide == "pmc") marketSide = null;
            if (wikiFaction == "any" || wikiFaction == "") wikiFaction = null;

            var taskName = wikiTask.GetProperty("name").GetString()!;

            if (wikiFaction == marketSide)
            {
                factionMatch++;
            }
            else if (wikiFaction == null && marketSide != null)
            {
                factionMarketOnly++;
                factionDiffs.Add((taskName, null, marketSide));
            }
            else if (wikiFaction != null && marketSide == null)
            {
                factionWikiOnly++;
                factionDiffs.Add((taskName, wikiFaction, null));
            }
            else
            {
                factionDiff++;
                factionDiffs.Add((taskName, wikiFaction, marketSide));
            }
        }

        Console.WriteLine($"Faction match: {factionMatch}");
        Console.WriteLine($"Faction differs: {factionDiff}");
        Console.WriteLine($"Wiki has faction, Market doesn't: {factionWikiOnly}");
        Console.WriteLine($"Market has side, Wiki doesn't: {factionMarketOnly}");
        Console.WriteLine();

        if (factionDiffs.Count > 0)
        {
            Console.WriteLine("Details:");
            foreach (var (name, wiki, market) in factionDiffs.Take(50))
            {
                var wikiStr = wiki ?? "null";
                var marketStr = market ?? "null";
                Console.WriteLine($"  {name}: wiki={wikiStr}, market={marketStr}");
            }
            if (factionDiffs.Count > 50)
                Console.WriteLine($"  ... and {factionDiffs.Count - 50} more");
        }
        Console.WriteLine();

        // === PREREQUISITES COMPARISON ===
        Console.WriteLine("=== PREREQUISITES COMPARISON ===");
        int prereqMatch = 0, prereqDiff = 0;
        int wikiHasMore = 0, marketHasMore = 0;
        var prereqDiffs = new List<(string name, int wiki, int market, List<string> wikiNames, List<string> marketNames)>();

        foreach (var kvp in tasksByMarketUid)
        {
            var marketUid = kvp.Key;
            var wikiTask = kvp.Value;

            if (!marketByUid.TryGetValue(marketUid, out var marketQuest))
                continue;

            var taskName = wikiTask.GetProperty("name").GetString()!;

            // Wiki previous
            var wikiPrevNames = new List<string>();
            if (wikiTask.TryGetProperty("previous", out var wpProp) && wpProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var p in wpProp.EnumerateArray())
                    if (p.ValueKind == System.Text.Json.JsonValueKind.String)
                        wikiPrevNames.Add(p.GetString()!);
            }

            // Market require
            var marketPrevNames = new List<string>();
            if (marketQuest.TryGetProperty("require", out var mrProp) && mrProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var r in mrProp.EnumerateArray())
                {
                    if (r.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var reqUid = r.GetString()!;
                        if (marketByUid.TryGetValue(reqUid, out var prereqQuest))
                            marketPrevNames.Add(prereqQuest.GetProperty("name").GetString()!);
                        else
                            marketPrevNames.Add($"[Unknown: {reqUid}]");
                    }
                }
            }

            if (wikiPrevNames.Count == marketPrevNames.Count)
            {
                prereqMatch++;
            }
            else
            {
                prereqDiff++;
                if (wikiPrevNames.Count > marketPrevNames.Count) wikiHasMore++;
                else marketHasMore++;

                if (prereqDiffs.Count < 50)
                    prereqDiffs.Add((taskName, wikiPrevNames.Count, marketPrevNames.Count, wikiPrevNames, marketPrevNames));
            }
        }

        Console.WriteLine($"Prereq count match: {prereqMatch}");
        Console.WriteLine($"Prereq count differs: {prereqDiff}");
        Console.WriteLine($"  - Wiki has more prereqs: {wikiHasMore}");
        Console.WriteLine($"  - Market has more prereqs: {marketHasMore}");
        Console.WriteLine();

        if (prereqDiffs.Count > 0)
        {
            Console.WriteLine("Sample differences:");
            foreach (var (name, wiki, market, wikiNames, marketNames) in prereqDiffs.Take(30))
            {
                Console.WriteLine($"  {name}: wiki={wiki}, market={market}");
                if (wikiNames.Count > 0)
                    Console.WriteLine($"    Wiki: [{string.Join(", ", wikiNames)}]");
                if (marketNames.Count > 0)
                    Console.WriteLine($"    Market: [{string.Join(", ", marketNames)}]");
            }
            if (prereqDiffs.Count > 30)
                Console.WriteLine($"  ... and {prereqDiffs.Count - 30} more");
        }
    }

    private static async Task RunKappaPathAsync()
    {
        Console.WriteLine("Calculating Kappa path...");

        var graphService = QuestGraphService.Instance;
        await graphService.InitializeAsync();

        var itemService = ItemRequirementService.Instance;
        await itemService.InitializeAsync();

        var kappaPath = graphService.GetKappaPath();
        var kappaItems = itemService.GetKappaItems();

        Console.WriteLine();
        Console.WriteLine($"=== Kappa Path ===");
        Console.WriteLine($"Total quests for Kappa: {kappaPath.Count}");

        // Group by trader
        var byTrader = kappaPath.GroupBy(t => t.Trader).OrderByDescending(g => g.Count());
        Console.WriteLine();
        Console.WriteLine("Quests by Trader:");
        foreach (var group in byTrader)
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} quests");
        }

        Console.WriteLine();
        Console.WriteLine("Quest Completion Order (first 20):");
        for (int i = 0; i < Math.Min(20, kappaPath.Count); i++)
        {
            var quest = kappaPath[i];
            var levelReq = quest.RequiredLevel.HasValue ? $" [Lv.{quest.RequiredLevel}]" : "";
            Console.WriteLine($"  {i + 1}. {quest.Name} ({quest.Trader}){levelReq}");
        }

        if (kappaPath.Count > 20)
        {
            Console.WriteLine($"  ... and {kappaPath.Count - 20} more quests");
        }

        Console.WriteLine();
        Console.WriteLine($"=== Items Required for Kappa ===");
        Console.WriteLine($"Unique items: {kappaItems.Count}");
        Console.WriteLine($"Total items: {kappaItems.Sum(i => i.TotalAmount)}");
        Console.WriteLine($"Total FIR items: {kappaItems.Sum(i => i.TotalFIRAmount)}");

        Console.WriteLine();
        Console.WriteLine("Top 15 Required Items:");
        foreach (var item in kappaItems.Take(15))
        {
            var firText = item.TotalFIRAmount > 0 ? $" ({item.TotalFIRAmount} FIR)" : "";
            Console.WriteLine($"  - {item.DisplayName}: {item.TotalAmount}{firText}");
        }
    }
}
