using System;
using System.Collections.Generic;
using PlanetaryTerrainRenderer.Math;
using PlanetaryTerrainRenderer.TerrainData;

namespace PlanetaryTerrainRenderer
{
    public class TerrainConfig
    {
        public string Path { get; set; } = string.Empty;
        public TerrainShape Shape { get; set; } = TerrainShape.Plane(1.0);
        public uint LodCount { get; set; } = 1;
        public float MinHeight { get; set; } = 0.0f;
        public float MaxHeight { get; set; } = 1.0f;
        public Dictionary<AttachmentLabel, AttachmentConfig> Attachments { get; set; } = new Dictionary<AttachmentLabel, AttachmentConfig>();
        public List<TileCoordinate> Tiles { get; set; } = new List<TileCoordinate>();

        public TerrainConfig AddAttachment(AttachmentLabel label, AttachmentConfig attachment)
        {
            Attachments[label] = attachment;
            return this;
        }

        // Serialization logic (e.g., RON parsing) skipped for brevity in C# port
        // as Stride uses its own Asset compiler pipeline system (YAML-based)
    }

    public class TerrainSettings
    {
        public List<AttachmentLabel> Attachments { get; set; } = new List<AttachmentLabel> { AttachmentLabel.Height };
        public uint AtlasSize { get; set; } = 1028;

        public TerrainSettings() { }

        public TerrainSettings(IEnumerable<string> customAttachments)
        {
            foreach (var name in customAttachments)
            {
                Attachments.Add(AttachmentLabel.Custom(name));
            }
        }
    }
}
