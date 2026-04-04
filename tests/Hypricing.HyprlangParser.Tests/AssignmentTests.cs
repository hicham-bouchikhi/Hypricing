using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class AssignmentTests
{
    [Theory]
    [InlineData("gaps_in = 5\n", "gaps_in", "5")]
    [InlineData("gaps_out = 20\n", "gaps_out", "20")]
    [InlineData("enabled = true\n", "enabled", "true")]
    [InlineData("enabled = false\n", "enabled", "false")]
    [InlineData("rounding = 0\n", "rounding", "0")]
    public void Parse_Assignment_ExtractsKeyAndValue(string input, string expectedKey, string expectedValue)
    {
        var config = HyprlangParser.Parse(input);

        var node = Assert.Single(config.Children);
        var assign = Assert.IsType<AssignmentNode>(node);
        Assert.Equal(expectedKey, assign.Key);
        Assert.Equal(expectedValue, assign.Value);
    }

    [Fact]
    public void Parse_Assignment_GradientValue()
    {
        var config = HyprlangParser.Parse("col.active_border = rgba(33ccffee) rgba(00ff99ee) 45deg\n");

        var assign = Assert.IsType<AssignmentNode>(Assert.Single(config.Children));
        Assert.Equal("col.active_border", assign.Key);
        Assert.Equal("rgba(33ccffee) rgba(00ff99ee) 45deg", assign.Value);
    }

    [Fact]
    public void Parse_Assignment_EmptyValue()
    {
        var config = HyprlangParser.Parse("key =\n");

        var assign = Assert.IsType<AssignmentNode>(Assert.Single(config.Children));
        Assert.Equal("key", assign.Key);
        Assert.Equal("", assign.Value);
    }
}
