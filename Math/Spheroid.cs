using System;
using Stride.Core.Mathematics;

namespace PlanetaryTerrainRenderer.Math
{
    public static class Spheroid
    {
        public static Vector3 ProjectPointSpheroid(double majorAxis, double minorAxis, Vector3 y)
        {
            var ellipse = new Vector2((float)majorAxis, (float)minorAxis);
            var axis = new Vector2(y.X, y.Z);
            var inputPosition = new Vector2(axis.Length(), y.Y);
            var ellipsePosition = ProjectPointEllipse(ellipse, inputPosition);

            axis.Normalize();
            axis *= ellipsePosition.X;

            return new Vector3(axis.X, ellipsePosition.Y, axis.Y);
        }

        private static Vector2 ProjectPointEllipse(Vector2 ellipse, Vector2 inputPosition)
        {
            var sign = new Vector2(System.Math.Sign(inputPosition.X), System.Math.Sign(inputPosition.Y));
            inputPosition = new Vector2(System.Math.Abs(inputPosition.X), System.Math.Abs(inputPosition.Y));

            Vector2 result;

            if (inputPosition.X == 0.0f)
            {
                result = new Vector2(0.0f, ellipse.Y);
            }
            else if (inputPosition.Y == 0.0f)
            {
                float n = ellipse.X * inputPosition.X;
                float d = ellipse.X * ellipse.X - ellipse.Y * ellipse.Y;

                if (n < d)
                {
                    float f = n / d;
                    result = new Vector2(ellipse.X * f, ellipse.Y * (float)System.Math.Sqrt(1.0f - f * f));
                }
                else
                {
                    result = new Vector2(ellipse.X, 0.0f);
                }
            }
            else
            {
                var z = inputPosition / ellipse;
                float g = z.LengthSquared() - 1.0f;

                if (g != 0.0f)
                {
                    var r = new Vector2((ellipse.X * ellipse.X) / (ellipse.Y * ellipse.Y), 1.0f);
                    float root = FindRoot(r, z, g);
                    result = inputPosition * r / (new Vector2(root) + r);
                }
                else
                {
                    result = inputPosition;
                }
            }

            return new Vector2(sign.X * result.X, sign.Y * result.Y);
        }

        private static float FindRoot(Vector2 r, Vector2 z, float g)
        {
            var n = r * z;

            float s0 = z.Y - 1.0f;
            float s1 = g < 0.0f ? 0.0f : n.Length() - 1.0f;

            while (true)
            {
                float s = (s0 + s1) / 2.0f;
                var denom = new Vector2(s) + r;
                var frac = new Vector2(n.X / denom.X, n.Y / denom.Y);
                float currentG = frac.LengthSquared() - 1.0f;

                if (s == s0 || s == s1)
                {
                    return s;
                }

                if (currentG < 0.0f)
                {
                    s1 = s;
                }
                else if (currentG > 0.0f)
                {
                    s0 = s;
                }
                else
                {
                    return s;
                }
            }
        }
    }
}
