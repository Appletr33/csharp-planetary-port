using System;
using System.IO;
using PlanetaryTerrainRenderer.Math;
using Stride.Graphics;

namespace PlanetaryTerrainRenderer.TerrainData
{
    public abstract class AttachmentLabel : IEquatable<AttachmentLabel>
    {
        public sealed class HeightLabel : AttachmentLabel { }
        public sealed class CustomLabel : AttachmentLabel
        {
            public string Name { get; }
            public CustomLabel(string name) { Name = name; }
        }
        public sealed class EmptyLabel : AttachmentLabel
        {
            public int Index { get; }
            public EmptyLabel(int index) { Index = index; }
        }

        public static AttachmentLabel Height => new HeightLabel();
        public static AttachmentLabel Custom(string name) => new CustomLabel(name);
        public static AttachmentLabel Empty(int index) => new EmptyLabel(index);

        public override string ToString()
        {
            return this switch
            {
                HeightLabel => "height",
                CustomLabel custom => custom.Name,
                EmptyLabel empty => $"empty_{(char)('a' + empty.Index)}",
                _ => throw new NotImplementedException()
            };
        }

        public static AttachmentLabel Parse(string s)
        {
            var trimmed = s.Trim();
            if (trimmed == "height") return Height;
            return Custom(trimmed);
        }

        public override bool Equals(object? obj) => Equals(obj as AttachmentLabel);

        public bool Equals(AttachmentLabel? other)
        {
            if (other is null) return false;
            return ToString() == other.ToString();
        }

        public override int GetHashCode() => ToString().GetHashCode();
    }

    public enum AttachmentFormat
    {
        Rgb8U,
        Rgba8U,
        R16U,
        R16I,
        Rg16U,
        R32F
    }

    public static class AttachmentFormatExtensions
    {
        public static AttachmentFormat Parse(string s)
        {
            return s.Trim() switch
            {
                "rg8u" => AttachmentFormat.Rgb8U,
                "rgba8u" => AttachmentFormat.Rgba8U,
                "r16u" => AttachmentFormat.R16U,
                "r16i" => AttachmentFormat.R16I,
                "r32f" => AttachmentFormat.R32F,
                _ => throw new FormatException()
            };
        }

        public static PixelFormat RenderFormat(this AttachmentFormat format)
        {
            return format switch
            {
                AttachmentFormat.Rgb8U => PixelFormat.R8G8B8A8_UNorm_SRgb,
                AttachmentFormat.Rgba8U => PixelFormat.R8G8B8A8_UNorm_SRgb,
                AttachmentFormat.R16U => PixelFormat.R16_UNorm,
                AttachmentFormat.R16I => PixelFormat.R16_SNorm,
                AttachmentFormat.Rg16U => PixelFormat.R16G16_UNorm,
                AttachmentFormat.R32F => PixelFormat.R32_Float,
                _ => throw new NotImplementedException()
            };
        }

        public static PixelFormat ProcessingFormat(this AttachmentFormat format)
        {
            return format switch
            {
                AttachmentFormat.Rgb8U => PixelFormat.R8G8B8A8_UNorm,
                AttachmentFormat.Rgba8U => PixelFormat.R8G8B8A8_UNorm,
                AttachmentFormat.R16U => PixelFormat.R16_UInt,
                AttachmentFormat.R16I => PixelFormat.R16_SInt,
                AttachmentFormat.Rg16U => PixelFormat.R16G16_UInt,
                _ => format.RenderFormat()
            };
        }

        public static uint PixelSize(this AttachmentFormat format)
        {
            return format switch
            {
                AttachmentFormat.Rgb8U => 4,
                AttachmentFormat.Rgba8U => 4,
                AttachmentFormat.R16U => 2,
                AttachmentFormat.R16I => 2,
                AttachmentFormat.Rg16U => 4,
                AttachmentFormat.R32F => 4,
                _ => throw new NotImplementedException()
            };
        }
    }

    public class AttachmentConfig
    {
        public uint TextureSize { get; set; } = 512;
        public uint BorderSize { get; set; } = 2;
        public uint MipLevelCount { get; set; } = 2;
        public bool Mask { get; set; } = false;
        public AttachmentFormat Format { get; set; } = AttachmentFormat.Rgba8U;

        public uint CenterSize => TextureSize - 2 * BorderSize;
        public uint OffsetSize => TextureSize - BorderSize;
    }

    public abstract class AttachmentData
    {
        public sealed class Rgba8UData : AttachmentData { public byte[] Data { get; } public Rgba8UData(byte[] data) { Data = data; } }
        public sealed class R16UData : AttachmentData { public ushort[] Data { get; } public R16UData(ushort[] data) { Data = data; } }
        public sealed class R16IData : AttachmentData { public short[] Data { get; } public R16IData(short[] data) { Data = data; } }
        public sealed class Rg16UData : AttachmentData { public ushort[] Data { get; } public Rg16UData(ushort[] data) { Data = data; } }
        public sealed class R32FData : AttachmentData { public float[] Data { get; } public R32FData(float[] data) { Data = data; } }

        public static AttachmentData FromBytes(byte[] data, AttachmentFormat format)
        {
            switch (format)
            {
                case AttachmentFormat.Rgb8U:
                    var rgbBytes = new byte[(data.Length / 3) * 4];
                    for (int i = 0, j = 0; i < data.Length; i += 3, j += 4)
                    {
                        rgbBytes[j] = data[i];
                        rgbBytes[j + 1] = data[i + 1];
                        rgbBytes[j + 2] = data[i + 2];
                        rgbBytes[j + 3] = 255;
                    }
                    return new Rgba8UData(rgbBytes);
                case AttachmentFormat.Rgba8U:
                    return new Rgba8UData(data);
                case AttachmentFormat.R16U:
                    var r16u = new ushort[data.Length / 2];
                    System.Buffer.BlockCopy(data, 0, r16u, 0, data.Length);
                    return new R16UData(r16u);
                case AttachmentFormat.R16I:
                    var r16i = new short[data.Length / 2];
                    System.Buffer.BlockCopy(data, 0, r16i, 0, data.Length);
                    return new R16IData(r16i);
                case AttachmentFormat.Rg16U:
                    var rg16u = new ushort[data.Length / 2];
                    System.Buffer.BlockCopy(data, 0, rg16u, 0, data.Length);
                    return new Rg16UData(rg16u);
                case AttachmentFormat.R32F:
                    var r32f = new float[data.Length / 4];
                    System.Buffer.BlockCopy(data, 0, r32f, 0, data.Length);
                    return new R32FData(r32f);
                default:
                    throw new NotImplementedException();
            }
        }

        public byte[] Bytes()
        {
            switch (this)
            {
                case Rgba8UData rgba8U: return rgba8U.Data;
                case R16UData r16U:
                    var b1 = new byte[r16U.Data.Length * 2];
                    System.Buffer.BlockCopy(r16U.Data, 0, b1, 0, b1.Length);
                    return b1;
                case R16IData r16I:
                    var b2 = new byte[r16I.Data.Length * 2];
                    System.Buffer.BlockCopy(r16I.Data, 0, b2, 0, b2.Length);
                    return b2;
                case Rg16UData rg16U:
                    var b3 = new byte[rg16U.Data.Length * 2];
                    System.Buffer.BlockCopy(rg16U.Data, 0, b3, 0, b3.Length);
                    return b3;
                case R32FData r32F:
                    var b4 = new byte[r32F.Data.Length * 4];
                    System.Buffer.BlockCopy(r32F.Data, 0, b4, 0, b4.Length);
                    return b4;
                default:
                    throw new NotImplementedException();
            }
        }
    }

    public class AttachmentTile
    {
        public TileCoordinate Coordinate { get; set; }
        public AttachmentLabel Label { get; set; } = AttachmentLabel.Height;
    }

    public class AttachmentTileWithData
    {
        public uint AtlasIndex { get; set; }
        public AttachmentLabel Label { get; set; } = AttachmentLabel.Height;
        public AttachmentData Data { get; set; } = null!;
    }

    public class Attachment
    {
        public string Path { get; }
        public uint TextureSize { get; }
        public uint CenterSize { get; }
        public uint BorderSize { get; }
        public uint MipLevelCount { get; }
        public AttachmentFormat Format { get; }
        public bool Mask { get; }

        public Attachment(AttachmentConfig config, string path)
        {
            Path = path.StartsWith("assets") ? path.Substring(7) : path;
            TextureSize = config.TextureSize;
            CenterSize = config.CenterSize;
            BorderSize = config.BorderSize;
            MipLevelCount = config.MipLevelCount;
            Format = config.Format;
            Mask = config.Mask;
        }
    }
}
