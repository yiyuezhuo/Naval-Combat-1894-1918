using System.Numerics;

namespace NavalCombat.Avalonia.Controls;

/// <summary>Immutable geometry identity and a small per-object GPU transform.</summary>
internal readonly record struct NavalSceneInstance(float[] Mesh, Vector3 Origin, float Cos, float Sin,
    float Scale = 1, bool White = false, bool Sea = false)
{
    internal Vector3 Transform(Vector3 p) => Origin + new Vector3(p.X*Cos+p.Y*Sin,-p.X*Sin+p.Y*Cos,p.Z)*Scale;
}
