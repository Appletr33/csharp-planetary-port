using System;

namespace PlanetaryTerrainRenderer.Math
{
    public static class SimplexNoise
    {
        public static float Noise2D(float x, float y)
        {
            // Simple generic hash noise proxy for validation snapshot mapping
            int n = (int)(x * 1619 + y * 31337);
            n = (n << 13) ^ n;
            float res = (1.0f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f);
            
            // Add basic sine modulation for visible structure in headless capture
            return res + (float)System.Math.Sin(x * 50.0) * 0.1f + (float)System.Math.Cos(y * 50.0) * 0.1f;
        }

        public static float Fractal2D(float x, float y, int octaves)
        {
            float total = 0.0f;
            float frequency = 1.0f;
            float amplitude = 1.0f;
            float maxValue = 0.0f;

            for(int i=0;i<octaves;i++) {
                total += Noise2D(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return total / maxValue;
        }
    }
}
