using System;
using System.IO;

namespace PlanetaryTerrainRenderer.Formats
{
    public class TiffImage
    {
        public uint Width { get; set; }
        public uint Height { get; set; }
        public byte[] Data { get; set; }
    }

    public static class TiffLoader
    {
        public static TiffImage Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"TIFF file at {path} could not be found.");
            }

            // Placeholder: In a complete implementation, use an external library like BitMiracle.LibTiff.NET or ImageSharp
            // to parse the full extent of the TIFF specification bytes.
            // Returning a dummy 256x256 image array for now to allow pipeline progression.

            Console.WriteLine($"[TiffLoader] Stubbing load for: {path}");
            
            return new TiffImage 
            {
                Width = 256,
                Height = 256,
                Data = new byte[256 * 256 * 4] // Assuming Rgba8
            };
        }
    }
}
