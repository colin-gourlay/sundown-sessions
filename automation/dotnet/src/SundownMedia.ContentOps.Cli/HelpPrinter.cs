namespace SundownMedia.ContentOps.Cli;

public static class HelpPrinter
{
    public static void Print()
    {
        Console.WriteLine("SundownMedia ContentOps CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  contentops intake start --source <path> --working-root <path> --master-root <path> [--correlation-id <guid>]");
    }
}
