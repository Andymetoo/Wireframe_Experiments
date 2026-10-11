using System;
using System.Numerics;

namespace Meridian
{
    /// <summary>Continuous, seeded island terrain shared by collision queries and mesh generation.</summary>
    public sealed class TerrainField
    {
        public const float WaterLevel = 0;
        public const float HalfExtent = 15000;
        public readonly int Seed;
        public readonly Vector3 AirfieldPosition;

        public TerrainField(int seed)
        {
            Seed = seed;
            AirfieldPosition = new Vector3(-2600, 35, -2800);
        }

        public float Height(float x, float z)
        {
            // A broad coastal island, cut with valleys. Heights are independent of mesh resolution.
            float stretchX = x * .000095f;
            float stretchZ = z * .000074f;
            float radius = (float)Math.Sqrt(stretchX * stretchX + stretchZ * stretchZ);
            float coast = Clamp((1.03f - radius + (Noise(x * .00034f, z * .00034f) - .5f) * .36f) * 4, 0, 1);
            float hills = 28 + 410 * Noise(x * .00031f + 42, z * .00031f - 27)
                + 125 * Noise(x * .00091f - 31, z * .00091f + 17)
                + 30 * Noise(x * .0023f, z * .0023f);
            float valley = .52f + .48f * Math.Abs((float)Math.Sin(x * .00045f + z * .00028f));
            float height = Math.Max(0, coast * hills * valley - 18);
            float airfieldDistance = Math.Max(Math.Abs(x - AirfieldPosition.X) / 1400,
                Math.Abs(z - AirfieldPosition.Z) / 460);
            float blend = Clamp((airfieldDistance - 1) / .65f, 0, 1);
            blend = blend * blend * (3 - 2 * blend);
            return 35 + (height - 35) * blend;
        }

        private float Noise(float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float tx = x - ix, ty = y - iy;
            tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty;
        }

        private float Hash(int x, int y)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + Seed * 144269);
                n = (n ^ (n >> 13)) * 1274126177;
                return (n ^ (n >> 16)) / (float)uint.MaxValue;
            }
        }

        private static float Clamp(float v, float lo, float hi) { return Math.Max(lo, Math.Min(hi, v)); }
    }
}
