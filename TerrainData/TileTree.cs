using System.Collections.Generic;
using PlanetaryTerrainRenderer.Math;

namespace PlanetaryTerrainRenderer.TerrainData
{
    public struct TileTreeEntry
    {
        public uint AtlasIndex;
        public uint AtlasLod;

        public static TileTreeEntry Default => new TileTreeEntry { AtlasIndex = uint.MaxValue, AtlasLod = uint.MaxValue };
    }

    public class TileTree
    {
        public TileTreeEntry[] Data;
        public List<TileCoordinate> ReleasedTiles = new List<TileCoordinate>();
        public List<TileCoordinate> RequestedTiles = new List<TileCoordinate>();
        
        public uint TreeSize;
        public uint LodCount;
        
        public TileTree(uint treeSize, uint lodCount)
        {
            TreeSize = treeSize;
            LodCount = lodCount;
            Data = new TileTreeEntry[6 * LodCount * TreeSize * TreeSize]; // Assume maximum 6 faces for Spherical bounds
        }

        // Simulates distance-based traversal quadtree requests
        public void ComputeRequests()
        {
            // Note: Placeholder math abstraction until C# port of Coordinate and Face projection is complete. Typically you iterate visible Nodes from Root -> down. When distance > LodDistance, register it as a leaf node.
        }

        public void AdjustToTileAtlas(TileAtlas atlas)
        {
            // For each tracked active node slot, fetch the most resident lod slice
            for (uint face = 0; face < 6; face++) {
                for (uint lod = 0; lod < LodCount; lod++) {
                    for (int x = 0; x < TreeSize; x++) {
                        for (int y = 0; y < TreeSize; y++) {
                            int idx = (int)((face * LodCount * TreeSize * TreeSize) + (lod * TreeSize * TreeSize) + (x * TreeSize) + y);
                            Data[idx] = atlas.GetBestTile(new TileCoordinate { Face = face, Lod = lod, XY = new Int2(x, y) });
                        }
                    }
                }
            }
        }
    }
}
