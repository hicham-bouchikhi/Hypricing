using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class CommentTests
{
    [Fact]
    public void Parse_FullLineComment()
    {
        var config = HyprlangParser.Parse("# this is a comment\n");

        var comment = Assert.IsType<CommentNode>(Assert.Single(config.Children));
        Assert.Equal("# this is a comment", comment.Text);
    }

    [Fact]
    public void Parse_CommentedOutAssignment()
    {
        var config = HyprlangParser.Parse("# gaps_in = 5\n");

        var comment = Assert.IsType<CommentNode>(Assert.Single(config.Children));
        Assert.Equal("# gaps_in = 5", comment.Text);
    }

    [Fact]
    public void Parse_InlineComment_Assignment()
    {
        var config = HyprlangParser.Parse("gaps_in = 5 # inline\n");

        var assign = Assert.IsType<AssignmentNode>(Assert.Single(config.Children));
        Assert.Equal("5", assign.Value);
        Assert.Equal("# inline", assign.InlineComment);
    }

    [Fact]
    public void Parse_DoubleHashEscape_Assignment()
    {
        var config = HyprlangParser.Parse("title = My app ## note\n");

        var assign = Assert.IsType<AssignmentNode>(Assert.Single(config.Children));
        Assert.Equal("My app # note", assign.Value);
        Assert.Null(assign.InlineComment);
    }

    [Fact]
    public void Parse_DoubleHashEscape_Keyword()
    {
        var config = HyprlangParser.Parse("bind = SUPER,Q,exec ## cmd\n");

        var kw = Assert.IsType<KeywordNode>(Assert.Single(config.Children));
        Assert.Equal("SUPER,Q,exec # cmd", kw.Params);
        Assert.Null(kw.InlineComment);
    }
}
