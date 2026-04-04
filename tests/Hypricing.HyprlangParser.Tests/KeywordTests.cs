using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class KeywordTests
{
    [Theory]
    [InlineData("bind = SUPER,Q,killactive\n", "bind", "SUPER,Q,killactive")]
    [InlineData("monitor = DP-1,1920x1080@144,0x0,1\n", "monitor", "DP-1,1920x1080@144,0x0,1")]
    [InlineData("env = XCURSOR_SIZE,24\n", "env", "XCURSOR_SIZE,24")]
    [InlineData("env = QT_QPA_PLATFORM,wayland\n", "env", "QT_QPA_PLATFORM,wayland")]
    public void Parse_Keyword_ExtractsKeywordAndParams(string input, string expectedKeyword, string expectedParams)
    {
        var config = HyprlangParser.Parse(input);

        var node = Assert.Single(config.Children);
        var kw = Assert.IsType<KeywordNode>(node);
        Assert.Equal(expectedKeyword, kw.Keyword);
        Assert.Equal(expectedParams, kw.Params);
    }

    [Fact]
    public void Parse_Keyword_WindowruleWithParens()
    {
        var config = HyprlangParser.Parse("windowrule = float,^(pavucontrol)$\n");

        var kw = Assert.IsType<KeywordNode>(Assert.Single(config.Children));
        Assert.Equal("windowrule", kw.Keyword);
        Assert.Equal("float,^(pavucontrol)$", kw.Params);
    }

    [Fact]
    public void Parse_Keyword_BindWithVariableRef()
    {
        var config = HyprlangParser.Parse("bind = $mod,Return,exec,kitty\n");

        var kw = Assert.IsType<KeywordNode>(Assert.Single(config.Children));
        Assert.Equal("bind", kw.Keyword);
        Assert.Equal("$mod,Return,exec,kitty", kw.Params);
    }

    [Fact]
    public void Parse_Keyword_EmptyMiddleParam()
    {
        var config = HyprlangParser.Parse("bind = SUPER,,killactive\n");

        var kw = Assert.IsType<KeywordNode>(Assert.Single(config.Children));
        Assert.Equal("bind", kw.Keyword);
        Assert.Equal("SUPER,,killactive", kw.Params);
    }
}
