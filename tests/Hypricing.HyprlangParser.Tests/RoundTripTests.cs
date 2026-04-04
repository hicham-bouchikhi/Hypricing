using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class RoundTripTests
{
    [Theory]
    [InlineData("$myvar = hello\n")]
    [InlineData("gaps_in = 5\n")]
    [InlineData("bind = SUPER,Q,killactive\n")]
    [InlineData("# this is a comment\n")]
    [InlineData("\n")]
    [InlineData("source = ~/.config/hypr/monitors.conf\n")]
    [InlineData("exec-once = waybar\n")]
    [InlineData("exec-once = [workspace 1 silent] kitty\n")]
    public void RoundTrip_SingleLine(string input)
    {
        var config = HyprlangParser.Parse(input);
        string output = HyprlangWriter.Write(config);
        Assert.Equal(input, output);
    }

    [Fact]
    public void RoundTrip_MinimalConfig()
    {
        const string input = """
            $mod = SUPER
            gaps_in = 5
            bind = $mod,Q,killactive

            """;
        var config = HyprlangParser.Parse(input);
        Assert.Equal(input, HyprlangWriter.Write(config));
    }

    [Fact]
    public void RoundTrip_ConfigWithComments()
    {
        const string input = """
            # My config
            $mod = SUPER
            # Gaps
            gaps_in = 5

            """;
        var config = HyprlangParser.Parse(input);
        Assert.Equal(input, HyprlangWriter.Write(config));
    }

    [Fact]
    public void RoundTrip_ConfigWithBlankLines()
    {
        const string input = "$mod = SUPER\n\n\ngaps_in = 5\n";
        var config = HyprlangParser.Parse(input);
        Assert.Equal(input, HyprlangWriter.Write(config));
    }

    [Fact]
    public void RoundTrip_ConfigWithNestedSections()
    {
        const string input = "general {\n    gaps_in = 5\n    gaps_out = 10\n}\n\ndecoration {\n    blur {\n        enabled = true\n    }\n}\n";
        var config = HyprlangParser.Parse(input);
        Assert.Equal(input, HyprlangWriter.Write(config));
    }

    [Fact]
    public void RoundTrip_InlineComments()
    {
        const string input = "gaps_in = 5 # main gap\nborder_size = 2 # px\n";
        var config = HyprlangParser.Parse(input);
        Assert.Equal(input, HyprlangWriter.Write(config));
    }

    [Fact]
    public void RoundTrip_DoubleHashEscape()
    {
        const string input = "title = My app ## note\n";
        var config = HyprlangParser.Parse(input);
        Assert.Equal(input, HyprlangWriter.Write(config));
    }

    [Fact]
    public void RoundTrip_FullExampleConfig()
    {
        const string input = """
            # Hyprland config

            $mod = SUPER

            monitor = DP-1,1920x1080@144,0x0,1

            env = XCURSOR_SIZE,24
            env = QT_QPA_PLATFORM,wayland

            exec-once = waybar
            exec-once = dunst
            exec-once = [workspace 1 silent] kitty

            source = ~/.config/hypr/keybinds.conf

            general {
                gaps_in = 5
                gaps_out = 20
                border_size = 2
                col.active_border = rgba(33ccffee) rgba(00ff99ee) 45deg
            }

            decoration {
                rounding = 10
                blur {
                    enabled = true
                    size = 3
                }
            }

            bind = $mod,Q,killactive
            bind = $mod,Return,exec,kitty

            """;
        var config = HyprlangParser.Parse(input);
        Assert.Equal(input, HyprlangWriter.Write(config));
    }
}
