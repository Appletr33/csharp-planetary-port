using System;
using System.Collections.Generic;
using PlanetaryTerrainRenderer.Math;
using Stride.Core.Mathematics;

namespace PlanetaryTerrainRenderer.TerrainData
{
    public struct TileTreeEntry
    {
        public uint AtlasIndex;
        public uint AtlasLod;

        public static TileTreeEntry Default => new TileTreeEntry { AtlasIndex = uint.MaxValue, AtlasLod = uint.MaxValue };
    }

    public enum LoadingStateType
    {
        Loading,
        Loaded
    }

    public struct LoadingState
    {
        public LoadingStateType Type;
        public uint LoadingCount;

        public static LoadingState Loading(uint count) => new LoadingState { Type = LoadingStateType.Loading, LoadingCount = count };
        public static LoadingState Loaded() => new LoadingState { Type = LoadingStateType.Loaded, LoadingCount = 0 };
    }

    public class TileState
    {
        public LoadingState State { get; set; }
        public uint AtlasIndex { get; set; }
        public uint Requests { get; set; }
    }

    public class TileAtlas
    {
        public Dictionary<AttachmentLabel, Attachment> Attachments { get; } = new Dictionary<AttachmentLabel, Attachment>();
        public Dictionary<TileCoordinate, TileState> TileStates { get; } = new Dictionary<TileCoordinate, TileState>();
        public Queue<uint> UnusedIndices { get; } = new Queue<uint>();
        public HashSet<TileCoordinate> ExistingTiles { get; } = new HashSet<TileCoordinate>();

        public List<AttachmentTileWithData> UploadingTiles { get; } = new List<AttachmentTileWithData>();
        public List<AttachmentTile> ToLoad { get; } = new List<AttachmentTile>();

        public uint LodCount { get; set; }
        public float MinHeight { get; set; }
        public float MaxHeight { get; set; }
        public float HeightScale { get; set; } = 1.0f;
        public TerrainShape Shape { get; set; }

        public TileAtlas(IEnumerable<KeyValuePair<AttachmentLabel, AttachmentConfig>> configAttachments, IEnumerable<TileCoordinate> existingTiles, uint lodCount, float minHeight, float maxHeight, TerrainShape shape, uint atlasSize, string path)
        {
            LodCount = lodCount;
            MinHeight = minHeight;
            MaxHeight = maxHeight;
            Shape = shape;

            foreach (var kvp in configAttachments)
            {
                Attachments.Add(kvp.Key, new Attachment(kvp.Value, path));
            }

            foreach (var tile in existingTiles)
            {
                ExistingTiles.Add(tile);
            }

            for (uint i = 0; i < atlasSize; i++)
            {
                UnusedIndices.Enqueue(i);
            }
        }

        public TileTreeEntry GetBestTile(TileCoordinate tileCoordinate)
        {
            var bestTileCoordinate = tileCoordinate;

            if (!ExistingTiles.Contains(tileCoordinate))
            {
                return TileTreeEntry.Default;
            }

            while (true)
            {
                if (bestTileCoordinate.Equals(TileCoordinate.INVALID))
                {
                    return TileTreeEntry.Default;
                }

                if (TileStates.TryGetValue(bestTileCoordinate, out var tile))
                {
                    if (tile.State.Type == LoadingStateType.Loaded)
                    {
                        return new TileTreeEntry
                        {
                            AtlasIndex = tile.AtlasIndex,
                            AtlasLod = bestTileCoordinate.Lod
                        };
                    }
                }

                var parent = bestTileCoordinate.Parent();
                bestTileCoordinate = parent ?? TileCoordinate.INVALID;
            }
        }

        public void TileLoaded(AttachmentTile tile, AttachmentData data)
        {
            if (TileStates.TryGetValue(tile.Coordinate, out var tileState))
            {
                if (tileState.State.Type == LoadingStateType.Loading)
                {
                    if (tileState.State.LoadingCount == 1)
                    {
                        tileState.State = LoadingState.Loaded();
                    }
                    else
                    {
                        tileState.State = LoadingState.Loading(tileState.State.LoadingCount - 1);
                    }
                }
                else if (tileState.State.Type == LoadingStateType.Loaded)
                {
                    throw new InvalidOperationException("Loaded more attachments than registered with the tile atlas.");
                }

                UploadingTiles.Add(new AttachmentTileWithData
                {
                    AtlasIndex = tileState.AtlasIndex,
                    Label = tile.Label,
                    Data = data
                });
            }
            else
            {
                Console.WriteLine("Tile is no longer loaded.");
            }
        }

        public void RequestTile(TileCoordinate tileCoordinate)
        {
            if (!ExistingTiles.Contains(tileCoordinate))
            {
                return;
            }

            if (TileStates.TryGetValue(tileCoordinate, out var tile))
            {
                if (tile.Requests == 0)
                {
                    var list = new List<uint>(UnusedIndices);
                    list.Remove(tile.AtlasIndex);
                    UnusedIndices.Clear();
                    foreach (var i in list) UnusedIndices.Enqueue(i);
                }

                tile.Requests++;
            }
            else
            {
                if (UnusedIndices.Count == 0)
                {
                    throw new InvalidOperationException("Atlas out of indices");
                }
                uint atlasIndex = UnusedIndices.Dequeue();

                var keysToRemove = new List<TileCoordinate>();
                foreach (var kvp in TileStates)
                {
                    if (kvp.Value.AtlasIndex == atlasIndex)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                foreach (var k in keysToRemove) TileStates.Remove(k);

                TileStates.Add(tileCoordinate, new TileState
                {
                    Requests = 1,
                    State = LoadingState.Loading((uint)Attachments.Count),
                    AtlasIndex = atlasIndex
                });

                foreach (var label in Attachments.Keys)
                {
                    ToLoad.Add(new AttachmentTile
                    {
                        Coordinate = tileCoordinate,
                        Label = label
                    });
                }
            }
        }

        public void ReleaseTile(TileCoordinate tileCoordinate)
        {
            if (!ExistingTiles.Contains(tileCoordinate))
            {
                return;
            }

            if (TileStates.TryGetValue(tileCoordinate, out var tile))
            {
                tile.Requests--;

                if (tile.Requests == 0)
                {
                    UnusedIndices.Enqueue(tile.AtlasIndex);
                }
            }
        }
    }
}
