using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser.Tests;

public class ModificationTests
{
    [Fact]
    public void Modify_DeclarationValue_WritesBack()
    {
        var config = HyprlangParser.Parse("$myvar = hello\n$other = world\n");

        var decl = (DeclarationNode)config.Children[0];
        decl.Value = "newvalue";

        string output = HyprlangWriter.Write(config);
        Assert.Contains("$myvar = newvalue", output);
        Assert.Contains("$other = world", output);
    }

    [Fact]
    public void Modify_AssignmentInsideSection_WritesBack()
    {
        const string input = "general {\n    gaps_in = 5\n    gaps_out = 10\n}\n";
        var config = HyprlangParser.Parse(input);

        var section = (SectionNode)config.Children[0];
        var assign = (AssignmentNode)section.Children[0];
        assign.Value = "8";

        string output = HyprlangWriter.Write(config);
        Assert.Contains("gaps_in = 8", output);
        Assert.Contains("gaps_out = 10", output);
    }

    [Fact]
    public void Add_NewDeclaration_AppearsInOutput()
    {
        var config = HyprlangParser.Parse("$mod = SUPER\n");

        config.Children.Add(new DeclarationNode("newvar", "42"));

        string output = HyprlangWriter.Write(config);
        Assert.Contains("$mod = SUPER", output);
        Assert.Contains("$newvar = 42", output);
    }

    [Fact]
    public void Remove_ExecOnce_LineGone()
    {
        var config = HyprlangParser.Parse("exec-once = waybar\nexec-once = dunst\n");

        var waybar = config.Children.First(n => n is ExecNode e && e.Command == "waybar");
        config.Children.Remove(waybar);

        string output = HyprlangWriter.Write(config);
        Assert.DoesNotContain("waybar", output);
        Assert.Contains("dunst", output);
    }

    [Fact]
    public void Add_NewExecOnce_AppearsInOutput()
    {
        var config = HyprlangParser.Parse("exec-once = waybar\n");

        config.Children.Add(new ExecNode(ExecVariant.Once, "swaync"));

        string output = HyprlangWriter.Write(config);
        Assert.Contains("waybar", output);
        Assert.Contains("exec-once = swaync", output);
    }
}
