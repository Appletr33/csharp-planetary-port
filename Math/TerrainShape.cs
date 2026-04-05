using System;
using Stride.Core.Mathematics;

namespace PlanetaryTerrainRenderer.Math
{
    public enum TerrainShapeType
    {
        Plane,
        Sphere,
        Spheroid
    }

    public struct TerrainShape
    {
        public TerrainShapeType Type { get; set; }
        public double Param1 { get; set; } // side_length for Plane, radius for Sphere, major_axis for Spheroid
        public double Param2 { get; set; } // minor_axis for Spheroid

        public static TerrainShape Plane(double sideLength) => new TerrainShape { Type = TerrainShapeType.Plane, Param1 = sideLength };
        public static TerrainShape Sphere(double radius) => new TerrainShape { Type = TerrainShapeType.Sphere, Param1 = radius };
        public static TerrainShape Spheroid(double majorAxis, double minorAxis) => new TerrainShape { Type = TerrainShapeType.Spheroid, Param1 = majorAxis, Param2 = minorAxis };

        public static readonly TerrainShape WGS84 = Spheroid(6378137.0, 6356752.314245);

        public double FaceSize => 2.0 * System.Math.PI / 4.0 * ScaleScalar;

        public double ScaleScalar => Type switch
        {
            TerrainShapeType.Plane => Param1 / 2.0,
            TerrainShapeType.Sphere => Param1,
            TerrainShapeType.Spheroid => Param1,
            _ => throw new NotImplementedException()
        };

        public Vector3 Scale => Type switch
        {
            TerrainShapeType.Plane => new Vector3((float)Param1, 1.0f, (float)Param1),
            TerrainShapeType.Sphere => new Vector3((float)Param1, (float)Param1, (float)Param1),
            TerrainShapeType.Spheroid => new Vector3((float)Param1, (float)Param2, (float)Param1),
            _ => throw new NotImplementedException()
        };

        public bool IsSpherical => Type != TerrainShapeType.Plane;

        public uint FaceCount => IsSpherical ? 6u : 1u;

        public Vector3 PositionUnitToLocal(Vector3 unitPosition, double height)
        {
            var scale = Scale;
            var localPosition = scale * unitPosition;
            var normalVec = IsSpherical ? unitPosition : Vector3.UnitY;
            var localNormal = (scale * normalVec);
            localNormal.Normalize();

            return localPosition + (float)height * localNormal;
        }

        public Vector3 PositionLocalToUnit(Vector3 localPosition)
        {
            switch (Type)
            {
                case TerrainShapeType.Plane:
                    var scalePlane = Scale;
                    return new Vector3(1.0f, 0.0f, 1.0f) * (localPosition / scalePlane);
                case TerrainShapeType.Sphere:
                    var scaleSphere = Scale;
                    var normSphere = localPosition / scaleSphere;
                    normSphere.Normalize();
                    return normSphere;
                case TerrainShapeType.Spheroid:
                    var scaleSpheroid = Scale;
                    var surfacePosition = PlanetaryTerrainRenderer.Math.Spheroid.ProjectPointSpheroid(Param1, Param2, localPosition);
                    var normSpheroid = surfacePosition / scaleSpheroid;
                    normSpheroid.Normalize();
                    return normSpheroid;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
