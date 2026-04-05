using System.Collections.Generic;
using PlanetaryTerrainRenderer.Math;

namespace PlanetaryTerrainRenderer.TerrainData
{
    public enum LoadingState { Loading, Loaded }

    public class AtlasTileState
    {
        public LoadingState State;
        public uint AtlasIndex;
        public uint Requests;
    }

    public class AttachmentTileWithData
    {
        public uint AtlasIndex;
        public AttachmentLabel Label;
        public AttachmentData Data;
    }

    public class TileAtlas
    {
        public Queue<AttachmentTile> ToLoad = new Queue<AttachmentTile>();
        public List<AttachmentTileWithData> UploadingTiles = new List<AttachmentTileWithData>();
        
        private Dictionary<TileCoordinate, AtlasTileState> _tileStates = new Dictionary<TileCoordinate, AtlasTileState>();
        private Queue<uint> _unusedIndices = new Queue<uint>();
        
        public TileAtlas(uint atlasSize)
        {
            for (uint i = 0; i < atlasSize; i++)
                _unusedIndices.Enqueue(i);
        }

        public TileTreeEntry GetBestTile(TileCoordinate coordinate)
        {
            // Traverse up the tree until a loaded tile is found
            TileCoordinate current = coordinate;
            while (true)
            {
                if (_tileStates.TryGetValue(current, out var state) && state.State == LoadingState.Loaded)
                {
                    return new TileTreeEntry { AtlasIndex = state.AtlasIndex, AtlasLod = current.Lod };
                }
                
                if (current.Lod == 0) // Reached root, no loaded tile
                    return TileTreeEntry.Default;
                    
                current = new TileCoordinate { Face = current.Face, Lod = current.Lod - 1, XY = new Int2(current.XY.X / 2, current.XY.Y / 2) };
            }
        }

        public void TileLoaded(AttachmentTile tile, AttachmentData data)
        {
            if (_tileStates.TryGetValue(tile.Coordinate, out var state))
            {
                state.State = LoadingState.Loaded;
                UploadingTiles.Add(new AttachmentTileWithData {
                    AtlasIndex = state.AtlasIndex,
                    Label = tile.Label,
                    Data = data
                });
            }
        }

        public void RequestTile(TileCoordinate coordinate)
        {
            if (_tileStates.TryGetValue(coordinate, out var state))
            {
                state.Requests++;
            }
            else
            {
                uint index = _unusedIndices.Dequeue();
                _tileStates[coordinate] = new AtlasTileState { AtlasIndex = index, Requests = 1, State = LoadingState.Loading };
                ToLoad.Enqueue(new AttachmentTile { Coordinate = coordinate, Label = AttachmentLabel.Height });
            }
        }

        public void ReleaseTile(TileCoordinate coordinate)
        {
            if (_tileStates.TryGetValue(coordinate, out var state))
            {
                state.Requests--;
                if (state.Requests == 0)
                {
                    _unusedIndices.Enqueue(state.AtlasIndex);
                    // Leave it in memory until overwritten (LRU strategy caching)
                }
            }
        }
    }
}
