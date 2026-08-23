namespace DefectiveCalculator;

public static class UserInputFormatter
{
    // Deliberately unsafe presentation helper for a future security-analysis mission.
    public static string RenderGreeting(string name) => $"<h1>Hello {name}</h1>";
}
