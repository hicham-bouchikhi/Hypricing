using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;

namespace Hypricing.Core.Tests;

public class PowerServiceTests
{
    [Fact]
    public async Task GetProfilesAsync_ParsesAllProfiles()
    {
        const string output = """
              performance:
                Driver:     driver1
            * balanced:
                Driver:     driver2
              power-saver:
                Driver:     driver3
            """;
        var cli = new FakeCliRunner(output);
        var service = new PowerService(cli);

        var (profiles, active) = await service.GetProfilesAsync();

        Assert.Equal(3, profiles.Length);
        Assert.Contains("performance", profiles);
        Assert.Contains("balanced", profiles);
        Assert.Contains("power-saver", profiles);
    }

    [Fact]
    public async Task GetProfilesAsync_IdentifiesActiveProfile()
    {
        const string output = """
              performance:
            * balanced:
              power-saver:
            """;
        var cli = new FakeCliRunner(output);
        var service = new PowerService(cli);

        var (_, active) = await service.GetProfilesAsync();

        Assert.Equal("balanced", active);
    }

    [Fact]
    public async Task SetProfileAsync_SendsCorrectCommand()
    {
        var cli = new RecordingCliRunner();
        var service = new PowerService(cli);

        await service.SetProfileAsync("power-saver");

        Assert.Single(cli.Invocations);
        Assert.Equal(("powerprofilesctl", "set power-saver"), cli.Invocations[0]);
    }

    [Fact]
    public async Task GetBatteryInfoAsync_ParsesBatteryInfo()
    {
        const string devicesOutput = "/org/freedesktop/UPower/devices/battery_BAT0\n";
        const string batteryInfo = """
              native-path:          BAT0
              power supply:         yes
              present:             yes
              state:               discharging
              percentage:          72%
              time to empty:       3.5 hours
            """;

        var cli = new MapCliRunner(new()
        {
            [("upower", "-e")] = devicesOutput,
            [("upower", "-i /org/freedesktop/UPower/devices/battery_BAT0")] = batteryInfo,
        });
        var service = new PowerService(cli);

        var info = await service.GetBatteryInfoAsync();

        Assert.NotNull(info);
        Assert.True(info.Present);
        Assert.Equal("discharging", info.State);
        Assert.Equal(72.0, info.Percentage, 0.01);
        Assert.Equal("3.5 hours", info.TimeEstimate);
    }

    [Fact]
    public async Task GetBatteryInfoAsync_ParsesTimeToFull()
    {
        const string devicesOutput = "/org/freedesktop/UPower/devices/battery_BAT0\n";
        const string batteryInfo = """
              power supply:         yes
              present:             yes
              state:               charging
              percentage:          45%
              time to full:        1.2 hours
            """;

        var cli = new MapCliRunner(new()
        {
            [("upower", "-e")] = devicesOutput,
            [("upower", "-i /org/freedesktop/UPower/devices/battery_BAT0")] = batteryInfo,
        });
        var service = new PowerService(cli);

        var info = await service.GetBatteryInfoAsync();

        Assert.NotNull(info);
        Assert.Equal("charging", info.State);
        Assert.Equal("1.2 hours", info.TimeEstimate);
    }

    [Fact]
    public async Task GetBatteryInfoAsync_SkipsPeripheralBatteries()
    {
        const string devicesOutput =
            "/org/freedesktop/UPower/devices/battery_mouse\n" +
            "/org/freedesktop/UPower/devices/battery_BAT0\n";

        // Mouse has "power supply: no", laptop battery has "power supply: yes"
        const string mouseInfo = "  power supply:         no\n  present:             yes\n  percentage:          80%\n";
        const string laptopInfo = "  power supply:         yes\n  present:             yes\n  state:               discharging\n  percentage:          60%\n";

        var cli = new MapCliRunner(new()
        {
            [("upower", "-e")] = devicesOutput,
            [("upower", "-i /org/freedesktop/UPower/devices/battery_mouse")] = mouseInfo,
            [("upower", "-i /org/freedesktop/UPower/devices/battery_BAT0")] = laptopInfo,
        });
        var service = new PowerService(cli);

        var info = await service.GetBatteryInfoAsync();

        Assert.NotNull(info);
        Assert.Equal(60.0, info.Percentage, 0.01);
    }

    [Fact]
    public async Task GetBatteryInfoAsync_ReturnsNullWhenNoBatteryDevice()
    {
        var cli = new MapCliRunner(new()
        {
            [("upower", "-e")] = "/org/freedesktop/UPower/devices/DisplayDevice\n",
        });
        var service = new PowerService(cli);

        var info = await service.GetBatteryInfoAsync();

        Assert.Null(info);
    }

    [Fact]
    public async Task GetBatteryInfoAsync_ReturnsNullOnCliFailure()
    {
        var cli = new ThrowingCliRunner();
        var service = new PowerService(cli);

        var info = await service.GetBatteryInfoAsync();

        Assert.Null(info);
    }

    private sealed class FakeCliRunner(string response) : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => Task.FromResult(response);
    }

    private sealed class MapCliRunner(Dictionary<(string, string), string> map) : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => Task.FromResult(map.TryGetValue((command, arguments), out var r) ? r : string.Empty);
    }

    private sealed class RecordingCliRunner : CliRunner
    {
        public List<(string Command, string Arguments)> Invocations { get; } = [];

        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
        {
            Invocations.Add((command, arguments));
            return Task.FromResult(string.Empty);
        }
    }

    private sealed class ThrowingCliRunner : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated failure");
    }
}
