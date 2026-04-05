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

        public SwapchainKHR Swapchain { get; private set; }
        public Format ImageFormat { get; private set; }
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

            var colorAttachmentRef = new AttachmentReference { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef
            };

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &colorAttachment,
                SubpassCount = 1,
                PSubpasses = &subpass
            };

            if (_vk.CreateRenderPass(_device, in renderPassInfo, null, out RenderPass renderPass) != Result.Success)
                throw new Exception("Failed to create RenderPass!");
            RenderPass = renderPass;

            Framebuffers = new Framebuffer[imageCount];
            for (int i = 0; i < imageCount; i++)
            {
                var attachment = ImageViews[i];
                var framebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = RenderPass,
                    AttachmentCount = 1,
                    PAttachments = &attachment,
                    Width = Extent.Width,
                    Height = Extent.Height,
                    Layers = 1
                };

                if (_vk.CreateFramebuffer(_device, in framebufferInfo, null, out Framebuffers[i]) != Result.Success)
                    throw new Exception("Failed to create Framebuffer");
            }
        }

        public void Recreate(Extent2D extent, PresentModeKHR presentMode)
        {
            // Boilerplate destruction and re-allocation loops based on Resize ticks...
        }

        public void Dispose()
        {
            foreach (var fb in Framebuffers) _vk.DestroyFramebuffer(_device, fb, null);
            foreach (var view in ImageViews) _vk.DestroyImageView(_device, view, null);
            _vk.DestroyRenderPass(_device, RenderPass, null);
            _khrSwapchain.DestroySwapchain(_device, Swapchain, null);
            _khrSwapchain.Dispose();
        }
    }
}
