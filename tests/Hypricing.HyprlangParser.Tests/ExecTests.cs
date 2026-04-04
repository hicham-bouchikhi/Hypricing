using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class ExecTests
{
    [Theory]
    [InlineData("exec-once = waybar\n", ExecVariant.Once, "waybar")]
    [InlineData("exec-once = dunst\n", ExecVariant.Once, "dunst")]
    [InlineData("exec = ~/.config/hypr/start.sh\n", ExecVariant.Reload, "~/.config/hypr/start.sh")]
    [InlineData("exec-shutdown = poweroff\n", ExecVariant.Shutdown, "poweroff")]
    [InlineData("execr-once = waybar\n", ExecVariant.OnceRestart, "waybar")]
    [InlineData("execr = hyprpaper\n", ExecVariant.ExecrReload, "hyprpaper")]
    public void Parse_Exec_VariantsAndCommands(string input, ExecVariant expectedVariant, string expectedCommand)
    {
        var config = HyprlangParser.Parse(input);

        var exec = Assert.IsType<ExecNode>(Assert.Single(config.Children));
        Assert.Equal(expectedVariant, exec.Variant);
        Assert.Equal(expectedCommand, exec.Command);
        Assert.Null(exec.Rules);
    }

    [Fact]
    public void Parse_Exec_WithRules()
    {
        var config = HyprlangParser.Parse("exec-once = [workspace 1 silent] kitty\n");

        var exec = Assert.IsType<ExecNode>(Assert.Single(config.Children));
        Assert.Equal(ExecVariant.Once, exec.Variant);
        Assert.Equal("workspace 1 silent", exec.Rules);
        Assert.Equal("kitty", exec.Command);
    }

    [Fact]
    public void Parse_Exec_WithMultipleRules()
    {
        var config = HyprlangParser.Parse("exec-once = [float; workspace 2] foot\n");

        var exec = Assert.IsType<ExecNode>(Assert.Single(config.Children));
        Assert.Equal(ExecVariant.Once, exec.Variant);
        Assert.Equal("float; workspace 2", exec.Rules);
        Assert.Equal("foot", exec.Command);
    }
}
