using System;
using System.Collections.Generic;
using PlanetaryTerrainRenderer.Math;
using Stride.Core.Mathematics;

namespace PlanetaryTerrainRenderer.TerrainData
{
    public enum RequestState
    {
        Requested,
        Released
    }

    public struct TileStateTree
    {
        public TileCoordinate Coordinate;
        public RequestState State;

        public static TileStateTree Default => new TileStateTree { Coordinate = TileCoordinate.INVALID, State = RequestState.Released };
    }

    public class TileTree
    {
        public TileTreeEntry[] Data { get; set; }
        public List<TileCoordinate> ReleasedTiles { get; } = new List<TileCoordinate>();
        public List<TileCoordinate> RequestedTiles { get; } = new List<TileCoordinate>();
        private TileStateTree[] _tiles;

        public uint TreeSize { get; }
        public uint LodCount { get; }
        public TerrainShape Shape { get; }
        public uint GeometryTileCount { get; }
        public uint RefinementCount { get; }
        public uint GridSize { get; }
        public float MorphRange { get; }
        public float BlendRange { get; }
        public double MorphDistance { get; }
        public double BlendDistance { get; }
        public double SubdivisionDistance { get; }
        public double LoadDistance { get; }
        public double PrecisionDistance { get; }

        public uint ViewFace { get; private set; }
        public uint ViewLod { get; }
        public Vector3 ViewLocalPosition { get; set; }
        public Vector3 ViewWorldPosition { get; set; }
        public Coordinate[] ViewCoordinates { get; } = new Coordinate[6];
        public Vector4[] HalfSpaces { get; set; } = new Vector4[6];
        public SurfaceApproximation[] SurfaceApproximation { get; set; } = new SurfaceApproximation[6];
        public float ApproximateHeight { get; set; }
        public uint Order { get; }

        public TileTree(uint treeSize, uint lodCount, TerrainShape shape, uint geometryTileCount, uint refinementCount, uint gridSize, double morphDistance, double blendDistance, double loadTolerance, double subdivisionTolerance, float morphRange, float blendRange, double precisionDistance, uint viewLod, uint order)
        {
            TreeSize = treeSize;
            LodCount = lodCount;
            Shape = shape;
            GeometryTileCount = geometryTileCount;
            RefinementCount = refinementCount;
            GridSize = gridSize;
            MorphDistance = morphDistance * shape.FaceSize;
            BlendDistance = blendDistance * shape.FaceSize;
            LoadDistance = BlendDistance * (1.0 + loadTolerance);
            SubdivisionDistance = MorphDistance * (1.0 + subdivisionTolerance);
            MorphRange = morphRange;
            BlendRange = blendRange;
            PrecisionDistance = precisionDistance * shape.ScaleScalar;
            ViewLod = viewLod;
            Order = order;

            int faceCount = (int)shape.FaceCount;
            int totalSize = faceCount * (int)lodCount * (int)treeSize * (int)treeSize;
            Data = new TileTreeEntry[totalSize];
            _tiles = new TileStateTree[totalSize];

            for (int i = 0; i < totalSize; i++)
            {
                Data[i] = TileTreeEntry.Default;
                _tiles[i] = TileStateTree.Default;
            }
        }

        private int GetIndex(uint face, uint lod, uint x, uint y)
        {
            return (int)((((face * LodCount) + lod) * TreeSize + x) * TreeSize + y);
        }

        private static Vector2 ComputeTreeXY(Coordinate coordinate, double tileCount)
        {
            var uv = coordinate.UV;
            var treeX = System.Math.Min(uv.X * tileCount, tileCount - 0.000001);
            var treeY = System.Math.Min(uv.Y * tileCount, tileCount - 0.000001);
            return new Vector2((float)treeX, (float)treeY);
        }

        private Int2 ComputeOrigin(Coordinate viewCoordinate, uint lod)
        {
            double tileCount = System.Math.Pow(2, lod);
            var treeXy = ComputeTreeXY(viewCoordinate, tileCount);

            var originX = (int)System.Math.Round(System.Math.Clamp(treeXy.X - 0.5 * TreeSize, 0.0, tileCount - TreeSize));
            var originY = (int)System.Math.Round(System.Math.Clamp(treeXy.Y - 0.5 * TreeSize, 0.0, tileCount - TreeSize));

            return new Int2(originX, originY);
        }

        private double ComputeTileDistance(TileCoordinate tile, Coordinate viewCoordinate)
        {
            double tileCount = System.Math.Pow(2, tile.Lod);
            var viewTileXy = ComputeTreeXY(viewCoordinate, tileCount);
            var tileOffset = new Int2((int)viewTileXy.X, (int)viewTileXy.Y) - tile.XY;

            var offset = new Vector2(viewTileXy.X - (float)System.Math.Floor(viewTileXy.X), viewTileXy.Y - (float)System.Math.Floor(viewTileXy.Y));

            if (tileOffset.X < 0) offset.X = 0.0f;
            else if (tileOffset.X > 0) offset.X = 1.0f;

            if (tileOffset.Y < 0) offset.Y = 0.0f;
            else if (tileOffset.Y > 0) offset.Y = 1.0f;

            var tileLocalPosition = new Coordinate(tile.Face, new Vector2((tile.XY.X + offset.X) / (float)tileCount, (tile.XY.Y + offset.Y) / (float)tileCount))
                .LocalPosition(Shape, ApproximateHeight);

            return Vector3.Distance(tileLocalPosition, ViewLocalPosition);
        }

        public void Update()
        {
            var viewCoordinate = Coordinate.FromLocalPosition(ViewLocalPosition, Shape);
            ViewFace = viewCoordinate.Face;

            for (uint face = 0; face < Shape.FaceCount; face++)
            {
                var viewCoordFace = viewCoordinate.ProjectToFace(face);
                ViewCoordinates[face] = viewCoordFace;

                for (uint lod = 0; lod < LodCount; lod++)
                {
                    var origin = ComputeOrigin(viewCoordFace, lod);

                    for (uint x = 0; x < TreeSize; x++)
                    {
                        for (uint y = 0; y < TreeSize; y++)
                        {
                            var tileCoordinate = new TileCoordinate(face, lod, origin + new Int2((int)x, (int)y));

                            var tileDistance = ComputeTileDistance(tileCoordinate, viewCoordFace);
                            var loadDistance = LoadDistance / System.Math.Pow(2, lod);

                            var state = (lod == 0 || tileDistance < loadDistance) ? RequestState.Requested : RequestState.Released;

                            int index = GetIndex(face, lod, (uint)tileCoordinate.XY.X % TreeSize, (uint)tileCoordinate.XY.Y % TreeSize);
                            var tile = _tiles[index];

                            if (!tileCoordinate.Equals(tile.Coordinate))
                            {
                                if (tile.State == RequestState.Requested)
                                {
                                    tile.State = RequestState.Released;
                                    ReleasedTiles.Add(tile.Coordinate);
                                }
                                tile.Coordinate = tileCoordinate;
                            }

                            if (tile.State == RequestState.Released && state == RequestState.Requested)
                            {
                                tile.State = RequestState.Requested;
                                RequestedTiles.Add(tile.Coordinate);
                            }
                            else if (tile.State == RequestState.Requested && state == RequestState.Released)
                            {
                                tile.State = RequestState.Released;
                                ReleasedTiles.Add(tile.Coordinate);
                            }

                            _tiles[index] = tile;
                        }
                    }
                }
            }
        }

        public void AdjustToTileAtlas(TileAtlas tileAtlas)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                Data[i] = tileAtlas.GetBestTile(_tiles[i].Coordinate);
            }
        }

        public void GenerateSurfaceApproximation()
        {
            for (int i = 0; i < 6; i++)
            {
                if (i < Shape.FaceCount)
                {
                    SurfaceApproximation[i] = PlanetaryTerrainRenderer.Math.SurfaceApproximation.Compute(ViewCoordinates[i], ViewLocalPosition, ViewWorldPosition, Shape);
                }
            }
        }
    }
}
