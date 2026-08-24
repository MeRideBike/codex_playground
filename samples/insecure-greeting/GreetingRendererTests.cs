using Xunit;

namespace InsecureGreeting;

public sealed class GreetingRendererTests
{
    [Fact]
    public void RenderEncodesMarkupInTheName()
    {
        Assert.Equal("<h1>Hello &lt;admin&gt;</h1>", GreetingRenderer.Render("<admin>"));
    }
}
