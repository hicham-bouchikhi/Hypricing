using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class SectionTests
{
    [Fact]
    public void Parse_Section_SingleAssignment()
    {
        var config = HyprlangParser.Parse("general {\n    gaps_in = 5\n}\n");

        var section = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.Equal("general", section.Name);
        Assert.Null(section.Device);

        var assign = Assert.IsType<AssignmentNode>(Assert.Single(section.Children));
        Assert.Equal("gaps_in", assign.Key);
        Assert.Equal("5", assign.Value);
    }

    [Fact]
    public void Parse_Section_MultipleAssignments()
    {
        var config = HyprlangParser.Parse("general {\n    gaps_in = 5\n    gaps_out = 10\n}\n");

        var section = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.Equal(2, section.Children.Count);

        var a1 = Assert.IsType<AssignmentNode>(section.Children[0]);
        Assert.Equal("gaps_in", a1.Key);

        var a2 = Assert.IsType<AssignmentNode>(section.Children[1]);
        Assert.Equal("gaps_out", a2.Key);
    }

    [Fact]
    public void Parse_Section_NestedSections()
    {
        var input = "decoration {\n    blur {\n        enabled = true\n    }\n}\n";
        var config = HyprlangParser.Parse(input);

        var outer = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.Equal("decoration", outer.Name);

        var inner = Assert.IsType<SectionNode>(Assert.Single(outer.Children));
        Assert.Equal("blur", inner.Name);

        var assign = Assert.IsType<AssignmentNode>(Assert.Single(inner.Children));
        Assert.Equal("enabled", assign.Key);
        Assert.Equal("true", assign.Value);
    }

    [Fact]
    public void Parse_Section_EmptySection()
    {
        var config = HyprlangParser.Parse("general {\n}\n");

        var section = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.Equal("general", section.Name);
        Assert.Empty(section.Children);
    }

    [Fact]
    public void Parse_Section_DeclarationInsideSectionBecomesRawNode()
    {
        var config = HyprlangParser.Parse("general {\n    $var = x\n    gaps_in = 5\n}\n");

        var section = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.Equal(2, section.Children.Count);
        Assert.IsType<RawNode>(section.Children[0]);
        Assert.IsType<AssignmentNode>(section.Children[1]);
    }

    [Fact]
    public void Parse_Section_DeviceSection()
    {
        var config = HyprlangParser.Parse("device:my-keyboard {\n    kb_layout = us\n}\n");

        var section = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.Equal("device", section.Name);
        Assert.Equal("my-keyboard", section.Device);

        var assign = Assert.IsType<AssignmentNode>(Assert.Single(section.Children));
        Assert.Equal("kb_layout", assign.Key);
    }

    [Fact]
    public void Parse_Section_SingleLine()
    {
        var config = HyprlangParser.Parse("general { gaps_in = 5 }\n");

        var section = Assert.IsType<SectionNode>(Assert.Single(config.Children));
        Assert.Equal("general", section.Name);

        var assign = Assert.IsType<AssignmentNode>(Assert.Single(section.Children));
        Assert.Equal("gaps_in", assign.Key);
        Assert.Equal("5", assign.Value);
    }
}
