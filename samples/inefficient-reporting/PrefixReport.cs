namespace InefficientReporting;

public static class PrefixReport
{
    public static int SumEveryPrefix(IReadOnlyList<int> values)
    {
        var total = 0;
        for (var index = 1; index <= values.Count; index++) total += values.Take(index).Sum();
        return total;
    }
}
