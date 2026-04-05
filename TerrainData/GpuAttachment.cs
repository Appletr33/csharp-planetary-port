using System;
using System.Collections.Generic;
using PlanetaryTerrainRenderer.Math;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace PlanetaryTerrainRenderer.TerrainData
{
    public struct AtlasTileAttachment
    {
        public uint AtlasIndex;
        public AttachmentLabel Label;
    }

    public struct AttachmentMeta
    {
        public uint LodCount;
        public uint TextureSize;
        public uint BorderSize;
        public uint CenterSize;
        public uint PixelsPerEntry;
        public uint EntriesPerSide;
        public uint EntriesPerTile;
    }

    public struct AtlasBufferInfo
    {
        public bool Mask;
        public uint LodCount;
        public uint TextureSize;
        public uint BorderSize;
        public uint CenterSize;
        public AttachmentFormat Format;
        public uint MipLevelCount;
        public uint PixelsPerEntry;
        public uint EntriesPerSide;
        public uint EntriesPerTile;
        public uint ActualSideSize;
        public uint AlignedSideSize;
        public uint ActualTileSize;
        public uint AlignedTileSize;
        public Int3 WorkgroupCount;

        private const uint COPY_BYTES_PER_ROW_ALIGNMENT = 256;

        private static uint AlignByteSize(uint value)
        {
            return value - 1 - (value - 1) % COPY_BYTES_PER_ROW_ALIGNMENT + COPY_BYTES_PER_ROW_ALIGNMENT;
        }

        public AtlasBufferInfo(Attachment attachment, uint lodCount)
        {
            Format = attachment.Format;
            TextureSize = attachment.TextureSize;
            BorderSize = attachment.BorderSize;
            CenterSize = attachment.CenterSize;
            MipLevelCount = attachment.MipLevelCount;
            Mask = attachment.Mask;
            LodCount = lodCount;

            uint pixelSize = Format.PixelSize();
            uint entrySize = sizeof(uint);
            PixelsPerEntry = entrySize / pixelSize;

            ActualSideSize = TextureSize * pixelSize;
            AlignedSideSize = AlignByteSize(ActualSideSize);
            ActualTileSize = TextureSize * ActualSideSize;
            AlignedTileSize = TextureSize * AlignedSideSize;

            EntriesPerSide = AlignedSideSize / entrySize;
            EntriesPerTile = TextureSize * EntriesPerSide;

            WorkgroupCount = new Int3((int)(EntriesPerSide / 8), (int)(TextureSize / 8), 1);
        }

        public uint BufferSize(uint slots)
        {
            return slots * AlignedTileSize;
        }

        public AttachmentMeta AttachmentMeta()
        {
            return new AttachmentMeta
            {
                LodCount = LodCount,
                TextureSize = TextureSize,
                BorderSize = BorderSize,
                CenterSize = CenterSize,
                PixelsPerEntry = PixelsPerEntry,
                EntriesPerSide = EntriesPerSide,
                EntriesPerTile = EntriesPerTile
            };
        }
    }

    public class GpuAttachment
    {
        public int Index { get; set; }
        public AtlasBufferInfo BufferInfo { get; set; }
        public Texture AtlasTexture { get; set; }

        public List<Texture> MipViews { get; set; } = new List<Texture>();
        public List<List<uint>> MipsToGenerate { get; set; } = new List<List<uint>>();

        public GpuAttachment(GraphicsDevice device, AttachmentLabel label, Attachment attachment, TileAtlas tileAtlas)
        {
            BufferInfo = new AtlasBufferInfo(attachment, tileAtlas.LodCount);

            var desc = TextureDescription.New3D(
                (int)BufferInfo.TextureSize,
                (int)BufferInfo.TextureSize,
                100,
                BufferInfo.Format.ProcessingFormat(),
                TextureFlags.ShaderResource | TextureFlags.UnorderedAccess
            );
            desc.MipLevels = (int)attachment.MipLevelCount;

            AtlasTexture = Texture.New(device, desc);

            for(int i = 0; i < BufferInfo.MipLevelCount; i++)
            {
                MipsToGenerate.Add(new List<uint>());
            }
        }
    }
}
