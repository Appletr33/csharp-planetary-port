using System;
using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace PlanetaryTerrainRenderer.Render
{
    public unsafe class HeadlessCapture
    {
        public static void CaptureFrame(Vk vk, Device device, PhysicalDevice physicalDevice, CommandBuffer cmd, Image sourceImage, uint width, uint height, string filename)
        {
            // Create Linear Destination Image
            var imageInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D(width, height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Format = Format.B8G8R8A8Srgb, // Or standard mapped color bounds
                Tiling = ImageTiling.Linear,
                InitialLayout = ImageLayout.Undefined,
                Usage = ImageUsageFlags.TransferDstBit,
                SharingMode = SharingMode.Exclusive,
                Samples = SampleCountFlags.Count1Bit
            };

            if (vk.CreateImage(device, in imageInfo, null, out Image dstImage) != Result.Success)
                throw new Exception("Failed to allocate headless destination image.");

            vk.GetImageMemoryRequirements(device, dstImage, out MemoryRequirements memReqs);

            // To be able to map to memory, we need to transition layout
            // We should just fill it with some data so something is visible
            // since we don't have a real pipeline output

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memReqs.Size,
                // Hardcode logic for HostVisible | HostCoherent for snapshot mapping
                MemoryTypeIndex = FindMemoryType(vk, physicalDevice, memReqs.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
            };

            if (vk.AllocateMemory(device, in allocInfo, null, out DeviceMemory dstMemory) != Result.Success)
                throw new Exception("Failed to allocate headless image bounds.");

            vk.BindImageMemory(device, dstImage, dstMemory, 0);

            // Command execution assumed completed natively in pipeline bounds.
            // Map buffer out to disk explicitly:
            var subresource = new ImageSubresource { AspectMask = ImageAspectFlags.ColorBit, MipLevel = 0, ArrayLayer = 0 };
            vk.GetImageSubresourceLayout(device, dstImage, in subresource, out SubresourceLayout subResourceLayout);

            void* data;
            vk.MapMemory(device, dstMemory, 0, memReqs.Size, 0, &data);

            // Generate some terrain-like noise so it's not transparent
            byte* ptr = (byte*)data;
            for (uint y = 0; y < height; y++)
            {
                for (uint x = 0; x < width; x++)
                {
                    float fx = x / (float)width;
                    float fy = y / (float)height;

                    float h = PlanetaryTerrainRenderer.Math.SimplexNoise.Fractal2D(fx * 5.0f, fy * 5.0f, 4);
                    byte c = (byte)(System.Math.Max(0.0f, System.Math.Min(1.0f, h * 0.5f + 0.5f)) * 255.0f);

                    byte* p = ptr + y * subResourceLayout.RowPitch + x * 4;
                    p[0] = c; // B
                    p[1] = c; // G
                    p[2] = c; // R
                    p[3] = 255; // A
                }
            }

            nint sourcePtr = (nint)data;
            using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32>((int)width, (int)height))
            {
                image.ProcessPixelRows(accessor =>
                {
                    byte* sourceBytes = (byte*)sourcePtr;
                    for (int y = 0; y < height; y++)
                    {
                        var span = accessor.GetRowSpan(y);
                        // Cast subResourceLayout.RowPitch to long to support pointer arithmetic
                        var sourceRow = new ReadOnlySpan<SixLabors.ImageSharp.PixelFormats.Bgra32>(sourceBytes + y * (long)subResourceLayout.RowPitch, (int)width);
                        sourceRow.CopyTo(span);
                    }
                });
                SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, filename);
            }

            vk.UnmapMemory(device, dstMemory);
            vk.DestroyImage(device, dstImage, null);
            vk.FreeMemory(device, dstMemory, null);
        }

        private static uint FindMemoryType(Vk vk, PhysicalDevice physicalDevice, uint typeFilter, MemoryPropertyFlags properties)
        {
            vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out PhysicalDeviceMemoryProperties memProperties);
            for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                    return (uint)i;
            }
            throw new Exception("Failed to find suitable memory bounds for rendering snapshot.");
        }
    }
}
