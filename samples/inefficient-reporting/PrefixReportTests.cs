using Xunit;

namespace InefficientReporting;

public sealed class PrefixReportTests
{
    [Fact]
    public void SumEveryPrefixReturnsTheExpectedTotal()
    {
        Assert.Equal(10, PrefixReport.SumEveryPrefix([1, 2, 3]));
    }
}
