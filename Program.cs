using System;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;

namespace PlanetaryTerrainRenderer
{
    class Program
    {
        private static IWindow window = null!;
        private static GL gl = null!;
        public static bool Headless { get; private set; } = false;

        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--headless")
            {
                Headless = true;
            }

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1280, 720);
            options.Title = "Planetary Terrain Renderer";

            // For headless we could use EGL or an invisible window. We'll use invisible for simplicity.
            if (Headless)
            {
                options.IsVisible = false;
            }

            window = Window.Create(options);

            window.Load += OnLoad;
            window.Render += OnRender;
            window.Update += OnUpdate;
            window.Resize += OnResize;
            window.Closing += OnClose;

            if (Headless)
            {
                // In headless, we just init, run one frame, and exit
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
            gl = window.CreateOpenGL();
            gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

            // Setup offscreen framebuffer for headless mode here if needed
            if (Headless)
            {
                Console.WriteLine("Running in headless mode. Rendering frame...");
            }
        }

        private static void OnRender(double deltaTime)
        {
            gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            // Render logic here

            if (Headless)
            {
                Console.WriteLine("Frame rendered to offscreen buffer.");
            }
        }

        private static void OnUpdate(double deltaTime)
        {
            // Update logic here
        }

        private static void OnResize(Vector2D<int> size)
        {
            gl.Viewport(size);
        }

        private static void OnClose()
        {
            // Cleanup
            gl?.Dispose();
        }
    }
}
