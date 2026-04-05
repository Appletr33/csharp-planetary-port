using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;
using PlanetaryTerrainRenderer.TerrainData;
using PlanetaryTerrainRenderer.Math;

namespace PlanetaryTerrainRenderer.Preprocess
{
    public class SplitData
    {
        public AtlasTileAttachment Tile;
        public Vector2 TopLeft;
        public Vector2 BottomRight;
        public uint TileIndex;
    }

    public class StitchData
    {
        public AtlasTileAttachment Tile;
        public AtlasTileAttachment[] NeighbourTiles = new AtlasTileAttachment[8];
        public uint TileIndex;
    }

    public class DownsampleData
    {
        public AtlasTileAttachment Tile;
        public AtlasTileAttachment[] ChildTiles = new AtlasTileAttachment[4];
        public uint TileIndex;
    }

    public enum PreprocessTaskType
    {
        Split,
        Stitch,
        Downsample,
        Save,
        Barrier
    }

    public class PreprocessTask
    {
        public AtlasTileAttachment Tile;
        public PreprocessTaskType TaskType;

        // Split specific
        public Texture TileData;
        public Vector2 TopLeft;
        public Vector2 BottomRight;

        // Stitch specific
        public AtlasTileAttachment[] NeighbourTiles;

        // Downsample specific
        public AtlasTileAttachment[] ChildTiles;

        public static PreprocessTask Barrier() => new PreprocessTask { TaskType = PreprocessTaskType.Barrier };
    }

    public class ProcessingTask
    {
        public PreprocessTask Task;
        // Stub for bind group mapping
    }

    public class GpuPreprocessor
    {
        public Queue<PreprocessTask> ReadyTasks { get; } = new Queue<PreprocessTask>();
        public List<ProcessingTask> ProcessingTasks { get; } = new List<ProcessingTask>();

        public GpuPreprocessor() { }
    }

    public class PreprocessDataset
    {
        public uint AttachmentIndex { get; set; } = 0;
        public string Path { get; set; } = "";
        public uint Face { get; set; } = 0;
        public Vector2 TopLeft { get; set; } = Vector2.Zero;
        public Vector2 BottomRight { get; set; } = Vector2.One;

        public IEnumerable<TileCoordinate> OverlappingTiles(uint lod)
        {
            float tileCount = (float)System.Math.Pow(2, lod);
            var lower = new Int2((int)(TopLeft.X * tileCount), (int)(TopLeft.Y * tileCount));
            var upper = new Int2((int)System.Math.Ceiling(BottomRight.X * tileCount), (int)System.Math.Ceiling(BottomRight.Y * tileCount));

            for (int x = lower.X; x < upper.X; x++)
            {
                for (int y = lower.Y; y < upper.Y; y++)
                {
                    yield return new TileCoordinate(Face, lod, new Int2(x, y));
                }
            }
        }
    }

    public class SphericalDataset
    {
        public uint AttachmentIndex { get; set; }
        public string[] Paths { get; set; } = new string[6];
        public uint LodMin { get; set; }
        public uint LodMax { get; set; }
    }

    public class Mipmap
    {
        // Corresponds to the mipmapping operations handled inside gpu_preprocessor / gpu_tile_atlas
    }

    public class Preprocessor
    {
        public Queue<PreprocessTask> TaskQueue { get; } = new Queue<PreprocessTask>();
        public List<PreprocessTask> ReadyTasks { get; } = new List<PreprocessTask>();

        public void PreprocessSpherical(SphericalDataset dataset, TileAtlas tileAtlas)
        {
            for (uint face = 0; face < 6; face++)
            {
                var faceDataset = new PreprocessDataset
                {
                    AttachmentIndex = dataset.AttachmentIndex,
                    Path = dataset.Paths[face],
                    Face = face
                };

                // SplitAndDownsample equivalent
                var lods = new List<uint>();
                for (uint i = dataset.LodMin; i <= dataset.LodMax; i++) lods.Add(i);
                lods.Reverse();

                if (lods.Count > 0)
                {
                    foreach (var tile in faceDataset.OverlappingTiles(lods[0]))
                    {
                        TaskQueue.Enqueue(new PreprocessTask { Tile = new AtlasTileAttachment { Label = AttachmentLabel.Height }, TaskType = PreprocessTaskType.Split, TopLeft = faceDataset.TopLeft, BottomRight = faceDataset.BottomRight });
                    }

                    foreach (var lod in lods)
                    {
                        TaskQueue.Enqueue(PreprocessTask.Barrier());
                        foreach (var tile in faceDataset.OverlappingTiles(lod))
                        {
                            TaskQueue.Enqueue(new PreprocessTask { Tile = new AtlasTileAttachment { Label = AttachmentLabel.Height }, TaskType = PreprocessTaskType.Downsample });
                        }
                    }
                }
            }

            TaskQueue.Enqueue(PreprocessTask.Barrier());

            for (uint lod = dataset.LodMin; lod <= dataset.LodMax; lod++)
            {
                for (uint face = 0; face < 6; face++)
                {
                    var faceDataset = new PreprocessDataset
                    {
                        AttachmentIndex = dataset.AttachmentIndex,
                        Path = dataset.Paths[face],
                        Face = face
                    };

                    foreach (var tile in faceDataset.OverlappingTiles(lod))
                    {
                        TaskQueue.Enqueue(new PreprocessTask { Tile = new AtlasTileAttachment { Label = AttachmentLabel.Height }, TaskType = PreprocessTaskType.Stitch });
                    }
                    TaskQueue.Enqueue(PreprocessTask.Barrier());
                    foreach (var tile in faceDataset.OverlappingTiles(lod))
                    {
                        TaskQueue.Enqueue(new PreprocessTask { Tile = new AtlasTileAttachment { Label = AttachmentLabel.Height }, TaskType = PreprocessTaskType.Save });
                    }
                }
            }
        }
    }
}
