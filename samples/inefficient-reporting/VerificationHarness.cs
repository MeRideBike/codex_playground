namespace InefficientReporting;

internal static class VerificationHarness
{
    public static int Main()
    {
        var actual = PrefixReport.SumEveryPrefix([1, 2, 3]);
        if (actual != 10)
        {
            Console.Error.WriteLine($"Expected the prefix report total to equal 10, but received {actual}.");
            return 1;
        }

        Console.WriteLine("Prefix report verification passed.");
        return 0;
    }
}
