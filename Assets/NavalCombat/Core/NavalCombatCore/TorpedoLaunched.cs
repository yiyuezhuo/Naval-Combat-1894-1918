using System;

namespace NavalCombatCore;

/// <summary>Transient presentation notification. Loading an existing torpedo never emits this event.</summary>
public sealed record TorpedoLaunchedEvent(DateTime LaunchTime, string ShooterId, string TorpedoId,
    double LatitudeDeg, double LongitudeDeg);
