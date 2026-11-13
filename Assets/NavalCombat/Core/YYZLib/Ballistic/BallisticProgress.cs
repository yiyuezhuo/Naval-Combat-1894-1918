namespace YYZ.Ballistic;

public enum BallisticStage { Preparing, Seeding, Trajectory, Penetration, Charts }

/// <summary>Completed work within the named phase; Total == 0 means indeterminate.</summary>
public sealed record BallisticProgress(BallisticStage Stage, int Completed = 0, int Total = 0);
