using ISTDocAi.Provisioning;

namespace ISTDocAi.OperatorTool;

// Local extension keeps the shared provisioning console and other tools unchanged.
public interface IOperatorConsole : IProvisionConsole
{
    bool IsInputRedirected { get; }
}

public sealed partial class OperatorMenu(IProvisionConsole console)
{
    public Func<string, string, bool, Task<AzurePriceStatus>>? PriceStatusReader { get; init; }
    private AzurePriceStatus? priceStatus;
    private bool priceStatusDirty;
    private async Task RefreshPrices()
    {
        priceStatusDirty = false;
        try { priceStatus = await (PriceStatusReader ?? AzurePriceStatus.Read)(server!, database, TrustServerCertificate == true); }
        catch (Exception) { priceStatus = null; }
    }
    public const string Text = """
        ISTDocAi Operator
        1) Pages & balance
        2) Customer reports
        3) IST reports
        4) Models
        5) Setup & administration
        9) Show all commands (command line)
        c) Change SQL connection
        0) Exit
        """;
    private string consumer = "";
    private string? server;
    private string database = "ISTDocAi";
    public bool? TrustServerCertificate { get; private set; }
    public static bool ShouldStart(string[] args, IProvisionConsole console) =>
        args.Length == 0 && !(console is IOperatorConsole input ? input.IsInputRedirected : Console.IsInputRedirected);

    public async Task<int> Run(Func<string[], Task<int>> command)
    {
        string? section = null;
        while (true)
        {
            if (section is null && server is not null)
            {
                if (priceStatusDirty) await RefreshPrices();
                if (priceStatus?.Banner() is { Length: > 0 } banner) console.WriteLine(banner);
            }
            console.WriteLine(section is null ? Text : Submenus[int.Parse(section) - 1]);
            console.Write("Choice: ");
            var choice = console.ReadLine()?.Trim();
            if (choice is null) return 0;
            if (choice == "0") { if (section is null) return 0; section = null; continue; }
            if (section is null && choice is "1" or "2" or "3" or "4" or "5") { section = choice; continue; }
            if (!(section is null ? choice is "9" or "c" : int.TryParse(choice, out var n) && n >= 1 && n <= new[] { 6, 6, 4, 9, 5 }[int.Parse(section) - 1]))
            { console.WriteLine("Invalid choice. Try again."); continue; }
            try
            {
                if (section is null && choice == "c") Connection();
                else if (section == "4" && choice == "2") await Upgrade(command);
                else if (section == "4" && choice == "6") await ApplyReview(command);
                else
                {
                    var args = section is null ? new[] { "--help" } : await Build(section, choice);
                    if (args is not null)
                    {
                        var result = await command(args);
                        if (args is ["prices", "show", ..]) await RefreshPrices();
                        if (result != 0) console.WriteLine($"Action ended with exit code {result}.");
                    }
                }
            }
            catch (CliCancelled) { console.WriteLine("CANCELLED: No further action taken."); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { console.WriteLine("REFUSED: " + e.Message); }
            catch (Exception) { console.WriteLine("REFUSED: Menu action failed; details suppressed."); }
            console.WriteLine("Press Enter to return to the menu");
            if (console.ReadLine() is null) return 0;
        }
    }
    private void Connection()
    {
        var nextServer = Required("SQL Server", server);
        var nextDatabase = Required("Database", database);
        SqlRunner.ValidateNames(nextServer, nextDatabase);
        var trust = CliPrompts.Ask(console, "TrustServerCertificate? yes/no", TrustServerCertificate == true ? "yes" : "no").Equals("yes", StringComparison.OrdinalIgnoreCase);
        priceStatusDirty = true;
        server = nextServer;
        database = nextDatabase;
        TrustServerCertificate = trust;
    }
    private string Required(string label, string? fallback = null)
    {
        while (true)
        {
            var value = CliPrompts.Ask(console, label, fallback);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            console.WriteLine(label + " is required.");
        }
    }
}
