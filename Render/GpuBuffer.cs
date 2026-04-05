using System;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace PlanetaryTerrainRenderer.Render
{
    public unsafe class GpuBuffer<T> : IDisposable where T : unmanaged
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly PhysicalDevice _physicalDevice;
        
        public Silk.NET.Vulkan.Buffer Handle;
        public DeviceMemory Memory;
        public uint SizeInBytes { get; private set; }
        public T[] Value { get; private set; }

        public GpuBuffer(Vk vk, Device device, PhysicalDevice physicalDevice, BufferUsageFlags usage, MemoryPropertyFlags memoryFlags, uint elementCount)
        {
            _vk = vk;
            _device = device;
            _physicalDevice = physicalDevice;
            
            Value = new T[elementCount];
            SizeInBytes = (uint)(Marshal.SizeOf<T>() * elementCount);

            var bufferInfo = new Silk.NET.Vulkan.BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = SizeInBytes,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            };

            if (_vk.CreateBuffer(_device, in bufferInfo, null, out Handle) != Result.Success)
                throw new Exception("Failed to create Vulkan buffer");

            _vk.GetBufferMemoryRequirements(_device, Handle, out MemoryRequirements memRequirements);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, memoryFlags)
            };

            if (_vk.AllocateMemory(_device, in allocInfo, null, out Memory) != Result.Success)
                throw new Exception("Failed to allocate buffer memory");

            _vk.BindBufferMemory(_device, Handle, Memory, 0);
        }

        private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
        {
            _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

            for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                {
                    return (uint)i;
                }
            }

            throw new Exception("Failed to find suitable memory type");
        }

        public void SetData(T[] data)
        {
            if (data.Length != Value.Length)
                throw new ArgumentException("Data length must match buffer configured element count.");
            data.CopyTo(Value, 0);
        }

        public void Update()
        {
            void* data;
            _vk.MapMemory(_device, Memory, 0, SizeInBytes, 0, &data);
            fixed (void* ptr = Value)
            {
                System.Buffer.MemoryCopy(ptr, data, SizeInBytes, SizeInBytes);
            }
            _vk.UnmapMemory(_device, Memory);
        }

        public void Dispose()
        {
            if (Handle.Handle != 0)
            {
                _vk.DestroyBuffer(_device, Handle, null);
                _vk.FreeMemory(_device, Memory, null);
                Handle.Handle = 0;
            }
        }
    }
}
