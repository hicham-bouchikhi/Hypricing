using Hypricing.HyprlangParser.Exceptions;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class EdgeCaseTests
{
    [Fact]
    public void Parse_EmptyFile()
    {
        var config = HyprlangParser.Parse("");
        Assert.Empty(config.Children);
        Assert.Equal("", HyprlangWriter.Write(config));
    }

    [Fact]
    public void Parse_OnlyComments()
    {
        const string input = "# comment 1\n# comment 2\n";
        var config = HyprlangParser.Parse(input);
        Assert.Equal(2, config.Children.Count);
        Assert.All(config.Children, c => Assert.IsType<CommentNode>(c));
        Assert.Equal(input, HyprlangWriter.Write(config));
    }

    [Fact]
    public void Parse_DeeplyNestedSections()
    {
        const string input = "a {\n    b {\n        c {\n            key = val\n        }\n    }\n}\n";
        var config = HyprlangParser.Parse(input);

        var a = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        var b = Assert.IsType<SectionNode>(Assert.Single(a.Children));
        var c = Assert.IsType<SectionNode>(Assert.Single(b.Children));
        var assign = Assert.IsType<AssignmentNode>(Assert.Single(c.Children));
        Assert.Equal("key", assign.Key);
        Assert.Equal("val", assign.Value);

        Assert.Equal(input, HyprlangWriter.Write(config));
    }

    [Fact]
    public void Parse_MissingClosingBrace_Throws()
    {
        Assert.Throws<ParseException>(() => HyprlangParser.Parse("general {\n    gaps_in = 5\n"));
    }

    [Fact]
    public void Parse_DeviceSection()
    {
        var config = HyprlangParser.Parse("device:my-keyboard {\n    kb_layout = us\n}\n");

        var section = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.Equal("device", section.Name);
        Assert.Equal("my-keyboard", section.Device);
    }

    [Fact]
    public void Parse_CrlfLineEndings()
    {
        var config = HyprlangParser.Parse("$mod = SUPER\r\ngaps_in = 5\r\n");

        Assert.Equal(2, config.Children.Count);
        Assert.IsType<DeclarationNode>(config.Children[0]);
        Assert.IsType<AssignmentNode>(config.Children[1]);
    }

    [Fact]
    public void Parse_DeclarationInsideSection_BecomesRawNode()
    {
        var config = HyprlangParser.Parse("general {\n    $var = x\n}\n");

        var section = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.IsType<RawNode>(Assert.Single(section.Children));
    }

    [Fact]
    public void Parse_ExecrOnce()
    {
        var config = HyprlangParser.Parse("execr-once = waybar\n");

        var exec = Assert.IsType<ExecNode>(Assert.Single(config.Children));
        Assert.Equal(ExecVariant.OnceRestart, exec.Variant);
        Assert.Equal("waybar", exec.Command);
    }
}
