using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NavalCombat.Unity.Editor
{
    public sealed class NavalWebPortraits : IPostprocessBuildWithReport
    {
        const int MaxLongEdge = 320;
        const int JpegQuality = 85;
        const string PortraitFolder = "NavalCombat/Pictures/Leaders";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report) =>
            Process(report.summary.platform, report.summary.outputPath);

        internal static void Process(BuildTarget target, string buildDirectory)
        {
            if (target != BuildTarget.WebGL) return;

            string source = Path.Combine(UnityEngine.Application.streamingAssetsPath, PortraitFolder);
            string output = Path.Combine(buildDirectory, "StreamingAssets", PortraitFolder);
            Directory.CreateDirectory(output);
            foreach (string path in Directory.EnumerateFiles(source))
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png") continue;

                string destination = Path.Combine(output, Path.GetFileName(path));
                // Always start from the full-size asset, including during incremental builds.
                byte[] bytes = File.ReadAllBytes(path);
                File.WriteAllBytes(destination, Resize(path, bytes, extension == ".png"));
            }

            // An incremental output can still contain a portrait under its former PNG name.
            foreach (string png in Directory.EnumerateFiles(output, "*.png"))
            {
                string original = Path.Combine(source, Path.GetFileName(png));
                if (!File.Exists(original) && File.Exists(Path.ChangeExtension(original, ".jpg")))
                    File.Delete(png);
            }
        }

        static byte[] Resize(string path, byte[] original, bool png)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Texture2D thumbnail = null;
            try
            {
                if (!image.LoadImage(original))
                    throw new BuildFailedException($"Unable to decode leader portrait: {path}");

                int longEdge = Math.Max(image.width, image.height);
                if (longEdge <= MaxLongEdge) return original;

                float scale = (float)MaxLongEdge / longEdge;
                int width = Math.Max(1, Mathf.RoundToInt(image.width * scale));
                int height = Math.Max(1, Mathf.RoundToInt(image.height * scale));
                thumbnail = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                thumbnail.SetPixels32(Downsample(image, width, height));
                thumbnail.Apply(false);
                return png ? thumbnail.EncodeToPNG() : thumbnail.EncodeToJPG(JpegQuality);
            }
            finally
            {
                if (thumbnail != null) UnityEngine.Object.DestroyImmediate(thumbnail);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        // Average the source area covered by each output pixel, including fractional edges.
        static Color32[] Downsample(Texture2D image, int width, int height)
        {
            var source = image.GetPixels32();
            var result = new Color32[width * height];
            int sourceWidth = image.width, sourceHeight = image.height;
            float stepX = (float)sourceWidth / width, stepY = (float)sourceHeight / height;
            for (int y = 0; y < height; y++)
            {
                float top = y * stepY, bottom = Mathf.Min((y + 1) * stepY, sourceHeight);
                for (int x = 0; x < width; x++)
                {
                    float left = x * stepX, right = Mathf.Min((x + 1) * stepX, sourceWidth);
                    Color sum = Color.clear;
                    for (int row = (int)top; row < bottom; row++)
                    {
                        float overlapY = Mathf.Min(bottom, row + 1) - Mathf.Max(top, row);
                        for (int col = (int)left; col < right; col++)
                        {
                            float overlapX = Mathf.Min(right, col + 1) - Mathf.Max(left, col);
                            Color pixel = source[row * sourceWidth + col];
                            float weight = pixel.a * overlapX * overlapY;
                            sum.r += pixel.r * weight;
                            sum.g += pixel.g * weight;
                            sum.b += pixel.b * weight;
                            sum.a += weight;
                        }
                    }
                    if (sum.a > 0)
                        result[y * width + x] = new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a,
                            sum.a / ((right - left) * (bottom - top)));
                }
            }
            return result;
        }
    }
}
