namespace Pkmds.Rcl.Services;

public readonly record struct FixOutcome(bool Changed, Severity Severity, string Message);
