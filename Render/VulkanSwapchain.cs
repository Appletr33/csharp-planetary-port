using System;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace PlanetaryTerrainRenderer.Render
{
    public unsafe class VulkanSwapchain : IDisposable
    {
        private Vk _vk;
        private Device _device;
        private KhrSwapchain _khrSwapchain;
        private SurfaceKHR _surface;
        private PhysicalDevice _physicalDevice;
        private Image _depthImage;
        private DeviceMemory _depthImageMemory;
        private ImageView _depthImageView;

        public SwapchainKHR Swapchain { get; private set; }
        public Format ImageFormat { get; private set; }
        public Format DepthFormat { get; private set; } = Format.D32Sfloat;
        public Extent2D Extent { get; private set; }
        public Image[] Images { get; private set; }
        public ImageView[] ImageViews { get; private set; }
        public Framebuffer[] Framebuffers { get; private set; }
        public RenderPass RenderPass { get; private set; }

        public VulkanSwapchain(Vk vk, Instance instance, Device device, PhysicalDevice physicalDevice, SurfaceKHR surface, Extent2D extent, PresentModeKHR presentMode)
        {
            _vk = vk;
            _device = device;
            _surface = surface;
            _physicalDevice = physicalDevice;

            if (!vk.TryGetDeviceExtension(instance, device, out _khrSwapchain))
                throw new Exception("VK_KHR_swapchain extension not found.");

            // Hardcode standard 32-bit SRGB for this implementation fallback loop
            ImageFormat = Format.B8G8R8A8Srgb;
            Extent = extent;

            var createInfo = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = _surface,
                MinImageCount = 3,
                ImageFormat = ImageFormat,
                ImageColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr,
                ImageExtent = Extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = SharingMode.Exclusive,
                PreTransform = SurfaceTransformFlagsKHR.IdentityBitKhr,
                CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
                PresentMode = presentMode,
                Clipped = true,
                OldSwapchain = default
            };

            if (_khrSwapchain.CreateSwapchain(_device, in createInfo, null, out SwapchainKHR swapchain) != Result.Success)
                throw new Exception("Failed to create Swapchain!");
            
            Swapchain = swapchain;

            uint imageCount = 0;
            _khrSwapchain.GetSwapchainImages(_device, Swapchain, ref imageCount, null);
            Images = new Image[imageCount];
            fixed (Image* ptr = Images)
                _khrSwapchain.GetSwapchainImages(_device, Swapchain, ref imageCount, ptr);

            ImageViews = new ImageView[imageCount];
            for (int i = 0; i < imageCount; i++)
            {
                var viewInfo = new ImageViewCreateInfo
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = Images[i],
                    ViewType = ImageViewType.Type2D,
                    Format = ImageFormat,
                    Components = new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity),
                    SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
                };

                if (_vk.CreateImageView(_device, in viewInfo, null, out ImageViews[i]) != Result.Success)
                    throw new Exception($"Failed to create Swapchain Image View {i}");
            }

            // Create Depth Image
            CreateDepthResources();

            // Create RenderPass mapping Opaque execution to this layout bounds
            var colorAttachment = new AttachmentDescription
            {
                Format = ImageFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr
            };

            var depthAttachment = new AttachmentDescription
            {
                Format = DepthFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            };

            var colorAttachmentRef = new AttachmentReference { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };
            var depthAttachmentRef = new AttachmentReference { Attachment = 1, Layout = ImageLayout.DepthStencilAttachmentOptimal };

            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef,
                PDepthStencilAttachment = &depthAttachmentRef
            };

            var dependency = new SubpassDependency
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
            };

            var attachments = stackalloc AttachmentDescription[] { colorAttachment, depthAttachment };

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 2,
                PAttachments = attachments,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            if (_vk.CreateRenderPass(_device, in renderPassInfo, null, out RenderPass renderPass) != Result.Success)
                throw new Exception("Failed to create RenderPass!");
            RenderPass = renderPass;

            Framebuffers = new Framebuffer[imageCount];
            for (int i = 0; i < imageCount; i++)
            {
                var framebufferAttachments = stackalloc ImageView[] { ImageViews[i], _depthImageView };
                var framebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = RenderPass,
                    AttachmentCount = 2,
                    PAttachments = framebufferAttachments,
                    Width = Extent.Width,
                    Height = Extent.Height,
                    Layers = 1
                };

                if (_vk.CreateFramebuffer(_device, in framebufferInfo, null, out Framebuffers[i]) != Result.Success)
                    throw new Exception("Failed to create Framebuffer");
            }
        }

        private void CreateDepthResources()
        {
            var imageInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D(Extent.Width, Extent.Height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Format = DepthFormat,
                Tiling = ImageTiling.Optimal,
                InitialLayout = ImageLayout.Undefined,
                Usage = ImageUsageFlags.DepthStencilAttachmentBit,
                SharingMode = SharingMode.Exclusive,
                Samples = SampleCountFlags.Count1Bit
            };

            if (_vk.CreateImage(_device, in imageInfo, null, out _depthImage) != Result.Success)
                throw new Exception("Failed to create depth image!");

            _vk.GetImageMemoryRequirements(_device, _depthImage, out var memReqs);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memReqs.Size,
                MemoryTypeIndex = FindMemoryType(memReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
            };

            if (_vk.AllocateMemory(_device, in allocInfo, null, out _depthImageMemory) != Result.Success)
                throw new Exception("Failed to allocate depth image memory!");

            _vk.BindImageMemory(_device, _depthImage, _depthImageMemory, 0);

            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _depthImage,
                ViewType = ImageViewType.Type2D,
                Format = DepthFormat,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.DepthBit, 0, 1, 0, 1)
            };

            if (_vk.CreateImageView(_device, in viewInfo, null, out _depthImageView) != Result.Success)
                throw new Exception("Failed to create depth image view!");
        }

        private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
        {
            _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);
            for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                    return (uint)i;
            }
            throw new Exception("Failed to find suitable memory type.");
        }

        public void Recreate(Extent2D extent, PresentModeKHR presentMode)
        {
            // Boilerplate destruction and re-allocation loops based on Resize ticks...
        }

        public void Dispose()
        {
            _vk.DestroyImageView(_device, _depthImageView, null);
            _vk.DestroyImage(_device, _depthImage, null);
            _vk.FreeMemory(_device, _depthImageMemory, null);

            foreach (var fb in Framebuffers) _vk.DestroyFramebuffer(_device, fb, null);
            foreach (var view in ImageViews) _vk.DestroyImageView(_device, view, null);
            _vk.DestroyRenderPass(_device, RenderPass, null);
            _khrSwapchain.DestroySwapchain(_device, Swapchain, null);
            _khrSwapchain.Dispose();
        }
    }
}
