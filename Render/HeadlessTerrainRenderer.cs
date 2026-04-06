using System;
using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Maths;

namespace PlanetaryTerrainRenderer.Render
{
    public unsafe class HeadlessTerrainRenderer : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly PhysicalDevice _physicalDevice;
        private readonly uint _queueFamilyIndex;

        private CommandPool _commandPool;
        private RenderPass _renderPass;
        private PipelineLayout _pipelineLayout;
        private Pipeline _graphicsPipeline;
        private ShaderCompiler _shaderCompiler;

        public HeadlessTerrainRenderer(Vk vk, Device device, PhysicalDevice physicalDevice, uint queueFamilyIndex)
        {
            _vk = vk;
            _device = device;
            _physicalDevice = physicalDevice;
            _queueFamilyIndex = queueFamilyIndex;
            _shaderCompiler = new ShaderCompiler();

            CreateCommandPool();
            CreateRenderPass();
            CreateGraphicsPipeline();
        }

        private void CreateCommandPool()
        {
            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = _queueFamilyIndex,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };

            if (_vk.CreateCommandPool(_device, in poolInfo, null, out _commandPool) != Result.Success)
                throw new Exception("Failed to create command pool.");
        }

        private void CreateRenderPass()
        {
            var colorAttachment = new AttachmentDescription
            {
                Format = Format.B8G8R8A8Srgb,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.TransferSrcOptimal
            };

            var depthAttachment = new AttachmentDescription
            {
                Format = Format.D32Sfloat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            };

            var colorAttachmentRef = new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };

            var depthAttachmentRef = new AttachmentReference
            {
                Attachment = 1,
                Layout = ImageLayout.DepthStencilAttachmentOptimal
            };

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

            if (_vk.CreateRenderPass(_device, in renderPassInfo, null, out _renderPass) != Result.Success)
                throw new Exception("Failed to create render pass.");
        }

        private void CreateGraphicsPipeline()
        {
            var vertShaderCode = _shaderCompiler.CompileHLSL(File.ReadAllText("Shaders/Render/vertex_preprocessed.hlsl"), "vertex.hlsl", Silk.NET.Shaderc.ShaderKind.VertexShader);
            var fragShaderCode = _shaderCompiler.CompileHLSL(File.ReadAllText("Shaders/Render/fragment_preprocessed.hlsl"), "fragment.hlsl", Silk.NET.Shaderc.ShaderKind.FragmentShader);

            var vertShaderModule = CreateShaderModule(vertShaderCode);
            var fragShaderModule = CreateShaderModule(fragShaderCode);

            var vertShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertShaderModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main")
            };

            var fragShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragShaderModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main")
            };

            var shaderStages = stackalloc[] { vertShaderStageInfo, fragShaderStageInfo };

            var bindingDescription = new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = (uint)sizeof(float) * 3, // Just 3 floats for position
                InputRate = VertexInputRate.Vertex
            };

            var attributeDescription = new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 0,
                Format = Format.R32G32B32Sfloat,
                Offset = 0
            };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = 1,
                PVertexAttributeDescriptions = &attributeDescription
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = Vk.False
            };

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = Vk.False,
                RasterizerDiscardEnable = Vk.False,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = Vk.False
            };

            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = Vk.False,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = Vk.True,
                DepthWriteEnable = Vk.True,
                DepthCompareOp = CompareOp.Less,
                DepthBoundsTestEnable = Vk.False,
                StencilTestEnable = Vk.False
            };

            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = Vk.False
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = Vk.False,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };

            var dynamicStates = stackalloc[] { DynamicState.Viewport, DynamicState.Scissor };
            var dynamicStateInfo = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates
            };

            var pushConstantRange = new PushConstantRange
            {
                StageFlags = ShaderStageFlags.VertexBit,
                Offset = 0,
                Size = (uint)sizeof(Matrix4X4<float>)
            };

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 0,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushConstantRange
            };

            if (_vk.CreatePipelineLayout(_device, in pipelineLayoutInfo, null, out _pipelineLayout) != Result.Success)
                throw new Exception("Failed to create pipeline layout.");

            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicStateInfo,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
                BasePipelineHandle = default
            };

            if (_vk.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out _graphicsPipeline) != Result.Success)
                throw new Exception("Failed to create graphics pipeline.");

            _vk.DestroyShaderModule(_device, vertShaderModule, null);
            _vk.DestroyShaderModule(_device, fragShaderModule, null);

            Marshal.FreeHGlobal((nint)vertShaderStageInfo.PName);
            Marshal.FreeHGlobal((nint)fragShaderStageInfo.PName);
        }

        private ShaderModule CreateShaderModule(byte[] code)
        {
            fixed (byte* codePtr = code)
            {
                var createInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)code.Length,
                    PCode = (uint*)codePtr
                };

                if (_vk.CreateShaderModule(_device, in createInfo, null, out var shaderModule) != Result.Success)
                    throw new Exception("Failed to create shader module.");

                return shaderModule;
            }
        }

        public void RenderFrame(TerrainManager terrainManager, uint width, uint height, string outputPath)
        {
            // 1. Create Color and Depth Images
            CreateImage(width, height, Format.B8G8R8A8Srgb, ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit, MemoryPropertyFlags.DeviceLocalBit, out var colorImage, out var colorMemory);
            CreateImageView(colorImage, Format.B8G8R8A8Srgb, ImageAspectFlags.ColorBit, out var colorImageView);

            CreateImage(width, height, Format.D32Sfloat, ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, out var depthImage, out var depthMemory);
            CreateImageView(depthImage, Format.D32Sfloat, ImageAspectFlags.DepthBit, out var depthImageView);

            // 2. Create Framebuffer
            var attachments = stackalloc ImageView[] { colorImageView, depthImageView };
            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = width,
                Height = height,
                Layers = 1
            };

            if (_vk.CreateFramebuffer(_device, in framebufferInfo, null, out var framebuffer) != Result.Success)
                throw new Exception("Failed to create framebuffer.");

            // 3. Allocate and Record Command Buffer
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            if (_vk.AllocateCommandBuffers(_device, in allocInfo, out var cmd) != Result.Success)
                throw new Exception("Failed to allocate command buffer.");

            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            _vk.BeginCommandBuffer(cmd, in beginInfo);

            var clearValues = stackalloc ClearValue[2];
            clearValues[0].Color = new ClearColorValue(0.1f, 0.1f, 0.1f, 1.0f);
            clearValues[1].DepthStencil = new ClearDepthStencilValue(1.0f, 0);

            var renderPassBeginInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _renderPass,
                Framebuffer = framebuffer,
                RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D(width, height)),
                ClearValueCount = 2,
                PClearValues = clearValues
            };

            _vk.CmdBeginRenderPass(cmd, in renderPassBeginInfo, SubpassContents.Inline);
            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _graphicsPipeline);

            var viewport = new Viewport
            {
                X = 0,
                Y = 0,
                Width = width,
                Height = height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };
            _vk.CmdSetViewport(cmd, 0, 1, &viewport);

            var scissor = new Rect2D
            {
                Offset = new Offset2D(0, 0),
                Extent = new Extent2D(width, height)
            };
            _vk.CmdSetScissor(cmd, 0, 1, &scissor);

            // Compute ViewProj Matrix
            var view = Matrix4X4.CreateLookAt(new Vector3D<float>(0, 150, 150), new Vector3D<float>(0, 0, 0), new Vector3D<float>(0, 1, 0));
            var proj = Matrix4X4.CreatePerspectiveFieldOfView((float)System.Math.PI / 4f, (float)width / height, 0.1f, 1000f);
            proj.M22 *= -1; // Vulkan Y-flip
            var viewProj = view * proj;

            _vk.CmdPushConstants(cmd, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)sizeof(Matrix4X4<float>), &viewProj);

            // Bind Buffers and Draw
            var vertexBuffer = terrainManager.DebugVertexBuffer.Handle;
            ulong offset = 0;
            _vk.CmdBindVertexBuffers(cmd, 0, 1, &vertexBuffer, &offset);
            _vk.CmdBindIndexBuffer(cmd, terrainManager.DebugIndexBuffer.Handle, 0, IndexType.Uint32);

            _vk.CmdDrawIndexed(cmd, terrainManager.DebugIndexBuffer.SizeInBytes / sizeof(uint), 1, 0, 0, 0);

            _vk.CmdEndRenderPass(cmd);

            // 4. Copy color image to CPU visible image
            CreateImage(width, height, Format.B8G8R8A8Srgb, ImageUsageFlags.TransferDstBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out var dstImage, out var dstMemory, ImageTiling.Linear);

            // Transition destination image to transfer destination optimal
            TransitionImageLayout(cmd, dstImage, Format.B8G8R8A8Srgb, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);

            // Copy Image
            var copyRegion = new ImageCopy
            {
                SrcSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlags.ColorBit, MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1 },
                SrcOffset = new Offset3D(0, 0, 0),
                DstSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlags.ColorBit, MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1 },
                DstOffset = new Offset3D(0, 0, 0),
                Extent = new Extent3D(width, height, 1)
            };

            _vk.CmdCopyImage(cmd, colorImage, ImageLayout.TransferSrcOptimal, dstImage, ImageLayout.TransferDstOptimal, 1, &copyRegion);

            // Transition destination image to general layout for CPU mapping
            TransitionImageLayout(cmd, dstImage, Format.B8G8R8A8Srgb, ImageLayout.TransferDstOptimal, ImageLayout.General);

            _vk.EndCommandBuffer(cmd);

            // 5. Submit and Wait
            _vk.GetDeviceQueue(_device, _queueFamilyIndex, 0, out var queue);

            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &cmd
            };

            _vk.QueueSubmit(queue, 1, in submitInfo, default);
            _vk.QueueWaitIdle(queue);

            // 6. Map Memory and Save
            var subresource = new ImageSubresource { AspectMask = ImageAspectFlags.ColorBit, MipLevel = 0, ArrayLayer = 0 };
            _vk.GetImageSubresourceLayout(_device, dstImage, in subresource, out var subResourceLayout);

            void* data;
            _vk.MapMemory(_device, dstMemory, 0, Vk.WholeSize, 0, &data);

            nint sourcePtr = (nint)data;
            using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32>((int)width, (int)height))
            {
                image.ProcessPixelRows(accessor =>
                {
                    byte* sourceBytes = (byte*)sourcePtr;
                    for (int y = 0; y < height; y++)
                    {
                        var span = accessor.GetRowSpan(y);
                        var sourceRow = new ReadOnlySpan<SixLabors.ImageSharp.PixelFormats.Bgra32>(sourceBytes + y * (long)subResourceLayout.RowPitch, (int)width);
                        sourceRow.CopyTo(span);
                    }
                });
                SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, outputPath);
            }

            _vk.UnmapMemory(_device, dstMemory);

            // Cleanup Frame Resources
            _vk.DestroyImage(_device, dstImage, null);
            _vk.FreeMemory(_device, dstMemory, null);

            _vk.FreeCommandBuffers(_device, _commandPool, 1, in cmd);
            _vk.DestroyFramebuffer(_device, framebuffer, null);

            _vk.DestroyImageView(_device, colorImageView, null);
            _vk.DestroyImage(_device, colorImage, null);
            _vk.FreeMemory(_device, colorMemory, null);

            _vk.DestroyImageView(_device, depthImageView, null);
            _vk.DestroyImage(_device, depthImage, null);
            _vk.FreeMemory(_device, depthMemory, null);
        }

        private void CreateImage(uint width, uint height, Format format, ImageUsageFlags usage, MemoryPropertyFlags properties, out Image image, out DeviceMemory imageMemory, ImageTiling tiling = ImageTiling.Optimal)
        {
            var imageInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D(width, height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Format = format,
                Tiling = tiling,
                InitialLayout = ImageLayout.Undefined,
                Usage = usage,
                SharingMode = SharingMode.Exclusive,
                Samples = SampleCountFlags.Count1Bit
            };

            if (_vk.CreateImage(_device, in imageInfo, null, out image) != Result.Success)
                throw new Exception("Failed to create image.");

            _vk.GetImageMemoryRequirements(_device, image, out var memReqs);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memReqs.Size,
                MemoryTypeIndex = FindMemoryType(memReqs.MemoryTypeBits, properties)
            };

            if (_vk.AllocateMemory(_device, in allocInfo, null, out imageMemory) != Result.Success)
                throw new Exception("Failed to allocate image memory.");

            _vk.BindImageMemory(_device, image, imageMemory, 0);
        }

        private void CreateImageView(Image image, Format format, ImageAspectFlags aspectFlags, out ImageView imageView)
        {
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = format,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = aspectFlags,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (_vk.CreateImageView(_device, in viewInfo, null, out imageView) != Result.Success)
                throw new Exception("Failed to create image view.");
        }

        private void TransitionImageLayout(CommandBuffer cmd, Image image, Format format, ImageLayout oldLayout, ImageLayout newLayout)
        {
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            PipelineStageFlags sourceStage;
            PipelineStageFlags destinationStage;

            if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = AccessFlags.TransferWriteBit;
                sourceStage = PipelineStageFlags.TopOfPipeBit;
                destinationStage = PipelineStageFlags.TransferBit;
            }
            else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.General)
            {
                barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
                barrier.DstAccessMask = AccessFlags.MemoryReadBit;
                sourceStage = PipelineStageFlags.TransferBit;
                destinationStage = PipelineStageFlags.HostBit; // Or Transfer
            }
            else
            {
                throw new Exception("Unsupported layout transition.");
            }

            _vk.CmdPipelineBarrier(
                cmd,
                sourceStage, destinationStage,
                0,
                0, null,
                0, null,
                1, &barrier
            );
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

        public void Dispose()
        {
            _vk.DestroyPipeline(_device, _graphicsPipeline, null);
            _vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
            _vk.DestroyRenderPass(_device, _renderPass, null);
            _vk.DestroyCommandPool(_device, _commandPool, null);
            _shaderCompiler?.Dispose();
        }
    }
}
