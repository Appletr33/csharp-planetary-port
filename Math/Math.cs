using System;
using Silk.NET.Maths;

namespace PlanetaryTerrainRenderer.Math
{
    public struct Vector2
    {
        public float X, Y;
        public Vector2(float x, float y) { X = x; Y = y; }
        public static Vector2 operator *(float a, Vector2 b) => new Vector2(a * b.X, a * b.Y);
    }
    public struct Vector3
    {
        public float X, Y, Z;
        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }
    public struct Int2 { public int X, Y; public Int2(int x, int y) { X=x; Y=y; } }
    public struct TileCoordinate { public uint Face, Lod; public Int2 XY; }
    public enum TerrainShape { Plane, Sphere }
}
