using Xunit;
namespace DefectiveCalculator;
public sealed class CalculatorTests { [Fact] public void Subtract_removes_the_right_operand() => Assert.Equal(2, Calculator.Subtract(5, 3)); }
