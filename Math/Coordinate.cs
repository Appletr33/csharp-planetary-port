using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace PlanetaryTerrainRenderer.Math
{
    public struct Coordinate
    {
        public uint Face;
        public Vector2 UV; // Using single-precision, equivalent of DVec2 in Rust

        public Coordinate(uint face, Vector2 uv)
        {
            Face = face;
            UV = uv;
        }

        public static Coordinate FromUnitPosition(Vector3 unitPosition, bool isSpherical)
        {
            if (isSpherical)
            {
                uint face = 0;
                float xAbs = System.Math.Abs(unitPosition.X);
                float yAbs = System.Math.Abs(unitPosition.Y);
                float zAbs = System.Math.Abs(unitPosition.Z);

                if (xAbs > yAbs && xAbs > zAbs && unitPosition.X < 0.0f) face = 0;
                else if (xAbs > yAbs && xAbs > zAbs) face = 3;
                else if (zAbs > yAbs && unitPosition.Z > 0.0f) face = 1;
                else if (zAbs > yAbs) face = 4;
                else if (unitPosition.Y > 0.0f) face = 2;
                else face = 5;

                var mat = MathConstants.INVERSE_FACE_MATRICES[face];
                var abc = Vector3.TransformCoordinate(unitPosition, mat);

                var xy = new Vector2(abc.Y, abc.Z) / abc.X;

                float x2 = xy.X * xy.X;
                float y2 = xy.Y * xy.Y;
                var powFactorX = (float)System.Math.Pow((1.0 + MathConstants.SIGMA) / (1.0 + MathConstants.SIGMA * x2), 0.5);
                var powFactorY = (float)System.Math.Pow((1.0 + MathConstants.SIGMA) / (1.0 + MathConstants.SIGMA * y2), 0.5);

                var uv = 0.5f * xy * new Vector2(powFactorX, powFactorY) + new Vector2(0.5f, 0.5f);

                return new Coordinate(face, uv);
            }
            else
            {
                var uvX = System.Math.Clamp(unitPosition.X + 0.5f, 0.0f, 1.0f);
                var uvY = System.Math.Clamp(unitPosition.Z + 0.5f, 0.0f, 1.0f);

                return new Coordinate(0, new Vector2(uvX, uvY));
            }
        }

        public Vector3 UnitPosition(bool isSpherical)
        {
            if (isSpherical)
            {
                var scaledUv = 2.0f * UV - new Vector2(1.0f, 1.0f);
                var u2 = UV.X * (UV.X - 1.0f);
                var v2 = UV.Y * (UV.Y - 1.0f);

                var powFactorU = (float)System.Math.Pow(1.0 - 4.0 * MathConstants.SIGMA * u2, 0.5);
                var powFactorV = (float)System.Math.Pow(1.0 - 4.0 * MathConstants.SIGMA * v2, 0.5);

                var xy = new Vector2(scaledUv.X / powFactorU, scaledUv.Y / powFactorV);

                var vec = new Vector3(1.0f, xy.X, xy.Y);
                vec.Normalize();
                return Vector3.TransformCoordinate(vec, MathConstants.FACE_MATRICES[Face]);
            }
            else
            {
                return new Vector3(UV.X - 0.5f, 0.0f, UV.Y - 0.5f);
            }
        }

        public static Coordinate FromLocalPosition(Vector3 worldPosition, TerrainShape shape)
        {
            var unitPosition = shape.PositionLocalToUnit(worldPosition);
            return FromUnitPosition(unitPosition, shape.IsSpherical);
        }

        public Vector3 LocalPosition(TerrainShape shape, float height)
        {
            var unitPosition = UnitPosition(shape.IsSpherical);
            return shape.PositionUnitToLocal(unitPosition, height);
        }

        public Coordinate ProjectToFace(uint face)
        {
            var rotation = FaceRotationExtensions.GetFaceRotation(Face, face);
            return new Coordinate(face, rotation.ProjectUV(this));
        }
    }

    public struct TileCoordinate : IEquatable<TileCoordinate>
    {
        public uint Face;
        public uint Lod;
        public Int2 XY;

        public static readonly TileCoordinate INVALID = new TileCoordinate(uint.MaxValue, uint.MaxValue, new Int2(int.MaxValue, int.MaxValue));

        public TileCoordinate(uint face, uint lod, Int2 xy)
        {
            Face = face;
            Lod = lod;
            XY = xy;
        }

        public string Path(string basePath)
        {
            var tileBlock = XY / MathConstants.BLOCK_SIZE;
            return System.IO.Path.Combine(basePath, $"{Lod}/{tileBlock.X}_{tileBlock.Y}/{this}.tif");
        }

        public TileCoordinate? Parent()
        {
            if (Lod == 0) return null;
            return new TileCoordinate(Face, Lod - 1, new Int2(XY.X >> 1, XY.Y >> 1));
        }

        public IEnumerable<TileCoordinate> Children()
        {
            for (int index = 0; index < 4; index++)
            {
                yield return new TileCoordinate(Face, Lod + 1, new Int2((XY.X << 1) + index % 2, (XY.Y << 1) + index / 2));
            }
        }

        public IEnumerable<(TileCoordinate, FaceRotation)> Neighbours(bool spherical)
        {
            foreach (var offset in MathConstants.NEIGHBOUR_OFFSETS)
            {
                var edgePosition = XY + offset;
                var tileCount = 1 << (int)Lod;
                var scale = (float)(tileCount - 1);

                if (spherical)
                {
                    var edgeUvX = System.Math.Clamp(edgePosition.X / scale, 0.0f, 1.0f);
                    var edgeUvY = System.Math.Clamp(edgePosition.Y / scale, 0.0f, 1.0f);
                    var edgeCoordinate = new Coordinate(Face, new Vector2(edgeUvX, edgeUvY));

                    uint edgeIndex = 0;
                    if ((edgePosition.X >= tileCount || edgePosition.X < 0) && (edgePosition.Y >= tileCount || edgePosition.Y < 0))
                    {
                        yield return (INVALID, FaceRotation.Identical);
                        continue;
                    }
                    else if (edgePosition.Y < 0) edgeIndex = 1; // up
                    else if (edgePosition.X >= tileCount) edgeIndex = 2; // right
                    else if (edgePosition.Y >= tileCount) edgeIndex = 3; // down
                    else if (edgePosition.X < 0) edgeIndex = 4; // left
                    else edgeIndex = 0;

                    var neighbourFace = MathConstants.NEIGHBOURING_FACES[Face][edgeIndex];
                    var neighbourCoordinate = edgeCoordinate.ProjectToFace(neighbourFace);
                    var neighbourXy = new Int2((int)(neighbourCoordinate.UV.X * scale), (int)(neighbourCoordinate.UV.Y * scale));
                    var rotation = FaceRotationExtensions.GetFaceRotation(Face, neighbourFace);

                    yield return (new TileCoordinate(neighbourFace, Lod, neighbourXy), rotation);
                }
                else
                {
                    if (edgePosition.X < 0 || edgePosition.Y < 0 || edgePosition.X >= tileCount || edgePosition.Y >= tileCount)
                    {
                        yield return (INVALID, FaceRotation.Identical);
                    }
                    else
                    {
                        yield return (new TileCoordinate(Face, Lod, edgePosition), FaceRotation.Identical);
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"{Face}_{Lod}_{XY.X}_{XY.Y}";
        }

        public bool Equals(TileCoordinate other)
        {
            return Face == other.Face && Lod == other.Lod && XY == other.XY;
        }

        public override bool Equals(object? obj)
        {
            return obj is TileCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Face, Lod, XY);
        }
    }

    public struct ViewCoordinate
    {
        public Int2 XY;
        public Vector2 UV;

        public ViewCoordinate(Coordinate coordinate, uint lod)
        {
            float count = (float)System.Math.Pow(2, lod);
            var scaledUv = coordinate.UV * count;

            XY = new Int2((int)scaledUv.X, (int)scaledUv.Y);
            UV = new Vector2(scaledUv.X - (float)System.Math.Floor(scaledUv.X), scaledUv.Y - (float)System.Math.Floor(scaledUv.Y));
        }
    }
}
