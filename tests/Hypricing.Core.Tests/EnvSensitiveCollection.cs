namespace Hypricing.Core.Tests;

// Tests that mutate XDG_CONFIG_HOME must not run in parallel with each other.
[CollectionDefinition("env-sensitive")]
public class EnvSensitiveCollection { }
