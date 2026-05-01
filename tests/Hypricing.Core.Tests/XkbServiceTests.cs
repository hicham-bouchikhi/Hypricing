using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;

namespace Hypricing.Core.Tests;

public class XkbServiceTests
{
    [Fact]
    public async Task GetLayoutsAsync_SplitsOutputIntoLines()
    {
        var cli = new FakeCliRunner("us\nfr\nde\n");
        var service = new XkbService(cli);

        var layouts = await service.GetLayoutsAsync();

        Assert.Equal(3, layouts.Length);
        Assert.Contains("us", layouts);
        Assert.Contains("fr", layouts);
        Assert.Contains("de", layouts);
    }

    [Fact]
    public async Task GetVariantsAsync_PrependEmptyEntryForNoVariant()
    {
        var cli = new FakeCliRunner("intl\ndvorak\n");
        var service = new XkbService(cli);

        var variants = await service.GetVariantsAsync("us");

        Assert.Equal(3, variants.Length);
        Assert.Equal(string.Empty, variants[0]);
        Assert.Equal("intl", variants[1]);
        Assert.Equal("dvorak", variants[2]);
    }

    [Fact]
    public async Task GetVariantsAsync_ReturnsEmptyForBlankLayout()
    {
        var cli = new FakeCliRunner("should not be called");
        var service = new XkbService(cli);

        var variants = await service.GetVariantsAsync("   ");

        Assert.Empty(variants);
    }

    [Fact]
    public async Task GetVariantsAsync_ReturnsEmptyOnCliFailure()
    {
        var cli = new ThrowingCliRunner();
        var service = new XkbService(cli);

        var variants = await service.GetVariantsAsync("us");

        Assert.Empty(variants);
    }

    private sealed class FakeCliRunner(string response) : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => Task.FromResult(response);
    }

    private sealed class ThrowingCliRunner : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated failure");
    }
}
