namespace Hypricing.Core.Models;

/// <summary>
/// Live monitor information returned by <c>hyprctl monitors -j</c>.
/// </summary>
public sealed record MonitorInfo(string Name, IReadOnlyList<string> AvailableModes);

