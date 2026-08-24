namespace DefectiveCalculator;

internal static class VerificationHarness
{
    public static int Main()
    {
        var actual = Calculator.Subtract(5, 3);
        if (actual != 2)
        {
            Console.Error.WriteLine($"Expected Calculator.Subtract(5, 3) to equal 2, but received {actual}.");
            return 1;
        }

        Console.WriteLine("Calculator verification passed.");
        return 0;
    }
}
