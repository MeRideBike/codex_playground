namespace DefectiveCalculator;

public static class ReportGenerator
{
    // Deliberately inefficient: repeatedly enumerates the complete input.
    public static int SumEveryPrefix(IReadOnlyList<int> values)
    {
        var total = 0;
        for (var index = 1; index <= values.Count; index++) total += values.Take(index).Sum();
        return total;
    }
}
