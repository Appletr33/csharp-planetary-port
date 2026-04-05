using System;
using Stride.Core.Mathematics;
using PlanetaryTerrainRenderer.Math;
using PlanetaryTerrainRenderer.TerrainData;

namespace PlanetaryTerrainRenderer.Debug
{
    public class ApproximationDebug
    {
        private const float DEBUG_SCALE = 1.0f / (1 << 5);

        public static void DebugSurfaceApproximation(bool enable, object gizmos, TileTree[] tileTrees, object input)
        {
            if (!enable) return;

            foreach (var tileTree in tileTrees)
            {
                var shape = tileTree.Shape;

                for (uint face = 0; face < shape.FaceCount; face++)
                {
                    var approx = tileTree.SurfaceApproximation[face];

                    float height = (float)shape.ScaleScalar * 0.02f;
                    var normal = Vector3.Cross(approx.P_dv, approx.P_du);
                    normal.Normalize();

                    var position = approx.P + height * normal;

                    var viewCoordinate = tileTree.ViewCoordinates[face];
                    var corners = new[] { new Int2(0, 0), new Int2(0, 1), new Int2(1, 1), new Int2(1, 0), new Int2(0, 0) };
                }
            }
        }
    }

    public class DebugTerrain
    {
        public bool Wireframe { get; set; } = false;
        public bool ShowDataLod { get; set; } = false;
        public bool ShowGeometryLod { get; set; } = false;
        public bool ShowTileTree { get; set; } = false;
        public bool ShowPixels { get; set; } = false;
        public bool ShowUv { get; set; } = false;
        public bool ShowNormals { get; set; } = false;
        public bool Morph { get; set; } = true;
        public bool Blend { get; set; } = true;
        public bool TileTreeLod { get; set; } = false;
        public bool Lighting { get; set; } = true;
        public bool SampleGrad { get; set; } = true;
        public bool HighPrecision { get; set; } = true;
        public bool Freeze { get; set; } = false;
        public bool Test1 { get; set; } = false;
        public bool Test2 { get; set; } = false;
        public bool Test3 { get; set; } = false;
    }

    public class Camera
    {
    }

    public class OrbitalCamera
    {
    }

    public class Debug
    {
    }
}
