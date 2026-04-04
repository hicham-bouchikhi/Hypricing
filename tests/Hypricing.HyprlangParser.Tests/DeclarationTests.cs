using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class DeclarationTests
{
    [Theory]
    [InlineData("$myvar = hello\n", "myvar", "hello")]
    [InlineData("$cursor_size = 24\n", "cursor_size", "24")]
    [InlineData("$mod = SUPER\n", "mod", "SUPER")]
    [InlineData("$var = value with spaces\n", "var", "value with spaces")]
    [InlineData("$var=nospace\n", "var", "nospace")]
    public void Parse_Declaration_ExtractsNameAndValue(string input, string expectedName, string expectedValue)
    {
        var config = HyprlangParser.Parse(input);

        var node = Assert.Single(config.Children);
        var decl = Assert.IsType<DeclarationNode>(node);
        Assert.Equal(expectedName, decl.Name);
        Assert.Equal(expectedValue, decl.Value);
    }

    [Fact]
    public void Parse_Declaration_EmptyValue()
    {
        var config = HyprlangParser.Parse("$empty =\n");

        var decl = Assert.IsType<DeclarationNode>(Assert.Single(config.Children));
        Assert.Equal("empty", decl.Name);
        Assert.Equal("", decl.Value);
    }

    [Fact]
    public void Parse_Declaration_NoSpaceAroundEquals()
    {
        var config = HyprlangParser.Parse("$var=nospace\n");

        var decl = Assert.IsType<DeclarationNode>(Assert.Single(config.Children));
        Assert.Equal("var", decl.Name);
        Assert.Equal("nospace", decl.Value);
    }
}
