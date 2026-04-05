using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PlanetaryTerrainRenderer.Math;
using PlanetaryTerrainRenderer.TerrainData;

namespace PlanetaryTerrainRenderer
{
    public class TerrainComponents<C>
    {
        public Dictionary<int, C> Components { get; } = new Dictionary<int, C>();
    }

    public class TerrainConfig
    {
        public string Path { get; set; } = string.Empty;
        public TerrainShape Shape { get; set; } = TerrainShape.Plane;
        public uint LodCount { get; set; } = 1;
        public float MinHeight { get; set; } = 0.0f;
        public float MaxHeight { get; set; } = 1.0f;
        public Dictionary<string, AttachmentConfig> Attachments { get; set; } = new Dictionary<string, AttachmentConfig>();
        public List<TileCoordinate> Tiles { get; set; } = new List<TileCoordinate>();

        public TerrainConfig AddAttachment(string label, AttachmentConfig attachment)
        {
            Attachments[label] = attachment;
            return this;
        }

        public static TerrainConfig LoadFile(string path)
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TerrainConfig>(json) ?? new TerrainConfig();
        }

        public void SaveFile(string path)
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
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
