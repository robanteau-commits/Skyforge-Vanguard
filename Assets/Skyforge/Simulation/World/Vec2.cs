using System;

namespace Skyforge.Simulation
{
    /// <summary>
    /// 2D vector in play-field units. X is right, Y is forward (towards the top of the screen).
    /// The simulation is engine-free, so it has its own small vector type.
    /// </summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 Up = new Vec2(0f, 1f);

        public float LengthSq => X * X + Y * Y;
        public float Length => (float)Math.Sqrt(LengthSq);

        public Vec2 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-6f ? new Vec2(X / l, Y / l) : Zero;
            }
        }

        /// <summary>This vector, shortened to at most <paramref name="max"/> long.</summary>
        public Vec2 ClampLength(float max)
        {
            float l2 = LengthSq;
            if (l2 <= max * max) return this;
            float s = max / (float)Math.Sqrt(l2);
            return new Vec2(X * s, Y * s);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static float DistanceSq(Vec2 a, Vec2 b) => (a - b).LengthSq;

        /// <summary>Unit vector at <paramref name="degrees"/> clockwise from straight up.</summary>
        public static Vec2 FromHeading(float degrees)
        {
            double r = degrees * Math.PI / 180.0;
            return new Vec2((float)Math.Sin(r), (float)Math.Cos(r));
        }

        public bool Equals(Vec2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
