using System.Numerics;

namespace MeijiPolitics.Rendering;

/// <summary>Adds neighbouring face normals for silhouette/crease ink without triangulation seams.
/// Input vertices are position/color/normal; output appends the three opposite-edge neighbours.</summary>
internal static class SketchMesh
{
    public const int Stride = 21;
    public static float[] Expand(float[] source, int triangleVertices, int smoothTriangleVertices = 0)
    {
        var count = source.Length / 9;
        var result = new float[count * Stride];
        var edges = new Dictionary<(Vector3, Vector3), List<(int Triangle, int Edge)>>();
        Vector3 Read(int vertex, int offset = 0) => new(source[vertex * 9 + offset], source[vertex * 9 + offset + 1], source[vertex * 9 + offset + 2]);
        // Quantization joins repeated vertices generated through slightly different arithmetic.
        static Vector3 Key(Vector3 p) => new(MathF.Round(p.X * 10000), MathF.Round(p.Y * 10000), MathF.Round(p.Z * 10000));
        static bool Before(Vector3 a, Vector3 b) => a.X < b.X || a.X == b.X && (a.Y < b.Y || a.Y == b.Y && a.Z < b.Z);
        var smooth = new Dictionary<Vector3, Vector3>();
        for (var t = 0; t + 2 < smoothTriangleVertices; t += 3)
        {
            var areaNormal = Vector3.Cross(Read(t + 1) - Read(t), Read(t + 2) - Read(t));
            if (areaNormal.Z < 0) areaNormal = -areaNormal;
            for (var j = 0; j < 3; j++)
            {
                var key = Key(Read(t + j));
                smooth[key] = smooth.GetValueOrDefault(key) + areaNormal;
            }
        }
        for (var v = 0; v < count; v++)
        {
            Array.Copy(source, v * 9, result, v * Stride, 9);
            var n = v < smoothTriangleVertices ? smooth[Key(Read(v))] : Read(v, 6);
            n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.UnitZ;
            result[v * Stride + 18] = n.X; result[v * Stride + 19] = n.Y; result[v * Stride + 20] = n.Z;
        }
        for (var t = 0; t + 2 < triangleVertices; t += 3)
            for (var e = 0; e < 3; e++)
            {
                var a = Key(Read(t + (e + 1) % 3)); var b = Key(Read(t + (e + 2) % 3));
                var key = Before(a, b) ? (a, b) : (b, a);
                if (!edges.TryGetValue(key, out var faces)) edges[key] = faces = [];
                faces.Add((t, e));
            }
        foreach (var faces in edges.Values)
        {
            // Boundary/non-manifold edges remain inked (zero neighbouring normal).
            if (faces.Count != 2) continue;
            foreach (var face in faces)
            {
                var other = faces[0] == face ? faces[1] : faces[0];
                var n = Read(other.Triangle, 6);
                // Material boundaries are intentional details even when coplanar.
                if (Vector3.DistanceSquared(Read(face.Triangle, 3), Read(other.Triangle, 3)) > .0025f) continue;
                for (var v = 0; v < 3; v++)
                {
                    var at = (face.Triangle + v) * Stride + 9 + face.Edge * 3;
                    result[at] = n.X; result[at + 1] = n.Y; result[at + 2] = n.Z;
                }
            }
        }
        return result;
    }

    public const string VertexDeclarations = """
        in vec3 neighbour0, neighbour1, neighbour2;
        flat out vec3 inkEdges;
        out float pencilShade;
        float edgeInk(vec3 n, vec3 adjacent, vec3 view) {
            if (length(adjacent) < 0.1) return 1.0;
            vec3 a = normalize(adjacent);
            float crease = abs(dot(n, a));
            // Suppress diagonals and smooth tessellation; retain silhouette edges and hard folds.
            return crease < 0.78 || (crease < 0.9999 && dot(n, view) * dot(a, view) <= 0.0) ? 1.0 : 0.0;
        }
        """;

    public const string FragmentDeclarations = """
        flat in vec3 inkEdges;
        in float pencilShade;
        float contourInk(vec3 barycentric) {
            vec3 distanceToEdge = barycentric / max(fwidth(barycentric), vec3(0.000001));
            vec3 edge = (1.0 - smoothstep(vec3(0.45), vec3(1.4), distanceToEdge)) * inkEdges;
            return max(edge.x, max(edge.y, edge.z));
        }
        vec4 cleanPieceColor(vec3 barycentric) {
            float tone = 0.97 - clamp(pencilShade, 0.0, 1.0) * 0.65;
            return vec4(vec3(mix(tone, 0.06, contourInk(barycentric))), 1.0);
        }
        """;
}
