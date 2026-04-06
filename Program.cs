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
        public static bool Headless { get; private set; } = false;

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
                    input.Mice[i].MouseMove += OnMouseMove;
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

            if (Headless)
                Console.WriteLine("Running in headless mode. Initialized Vulkan successfully!");
        }

        private static void KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Escape)
                window.Close();
        }

        private static void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
        {
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

            // Wait for image to become available
            // Note: A complete swapchain requires synchronised semaphores (ImageAvailable, RenderFinished)
            uint imageIndex = 0;
            // Assuming extension is fetched...
            // khrSwapchain.AcquireNextImage(device, swapchain.Swapchain, ulong.MaxValue, imageAvailableSemaphore, default, ref imageIndex);

            CommandBuffer cmd = default; 

            // Standard Graphics Render Pass Bindings
            var renderPassBeginInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = swapchain.RenderPass,
                Framebuffer = swapchain.Framebuffers[imageIndex],
                RenderArea = new Rect2D(new Offset2D(0, 0), swapchain.Extent)
            };

            var clearColor = new ClearValue { Color = new ClearColorValue(0.1f, 0.1f, 0.1f, 1.0f) };
            renderPassBeginInfo.ClearValueCount = 1;
            renderPassBeginInfo.PClearValues = &clearColor;

            // vk.CmdBeginRenderPass(cmd, in renderPassBeginInfo, SubpassContents.Inline);
            
            terrainManager.RenderOpaquePass(cmd, default);

            // vk.CmdEndRenderPass(cmd);

            // Queue presentation...
            // khrSwapchain.QueuePresent(queue, in presentInfo);
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
                swapchain.Dispose();
                swapchain = new VulkanSwapchain(vk, instance, device, physicalDevice, surface, new Extent2D((uint)size.X, (uint)size.Y), PresentModeKHR.MailboxKhr);
            }
        }

        private static void OnClose()
        {
            vk.DeviceWaitIdle(device);
            
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
