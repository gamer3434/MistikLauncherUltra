using System.IO;
using MistikLauncher;

static class LoggingTests
{
    public static int Run()
    {
        string marker = "parallel-log-fixture-" + Guid.NewGuid().ToString("N") + "-";
        Parallel.For(0, 128, index => App.Log(marker + index));
        var recorded = File.ReadAllLines(App.LogFile).Where(line => line.Contains(marker, StringComparison.Ordinal)).ToArray();
        if (recorded.Length != 128 || recorded.Distinct().Count() != 128)
            throw new Exception("Concurrent diagnostic messages must all be recorded once; found " + recorded.Length + "/128.");
        Console.WriteLine("PASS concurrent diagnostic messages are recorded exactly once without file-sharing losses");
        return 1;
    }
}
