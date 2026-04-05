using System;
using Stride.Core.Mathematics;

namespace PlanetaryTerrainRenderer.Math
{
    public struct SurfaceApproximation
    {
        public Vector3 P;
        public Vector3 P_du;
        public Vector3 P_dv;
        public Vector3 P_duu;
        public Vector3 P_duv;
        public Vector3 P_dvv;

        public static SurfaceApproximation Compute(Coordinate viewCoordinate, Vector3 viewLocalPosition, Vector3 viewWorldPosition, TerrainShape shape)
        {
            if (shape.IsSpherical)
            {
                var u = viewCoordinate.UV.X;
                var v = viewCoordinate.UV.Y;
                int face = (int)viewCoordinate.Face;

                double xDenom = System.Math.Sqrt(1.0 - 4.0 * MathConstants.SIGMA * u * (u - 1.0));
                double x = (2.0 * u - 1.0) / xDenom;
                double x_du = 2.0 * (MathConstants.SIGMA + 1.0) / System.Math.Pow(xDenom, 3);
                double x_duu = 12.0 * MathConstants.SIGMA * (MathConstants.SIGMA + 1.0) * (2.0 * u - 1.0) / System.Math.Pow(xDenom, 5);

                double yDenom = System.Math.Sqrt(1.0 - 4.0 * MathConstants.SIGMA * v * (v - 1.0));
                double y = (2.0 * v - 1.0) / yDenom;
                double y_dv = 2.0 * (MathConstants.SIGMA + 1.0) / System.Math.Pow(yDenom, 3);
                double y_dvv = 12.0 * MathConstants.SIGMA * (MathConstants.SIGMA + 1.0) * (2.0 * v - 1.0) / System.Math.Pow(yDenom, 5);

                double l = System.Math.Sqrt(1.0 + x * x + y * y);
                double l_du = x * x_du / l;
                double l_dv = y * y_dv / l;
                double l_duu = (x * x_duu * l * l + (y * y + 1.0) * x_du * x_du) / System.Math.Pow(l, 3);
                double l_duv = -(x * y * x_du * y_dv) / System.Math.Pow(l, 3);
                double l_dvv = (y * y_dvv * l * l + (x * x + 1.0) * y_dv * y_dv) / System.Math.Pow(l, 3);

                double a = 1.0;
                double a_du = -l_du;
                double a_dv = -l_dv;
                double a_duu = 2.0 * l_du * l_du - l * l_duu;
                double a_duv = 2.0 * l_du * l_dv - l * l_duv;
                double a_dvv = 2.0 * l_dv * l_dv - l * l_dvv;

                double b = x;
                double b_du = -x * l_du + x_du * l;
                double b_dv = -x * l_dv;
                double b_duu = 2.0 * x * l_du * l_du - l * (2.0 * x_du * l_du + x * l_duu) + x_duu * l * l;
                double b_duv = 2.0 * x * l_du * l_dv - l * (x_du * l_dv + x * l_duv);
                double b_dvv = 2.0 * x * l_dv * l_dv - l * x * l_dvv;

                double c = y;
                double c_du = -y * l_du;
                double c_dv = -y * l_dv + y_dv * l;
                double c_duu = 2.0 * y * l_du * l_du - l * y * l_duu;
                double c_duv = 2.0 * y * l_du * l_dv - l * (y_dv * l_du + y * l_duv);
                double c_dvv = 2.0 * y * l_dv * l_dv - l * (2.0 * y_dv * l_dv + y * l_dvv) + y_dvv * l * l;

                var scaleMat = Matrix.Scaling(shape.Scale);
                var m = scaleMat * MathConstants.FACE_MATRICES[face];

                var vecABC = new Vector3((float)a, (float)b, (float)c);
                var vecABC_du = new Vector3((float)a_du, (float)b_du, (float)c_du);
                var vecABC_dv = new Vector3((float)a_dv, (float)b_dv, (float)c_dv);
                var vecABC_duu = new Vector3((float)a_duu, (float)b_duu, (float)c_duu);
                var vecABC_duv = new Vector3((float)a_duv, (float)b_duv, (float)c_duv);
                var vecABC_dvv = new Vector3((float)a_dvv, (float)b_dvv, (float)c_dvv);

                var p = Vector3.TransformCoordinate(vecABC, m) / (float)l;
                var p_du = Vector3.TransformCoordinate(vecABC_du, m) / (float)System.Math.Pow(l, 2);
                var p_dv = Vector3.TransformCoordinate(vecABC_dv, m) / (float)System.Math.Pow(l, 2);
                var p_duu = Vector3.TransformCoordinate(vecABC_duu, m) / (float)System.Math.Pow(l, 3);
                var p_duv = Vector3.TransformCoordinate(vecABC_duv, m) / (float)System.Math.Pow(l, 3);
                var p_dvv = Vector3.TransformCoordinate(vecABC_dvv, m) / (float)System.Math.Pow(l, 3);

                return new SurfaceApproximation
                {
                    P = (p - viewLocalPosition) + viewWorldPosition,
                    P_du = p_du,
                    P_dv = p_dv,
                    P_duu = 0.5f * p_duu,
                    P_duv = p_duv,
                    P_dvv = 0.5f * p_dvv
                };
            }
            else
            {
                return new SurfaceApproximation
                {
                    P = (viewCoordinate.LocalPosition(shape, 0.0f) - viewLocalPosition) + viewWorldPosition,
                    P_du = Vector3.UnitX * (float)shape.ScaleScalar * 2.0f,
                    P_dv = Vector3.UnitZ * (float)shape.ScaleScalar * 2.0f,
                    P_duu = Vector3.Zero,
                    P_duv = Vector3.Zero,
                    P_dvv = Vector3.Zero
                };
            }
        }
    }
}
