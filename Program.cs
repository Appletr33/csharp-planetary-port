using System;
using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.Windowing;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Shaderc;
using Silk.NET.Maths;
using Silk.NET.Input;
using PlanetaryTerrainRenderer.Debug;
using PlanetaryTerrainRenderer.Render;
using PlanetaryTerrainRenderer.Math;
using PlanetaryTerrainRenderer.TerrainData;

namespace PlanetaryTerrainRenderer
{
    public unsafe class Program
    {
        private static IWindow window = null!;
        private static Vk vk = null!;
        private static Instance instance;
        private static Device device;
        private static PhysicalDevice physicalDevice;
        private static SurfaceKHR surface;
        private static VulkanSwapchain swapchain = null!;
        private static ShaderCompiler shaderCompiler = null!;
        private static HeadlessTerrainRenderer headlessRenderer = null!;
        private static ExtDebugUtils debugUtils = null!;
        private static DebugUtilsMessengerEXT debugMessenger;
        private static IInputContext input = null!;
        private static CameraController camera = null!;
        private static TerrainManager terrainManager = null!;

        private static CommandPool commandPool;
        private static CommandBuffer[] commandBuffers = null!;
        private static Silk.NET.Vulkan.Semaphore[] imageAvailableSemaphores = null!;
        private static Silk.NET.Vulkan.Semaphore[] renderFinishedSemaphores = null!;
        private static Fence[] inFlightFences = null!;
        private static PipelineLayout pipelineLayout;
        private static Pipeline graphicsPipeline;
        private static uint currentFrame = 0;
        private const int MAX_FRAMES_IN_FLIGHT = 2;

        public static bool Headless { get; private set; } = false;
        private static bool isMouseLocked = true;

        private static System.Numerics.Vector2 lastMousePos;

        private static uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
        {
            string message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage) ?? "Unknown Validation Message";
            System.Console.WriteLine($"[Vulkan {messageSeverity}] {message}");
            File.AppendAllText("vulkan_log.txt", $"[{DateTime.Now:HH:mm:ss}] [Vulkan {messageSeverity}] {message}\n");
            return Vk.False;
        }

        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--headless")
                Headless = true;

            var options = WindowOptions.DefaultVulkan;
            options.Size = new Vector2D<int>(1280, 720);
            options.Title = "Planetary Terrain Renderer (Vulkan native HLSL)";

            if (Headless)
                options.IsVisible = false;

            window = Window.Create(options);

            window.Load += OnLoad;
            window.Render += OnRender;
            window.Update += OnUpdate;
            window.Resize += OnResize;
            window.Closing += OnClose;

            if (Headless)
            {
                window.Initialize();
                OnLoad();
                
                // Ensure terrain extraction happens before the headless render
                terrainManager.Update();
                terrainManager.ExtractAndPrepare();
                
                OnRender(0.0);
                OnClose();
            }
            else
            {
                window.Run();
            }
        }

        private static void OnLoad()
        {
            if (File.Exists("vulkan_log.txt")) File.Delete("vulkan_log.txt");
            File.WriteAllText("vulkan_log.txt", $"=== Planetary Terrain Renderer Vulkan Execution Log ({DateTime.Now}) ===\n");

            vk = Vk.GetApi();

            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Planetary Terrain Renderer"),
                ApplicationVersion = new Silk.NET.Core.Version32(1, 0, 0),
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
                EngineVersion = new Silk.NET.Core.Version32(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            // Query required windowing extensions
            byte** windowExtensions = window.VkSurface!.GetRequiredExtensions(out uint extCount);
            
            var customExts = new string[extCount + 1];
            for (int i = 0; i < extCount; i++) {
                customExts[i] = Marshal.PtrToStringAnsi((nint)windowExtensions[i])!;
            }
            customExts[extCount] = ExtDebugUtils.ExtensionName;
            var extPtr = (byte**)Silk.NET.Core.Native.SilkMarshal.StringArrayToPtr(customExts);

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = extCount + 1,
                PpEnabledExtensionNames = extPtr
            };

            var layers = new[] { "VK_LAYER_KHRONOS_validation" };
            var layersPtr = (byte**)Silk.NET.Core.Native.SilkMarshal.StringArrayToPtr(layers);

            // Query available layers first
            uint availableLayerCount = 0;
            vk.EnumerateInstanceLayerProperties(ref availableLayerCount, null);
            var availableLayers = new LayerProperties[availableLayerCount];
            bool hasValidation = false;

            fixed (LayerProperties* availableLayersPtr = availableLayers)
            {
                vk.EnumerateInstanceLayerProperties(ref availableLayerCount, availableLayersPtr);
                for (int i = 0; i < availableLayerCount; i++)
                {
                    string layerName = Marshal.PtrToStringAnsi((nint)availableLayersPtr[i].LayerName)!;
                    if (layerName == "VK_LAYER_KHRONOS_validation")
                    {
                        hasValidation = true;
                        break;
                    }
                }
            }

            if (hasValidation)
            {
                createInfo.EnabledLayerCount = 1;
                createInfo.PpEnabledLayerNames = layersPtr;
            }
            else
            {
                createInfo.EnabledLayerCount = 0;
                createInfo.PpEnabledLayerNames = null;
                Console.WriteLine("Warning: VK_LAYER_KHRONOS_validation is not available. Proceeding without validation layers.");
            }

            if (vk.CreateInstance(in createInfo, null, out instance) != Result.Success)
                throw new Exception("Failed to create Vulkan instance");

            Silk.NET.Core.Native.SilkMarshal.Free((nint)layersPtr);
            Silk.NET.Core.Native.SilkMarshal.Free((nint)extPtr);

            if (vk.TryGetInstanceExtension(instance, out debugUtils))
            {
                var msgCreateInfo = new DebugUtilsMessengerCreateInfoEXT
                {
                    SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                    MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
                    MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
                    PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(DebugCallback)
                };

                debugUtils.CreateDebugUtilsMessenger(instance, in msgCreateInfo, null, out debugMessenger);
            }

            // Extract window surface securely
            surface = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();

            camera = new CameraController();

            if (!Headless)
            {
                input = window.CreateInput();
                for (int i = 0; i < input.Keyboards.Count; i++)
                    input.Keyboards[i].KeyDown += KeyDown;

                for (int i = 0; i < input.Mice.Count; i++)
                {
                    input.Mice[i].MouseMove += OnMouseMove;
                    input.Mice[i].MouseDown += OnMouseDown;
                    input.Mice[i].Scroll += OnMouseScroll;
                    input.Mice[i].Cursor.CursorMode = CursorMode.Disabled;
                }
            }

            uint deviceCount = 0;
            vk.EnumeratePhysicalDevices(instance, ref deviceCount, null);
            var devices = new PhysicalDevice[deviceCount];
            fixed (PhysicalDevice* devicesPtr = devices) {
                vk.EnumeratePhysicalDevices(instance, ref deviceCount, devicesPtr);
            }
            physicalDevice = devices[0];

            float queuePriority = 1.0f;
            var queueCreateInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = 0,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            // Inject the VK_KHR_swapchain extension internally to the device
            var deviceExtensions = new[] { KhrSwapchain.ExtensionName };
            var deviceExtPtr = (byte**)Silk.NET.Core.Native.SilkMarshal.StringArrayToPtr(deviceExtensions);

            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = 1,
                PpEnabledExtensionNames = deviceExtPtr
            };

            if (vk.CreateDevice(physicalDevice, in deviceCreateInfo, null, out device) != Result.Success)
                throw new Exception("Failed to create Vulkan logical device");

            Silk.NET.Core.Native.SilkMarshal.Free((nint)deviceExtPtr);

            // Establish Swapchain bounds with Triple Buffering Mailbox
            var extent = new Extent2D((uint)window.Size.X, (uint)window.Size.Y);
            if (!Headless)
            {
                swapchain = new VulkanSwapchain(vk, instance, device, physicalDevice, surface, extent, PresentModeKHR.MailboxKhr);
            }

            // Establish Compiler Runtime
            shaderCompiler = new ShaderCompiler();

            // Setup Base Config
            terrainManager = new TerrainManager(vk, device, physicalDevice);

            if (Headless)
            {
                headlessRenderer = new HeadlessTerrainRenderer(vk, device, physicalDevice, 0);
            }

            var config = new TerrainConfig { Shape = TerrainShape.Plane, LodCount = 6 };
            var settings = new TerrainSettings { AtlasSize = 64 };
            var tree = new TileTree(8, 6);
            var atlas = new TileAtlas(64);
            var terrainInstance = new TerrainInstance(config, settings, tree, atlas);

            // Example of loading HLSL shaders into Spirv natively utilizing the compiler runtime:
            // byte[] vsBytes = shaderCompiler.CompileHLSL(File.ReadAllText("Shaders/Render/vertex.hlsl"), "vertex.hlsl", ShaderKind.VertexShader);
            // byte[] fsBytes = shaderCompiler.CompileHLSL(File.ReadAllText("Shaders/Render/fragment.hlsl"), "fragment.hlsl", ShaderKind.FragmentShader);
            // -> Pass bytecode to vkCreateShaderModule...

            terrainManager.AddTerrain(terrainInstance);

            if (!Headless)
            {
                CreateSyncObjects();
                CreateCommandPool();
                CreateCommandBuffers();
                CreateGraphicsPipeline();
            }

            if (Headless)
                Console.WriteLine("Running in headless mode. Initialized Vulkan successfully!");
        }

        private static void CreateSyncObjects()
        {
            var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
            var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };

            imageAvailableSemaphores = new Silk.NET.Vulkan.Semaphore[MAX_FRAMES_IN_FLIGHT];
            renderFinishedSemaphores = new Silk.NET.Vulkan.Semaphore[MAX_FRAMES_IN_FLIGHT];
            inFlightFences = new Fence[MAX_FRAMES_IN_FLIGHT];

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                vk.CreateSemaphore(device, in semaphoreInfo, null, out imageAvailableSemaphores[i]);
                vk.CreateSemaphore(device, in semaphoreInfo, null, out renderFinishedSemaphores[i]);
                vk.CreateFence(device, in fenceInfo, null, out inFlightFences[i]);
            }
        }

        private static void CreateCommandPool()
        {
            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = 0,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };

            if (vk.CreateCommandPool(device, in poolInfo, null, out commandPool) != Result.Success)
                throw new Exception("Failed to create command pool.");
        }

        private static void CreateCommandBuffers()
        {
            commandBuffers = new CommandBuffer[swapchain.Framebuffers.Length];

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)commandBuffers.Length
            };

            fixed (CommandBuffer* ptr = commandBuffers)
            {
                if (vk.AllocateCommandBuffers(device, in allocInfo, ptr) != Result.Success)
                    throw new Exception("Failed to allocate command buffers.");
            }
        }

        private static void CreateGraphicsPipeline()
        {
            var vertShaderCode = shaderCompiler.CompileHLSL(File.ReadAllText("Shaders/Render/vertex_preprocessed.hlsl"), "vertex.hlsl", ShaderKind.VertexShader);
            var fragShaderCode = shaderCompiler.CompileHLSL(File.ReadAllText("Shaders/Render/fragment_preprocessed.hlsl"), "fragment.hlsl", ShaderKind.FragmentShader);

            var vertModule = CreateShaderModule(vertShaderCode);
            var fragModule = CreateShaderModule(fragShaderCode);

            var vertStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main")
            };

            var fragStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main")
            };

            var stages = stackalloc[] { vertStage, fragStage };

            var bindingDescription = new VertexInputBindingDescription { Binding = 0, Stride = (uint)sizeof(float) * 3, InputRate = VertexInputRate.Vertex };
            var attributeDescription = new VertexInputAttributeDescription { Binding = 0, Location = 0, Format = Format.R32G32B32Sfloat, Offset = 0 };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = 1,
                PVertexAttributeDescriptions = &attributeDescription
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
            var viewportState = new PipelineViewportStateCreateInfo { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, ScissorCount = 1 };
            var rasterizer = new PipelineRasterizationStateCreateInfo { SType = StructureType.PipelineRasterizationStateCreateInfo, PolygonMode = PolygonMode.Fill, LineWidth = 1.0f, CullMode = CullModeFlags.None, FrontFace = FrontFace.CounterClockwise };
            var multisampling = new PipelineMultisampleStateCreateInfo { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
            var depthStencil = new PipelineDepthStencilStateCreateInfo { SType = StructureType.PipelineDepthStencilStateCreateInfo, DepthTestEnable = Vk.True, DepthWriteEnable = Vk.True, DepthCompareOp = CompareOp.Less };
            var colorBlendAttachment = new PipelineColorBlendAttachmentState { ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit, BlendEnable = Vk.False };
            var colorBlending = new PipelineColorBlendStateCreateInfo { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &colorBlendAttachment };
            var dynamicStates = stackalloc[] { DynamicState.Viewport, DynamicState.Scissor };
            var dynamicStateInfo = new PipelineDynamicStateCreateInfo { SType = StructureType.PipelineDynamicStateCreateInfo, DynamicStateCount = 2, PDynamicStates = dynamicStates };

            var pushConstantRange = new PushConstantRange { StageFlags = ShaderStageFlags.VertexBit, Offset = 0, Size = (uint)sizeof(Matrix4X4<float>) };
            var pipelineLayoutInfo = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 0, PushConstantRangeCount = 1, PPushConstantRanges = &pushConstantRange };

            vk.CreatePipelineLayout(device, in pipelineLayoutInfo, null, out pipelineLayout);

            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicStateInfo,
                Layout = pipelineLayout,
                RenderPass = swapchain.RenderPass,
                Subpass = 0
            };

            vk.CreateGraphicsPipelines(device, default, 1, in pipelineInfo, null, out graphicsPipeline);

            vk.DestroyShaderModule(device, vertModule, null);
            vk.DestroyShaderModule(device, fragModule, null);
            Marshal.FreeHGlobal((nint)vertStage.PName);
            Marshal.FreeHGlobal((nint)fragStage.PName);
        }

        private static ShaderModule CreateShaderModule(byte[] code)
        {
            fixed (byte* ptr = code)
            {
                var createInfo = new ShaderModuleCreateInfo { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)code.Length, PCode = (uint*)ptr };
                vk.CreateShaderModule(device, in createInfo, null, out var module);
                return module;
            }
        }

        private static void KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Escape)
            {
                if (isMouseLocked)
                {
                    isMouseLocked = false;
                    foreach (var mouse in input.Mice)
                        mouse.Cursor.CursorMode = CursorMode.Normal;
                }
                else
                {
                    window.Close();
                }
            }
        }

        private static void OnMouseDown(IMouse mouse, MouseButton button)
        {
            if (!isMouseLocked)
            {
                isMouseLocked = true;
                foreach (var m in input.Mice)
                    m.Cursor.CursorMode = CursorMode.Disabled;
            }
        }

        private static void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
        {
            if (scrollWheel.Y > 0)
                camera.MovementSpeed *= 1.2f;
            else if (scrollWheel.Y < 0)
                camera.MovementSpeed /= 1.2f;

            // Clamp speed to a reasonable range
            camera.MovementSpeed = System.Math.Clamp(camera.MovementSpeed, 0.1f, 1000000.0f);
            Console.WriteLine($"Camera Speed: {camera.MovementSpeed:F2}");
        }

        private static void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
        {
            if (!isMouseLocked) return;

            if (lastMousePos == default)
            {
                lastMousePos = position;
                return;
            }

            float xoffset = position.X - lastMousePos.X;
            float yoffset = lastMousePos.Y - position.Y;
            lastMousePos = position;

            camera.ProcessMouseMovement(xoffset, yoffset);
        }

        private static void OnRender(double deltaTime)
        {
            if (Headless) 
            {
                // Explicit RTT Execution Mapping
                Console.WriteLine("Executing Headless RTT extraction...");
                headlessRenderer.RenderFrame(terrainManager, 1280, 720, "planet_render.png");
                Console.WriteLine("Headless Capture Complete! Validation tracking successful.");
                return;
            }

            vk.WaitForFences(device, 1, in inFlightFences[currentFrame], Vk.True, ulong.MaxValue);

            uint imageIndex = 0;
            var khrSwapchain = new KhrSwapchain(vk.Context);
            khrSwapchain.AcquireNextImage(device, swapchain.Swapchain, ulong.MaxValue, imageAvailableSemaphores[currentFrame], default, ref imageIndex);

            vk.ResetFences(device, 1, in inFlightFences[currentFrame]);

            var cmd = commandBuffers[imageIndex];
            vk.ResetCommandBuffer(cmd, 0);

            var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
            vk.BeginCommandBuffer(cmd, in beginInfo);

            var clearValues = stackalloc ClearValue[2];
            clearValues[0].Color = new ClearColorValue(0.1f, 0.1f, 0.1f, 1.0f);
            clearValues[1].DepthStencil = new ClearDepthStencilValue(1.0f, 0);

            var renderPassBeginInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = swapchain.RenderPass,
                Framebuffer = swapchain.Framebuffers[imageIndex],
                RenderArea = new Rect2D(new Offset2D(0, 0), swapchain.Extent),
                ClearValueCount = 2,
                PClearValues = clearValues
            };

            vk.CmdBeginRenderPass(cmd, in renderPassBeginInfo, SubpassContents.Inline);
            vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, graphicsPipeline);

            var viewport = new Viewport { X = 0, Y = 0, Width = swapchain.Extent.Width, Height = swapchain.Extent.Height, MinDepth = 0, MaxDepth = 1 };
            vk.CmdSetViewport(cmd, 0, 1, &viewport);

            var scissor = new Rect2D { Offset = new Offset2D(0, 0), Extent = swapchain.Extent };
            vk.CmdSetScissor(cmd, 0, 1, &scissor);

            var view = Matrix4X4.CreateLookAt(new Vector3D<float>(camera.Position.X, camera.Position.Y, camera.Position.Z), 
                                              new Vector3D<float>(camera.Position.X + camera.Forward.X, camera.Position.Y + camera.Forward.Y, camera.Position.Z + camera.Forward.Z), 
                                              new Vector3D<float>(0, 1, 0));
            var proj = Matrix4X4.CreatePerspectiveFieldOfView((float)System.Math.PI / 4f, (float)window.Size.X / window.Size.Y, 0.1f, 10000000f);
            proj.M22 *= -1;
            var viewProj = view * proj;

            vk.CmdPushConstants(cmd, pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)sizeof(Matrix4X4<float>), &viewProj);

            if (terrainManager.DebugVertexBuffer != null)
            {
                var vertexBuffer = terrainManager.DebugVertexBuffer.Handle;
                ulong offset = 0;
                vk.CmdBindVertexBuffers(cmd, 0, 1, &vertexBuffer, &offset);
                vk.CmdBindIndexBuffer(cmd, terrainManager.DebugIndexBuffer.Handle, 0, IndexType.Uint32);
                vk.CmdDrawIndexed(cmd, terrainManager.DebugIndexBuffer.SizeInBytes / sizeof(uint), 1, 0, 0, 0);
            }

            vk.CmdEndRenderPass(cmd);
            vk.EndCommandBuffer(cmd);

            var waitSemaphores = stackalloc[] { imageAvailableSemaphores[currentFrame] };
            var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
            var signalSemaphores = stackalloc[] { renderFinishedSemaphores[currentFrame] };

            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphores,
                PWaitDstStageMask = waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = &cmd,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphores
            };

            vk.GetDeviceQueue(device, 0, 0, out var queue);
            vk.QueueSubmit(queue, 1, in submitInfo, inFlightFences[currentFrame]);

            var swapchains = stackalloc[] { swapchain.Swapchain };
            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,
                SwapchainCount = 1,
                PSwapchains = swapchains,
                PImageIndices = &imageIndex
            };

            khrSwapchain.QueuePresent(queue, in presentInfo);

            currentFrame = (currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
        }

        private static void OnUpdate(double deltaTime)
        {
            if (input != null)
            {
                for (int i = 0; i < input.Keyboards.Count; i++)
                {
                    camera.ProcessKeyboard(input.Keyboards[i], deltaTime);
                }
            }

            terrainManager.Update();
            terrainManager.ExtractAndPrepare();
            
            CommandBuffer computeCmd = default;
            terrainManager.QueueComputePass(computeCmd);
        }

        private static void OnResize(Vector2D<int> size)
        {
            if (swapchain != null) {
                vk.DeviceWaitIdle(device);
                
                vk.FreeCommandBuffers(device, commandPool, (uint)commandBuffers.Length, commandBuffers[0]);
                vk.DestroyPipeline(device, graphicsPipeline, null);
                vk.DestroyPipelineLayout(device, pipelineLayout, null);
                
                swapchain.Dispose();
                swapchain = new VulkanSwapchain(vk, instance, device, physicalDevice, surface, new Extent2D((uint)size.X, (uint)size.Y), PresentModeKHR.MailboxKhr);
                
                CreateCommandBuffers();
                CreateGraphicsPipeline();
            }
        }

        private static void OnClose()
        {
            vk.DeviceWaitIdle(device);
            
            if (!Headless)
            {
                for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
                {
                    vk.DestroySemaphore(device, imageAvailableSemaphores[i], null);
                    vk.DestroySemaphore(device, renderFinishedSemaphores[i], null);
                    vk.DestroyFence(device, inFlightFences[i], null);
                }

                vk.DestroyCommandPool(device, commandPool, null);
                vk.DestroyPipeline(device, graphicsPipeline, null);
                vk.DestroyPipelineLayout(device, pipelineLayout, null);
            }

            headlessRenderer?.Dispose();
            terrainManager?.Dispose();
            swapchain?.Dispose();
            shaderCompiler?.Dispose();
            
            if (debugUtils != null) {
                debugUtils.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
                debugUtils.Dispose();
            }

            var khrSurface = new KhrSurface(vk.Context);
            khrSurface.DestroySurface(instance, surface, null);
            
            vk.DestroyDevice(device, null);
            vk.DestroyInstance(instance, null);
            vk.Dispose();
        }
    }
}
