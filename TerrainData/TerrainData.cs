using PlanetaryTerrainRenderer.Math;
using System.Collections.Generic;
using Silk.NET.OpenGL;
namespace PlanetaryTerrainRenderer.TerrainData
{
    public class AttachmentConfig { public uint TextureSize {get;set;} }
    public class AttachmentLabel {
        public static AttachmentLabel Height => new AttachmentLabel();
        public static AttachmentLabel Custom(string name) => new AttachmentLabel();
    }

    public class GpuTileAtlas {
        public uint AtlasTexture { get; private set; }
        public GpuTileAtlas(GL gl, TerrainSettings settings, AttachmentConfig config) {}
        public void Dispose() {}
    }

    public class TileAtlas {
        public TileAtlas(uint size) {}
    }

    public class TileTree {
        public TileTree(TerrainShape shape) {}
        public void ComputeRequests() {}
    }
}
