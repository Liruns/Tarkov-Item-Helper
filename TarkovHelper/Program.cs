using TarkovHelper.Services;

namespace TarkovHelper;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 콘솔 앱 모드로 데이터셋 생성 테스트
        if (args.Contains("--fetch"))
        {
            FetchDataAsync().GetAwaiter().GetResult();
            return;
        }

        // WPF 앱 실행
        var app = new App();
        app.InitializeComponent();
        app.Run(new MainWindow());
    }

    private static async Task FetchDataAsync()
    {
        Console.WriteLine("Fetching data from Tarkov API...");

        try
        {
            var (taskDataset, itemDataset, hideoutDataset) = await TaskDatasetManager.FetchAndSaveAllAsync();

            Console.WriteLine($"\nData fetched successfully!");
            Console.WriteLine($"- Generated At: {taskDataset.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");

            // Task 통계
            Console.WriteLine($"\n[Tasks]");
            Console.WriteLine($"- Total: {taskDataset.Tasks.Count}");
            Console.WriteLine($"- Saved to: {TaskDatasetManager.DefaultDataPath}");

            // Item 통계
            Console.WriteLine($"\n[Items]");
            Console.WriteLine($"- Total: {itemDataset.Items.Count}");
            Console.WriteLine($"- Saved to: {TaskDatasetManager.DefaultItemDataPath}");

            // Hideout 통계
            Console.WriteLine($"\n[Hideouts]");
            Console.WriteLine($"- Total Stations: {hideoutDataset.Hideouts.Count}");
            Console.WriteLine($"- Saved to: {TaskDatasetManager.DefaultHideoutDataPath}");

            // Hideout 아이템 요구사항 통계
            var totalItemReqs = hideoutDataset.Hideouts
                .SelectMany(h => h.Levels)
                .SelectMany(l => l.ItemRequirements)
                .ToList();
            var firItemReqs = totalItemReqs.Where(r => r.FoundInRaid).ToList();

            Console.WriteLine($"- Total Item Requirements: {totalItemReqs.Count}");
            Console.WriteLine($"- Found in Raid Requirements: {firItemReqs.Count}");

            Console.WriteLine("\nSample hideout stations:");
            foreach (var hideout in hideoutDataset.Hideouts.Take(5))
            {
                var levelCount = hideout.Levels.Count;
                var itemCount = hideout.Levels.Sum(l => l.ItemRequirements.Count);
                Console.WriteLine($"  - {hideout.NameEn} / {hideout.NameKo}");
                Console.WriteLine($"    Levels: {levelCount}, Item Requirements: {itemCount}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
