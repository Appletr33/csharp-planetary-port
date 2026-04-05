using PlanetaryTerrainRenderer.Math;
using System.Collections.Generic;
using Silk.NET.Vulkan;
namespace PlanetaryTerrainRenderer.TerrainData
{
    public class AttachmentConfig { 
        public uint TextureSize { get; set; } = 256; 
    }
    
    public class AttachmentLabel {
        private string name;
        public AttachmentLabel(string n) { name = n; }
        public static AttachmentLabel Height => new AttachmentLabel("Height");
        public static AttachmentLabel Albedo => new AttachmentLabel("Albedo");
        public static AttachmentLabel Custom(string name) => new AttachmentLabel(name);
        
        public override bool Equals(object? obj)
        {
            if (obj is AttachmentLabel l) return name == l.name;
            return false;
        }
        public override int GetHashCode() => name.GetHashCode();
    }
}
