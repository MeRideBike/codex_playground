namespace InsecureGreeting;

internal static class VerificationHarness
{
    public static int Main()
    {
        const string expected = "<h1>Hello &lt;admin&gt;</h1>";
        var actual = GreetingRenderer.Render("<admin>");
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Expected encoded greeting '{expected}', but received '{actual}'.");
            return 1;
        }

        Console.WriteLine("Greeting renderer verification passed.");
        return 0;
    }
}
