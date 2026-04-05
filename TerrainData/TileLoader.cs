using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlanetaryTerrainRenderer.Math;

namespace PlanetaryTerrainRenderer.TerrainData
{
    public class AttachmentTile
    {
        public TileCoordinate Coordinate;
        public AttachmentLabel Label;
    }

    public class AttachmentData
    {
        public byte[] Bytes;
    }

    public class TileLoader
    {
        private ConcurrentDictionary<TileCoordinate, Task<AttachmentData>> _loadingTasks;
        
        public TileLoader()
        {
            _loadingTasks = new ConcurrentDictionary<TileCoordinate, Task<AttachmentData>>();
        }

        public void StartLoading(TileAtlas atlas)
        {
            while (atlas.ToLoad.Count > 0)
            {
                var tile = atlas.ToLoad.Dequeue();
                
                // Fire off async I/O
                var task = Task.Run(() =>
                {
                    // Simulated loading for now, wait 50ms (in a real app this would load from disk/network using TIFF formats)
                    System.Threading.Thread.Sleep(50);
                    return new AttachmentData { Bytes = new byte[1024] /* Stub bytes */ };
                });
                
                _loadingTasks[tile.Coordinate] = task;
            }
        }

        public void FinishLoading(TileAtlas atlas)
        {
            List<TileCoordinate> completed = new List<TileCoordinate>();

            foreach (var kvp in _loadingTasks)
            {
                if (kvp.Value.IsCompleted)
                {
                    completed.Add(kvp.Key);
                    
                    var attachmentTile = new AttachmentTile { Coordinate = kvp.Key, Label = AttachmentLabel.Height };
                    if (kvp.Value.IsCompletedSuccessfully)
                    {
                        atlas.TileLoaded(attachmentTile, kvp.Value.Result);
                    }
                }
            }

            foreach (var c in completed)
            {
                _loadingTasks.TryRemove(c, out _);
            }
        }
    }
}
