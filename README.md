# Planetary Terrain Renderer (C# Vulkan Port)

Welcome to the Csharp port of the Planetary Terrain Renderer! 

This repository is currently transitioning a Bevy/WGPU Rust project into a bare-metal C# application using **Silk.NET**, **Vulkan**, and **HLSL**.

## 🎯 Context
The core goal of this repository is to establish a dynamic, clipmap-based terrain LoD rendering system that calculates planetary noise morphing at a hardware-accelerated level. We have officially purged the original WGSL and OpenGL APIs. The engine now fundamentally tracks:
- **`Silk.NET.Vulkan`** pointers for explicit device memory and buffer configurations.
- **`Silk.NET.Shaderc`** for evaluating text `.hlsl` arrays locally into GPU Spir-V structures natively.

> **Original Rust Repository:** If you need to evaluate the baseline architectural functionality, stream chunking logic, or compute algorithms, refer to the original implementation here:
> `https://github.com/Appletr33/planetary_terrain_renderer`

---

## 🖥 Headless Pipeline
Since agents generally lack integrated GUI surfaces during autonomous execution runs, we have constructed a **Headless Output Extractor**.

You can run the engine detached from explicit window tracking by invoking:
```bash
dotnet run --headless
```

### What happens in Headless mode?
1. The engine completely bypasses `PresentModeKHR.MailboxKHR` Swapchain recreation and GLFW OS input listeners.
2. It generates a discrete procedural noise bounding box inside `TerrainManager.DebugVertexBuffer`.
3. It creates an explicit VRAM `Image` utilizing `ImageTiling.Linear`.
4. It extracts these explicit unmanaged bytes and strictly converts them out of the `Format.B8G8R8A8Srgb` channel onto your root disk via `SixLabors.ImageSharp`.
5. You can find the extracted graphic result locally at **`planet_render.png`**.
6. Diagnostic validation bounds will funnel cleanly into a **`vulkan_log.txt`** file.

---

## ⚡ Running Without A GPU (Agents)
If you are an agent executing natively in a virtualized container or server without physical Hardware Graphics support, creating the `VkInstance` or picking Physical Devices might throw exceptions. 

To trick Khronos Validation layers into creating a graphical abstraction ring on the CPU, you must inject **Vulkan Software Rendering (Mesa / SwiftShader)**.

### Emulation via Mesa (Lavapipe)
If running Linux natively underneath:
```bash
sudo apt install mesa-vulkan-drivers
export VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/lvp_icd.x86_64.json
dotnet run --headless
```

This guarantees `EnumeratePhysicalDevices` detects a valid GPU bounds (the CPU software emulation mapper), letting the codebase theoretically process `Compute.hlsl` dispatch permutations asynchronously through your core processors flawlessly!
