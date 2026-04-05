using System.Collections.Generic;
using Stride.Core.Serialization.Contents;
using Stride.Graphics;

namespace PlanetaryTerrainRenderer.TerrainData
{
    public class LoadingTile
    {
        public string Url { get; set; } = null!;
        public AttachmentTile Tile { get; set; } = null!;
        public AttachmentFormat Format { get; set; }
    }

    public class DefaultLoader
    {
        public Dictionary<int, LoadingTile> LoadingTiles { get; } = new Dictionary<int, LoadingTile>();
        private int _nextId = 0;
        private readonly int _capacity = 32;

        public AttachmentTile? ToLoadNext(List<AttachmentTile> tiles)
        {
            if (tiles.Count == 0) return null;
            var tile = tiles[tiles.Count - 1];
            tiles.RemoveAt(tiles.Count - 1);
            return tile;
        }

        public void FinishLoading(TileAtlas atlas, ContentManager contentManager)
        {
            var toRemove = new List<int>();
            foreach (var kvp in LoadingTiles)
            {
                var tile = kvp.Value;
                if (contentManager.Exists(tile.Url))
                {
                    var texture = contentManager.Load<Texture>(tile.Url);
                    byte[] dummyData = new byte[1];
                    var data = AttachmentData.FromBytes(dummyData, tile.Format);
                    atlas.TileLoaded(tile.Tile, data);

                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                LoadingTiles.Remove(key);
            }
        }

        public void StartLoading(TileAtlas atlas)
        {
            while (LoadingTiles.Count < _capacity)
            {
                var tile = ToLoadNext(atlas.ToLoad);
                if (tile != null)
                {
                    var attachment = atlas.Attachments[tile.Label];
                    var pathStr = System.IO.Path.Combine(attachment.Path, tile.Label.ToString());
                    var url = tile.Coordinate.Path(pathStr);

                    LoadingTiles.Add(_nextId++, new LoadingTile
                    {
                        Url = url,
                        Tile = tile,
                        Format = attachment.Format
                    });
                }
                else
                {
                    break;
                }
            }
        }
    }
}
