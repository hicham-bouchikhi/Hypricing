using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class SourceTests
{
    [Theory]
    [InlineData("source = ~/.config/hypr/monitors.conf\n", "~/.config/hypr/monitors.conf")]
    [InlineData("source = ~/.config/hypr/keybinds.conf\n", "~/.config/hypr/keybinds.conf")]
    public void Parse_Source_ExtractsPath(string input, string expectedPath)
    {
        var config = HyprlangParser.Parse(input);

        var source = Assert.IsType<SourceNode>(Assert.Single(config.Children));
        Assert.Equal(expectedPath, source.Path);
    }

    [Fact]
    public void Parse_Source_NoSpaceAroundEquals()
    {
        var config = HyprlangParser.Parse("source=~/.config/hypr/other.conf\n");

        var source = Assert.IsType<SourceNode>(Assert.Single(config.Children));
        Assert.Equal("~/.config/hypr/other.conf", source.Path);
    }
}
