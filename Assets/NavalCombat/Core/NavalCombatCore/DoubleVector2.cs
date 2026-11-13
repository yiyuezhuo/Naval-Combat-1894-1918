using System;

namespace NavalCombatCore
{
    /// <summary>
    /// Minimal double-precision replacement for System.Numerics.Vector2 in
    /// model-side calculations that must not cross a single-precision boundary.
    /// </summary>
    public readonly struct DoubleVector2
    {
        public readonly double X;
        public readonly double Y;

        public DoubleVector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double LengthSquared() => X * X + Y * Y;

        public static DoubleVector2 Normalize(DoubleVector2 value)
        {
            var length = Math.Sqrt(value.LengthSquared());
            return length <= double.Epsilon ? default : value * (1d / length);
        }

        public static double Dot(DoubleVector2 left, DoubleVector2 right) => left.X * right.X + left.Y * right.Y;

        public static DoubleVector2 operator +(DoubleVector2 left, DoubleVector2 right) => new(left.X + right.X, left.Y + right.Y);
        public static DoubleVector2 operator -(DoubleVector2 left, DoubleVector2 right) => new(left.X - right.X, left.Y - right.Y);
        public static DoubleVector2 operator -(DoubleVector2 value) => new(-value.X, -value.Y);
        public static DoubleVector2 operator *(DoubleVector2 value, double scalar) => new(value.X * scalar, value.Y * scalar);
        public static DoubleVector2 operator *(double scalar, DoubleVector2 value) => value * scalar;
    }
}
