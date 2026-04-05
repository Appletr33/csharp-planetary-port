using System;
using System.Collections.Generic;

namespace PlanetaryTerrainRenderer
{
    public class TerrainViewConfig
    {
        public uint TreeSize { get; set; } = 16;
        public uint GeometryTileCount { get; set; } = 1000000;
        public uint RefinementCount { get; set; } = 30;
        public uint GridSize { get; set; } = 16;
        public float MorphRange { get; set; } = 0.2f;
        public float BlendRange { get; set; } = 0.2f;
        public double MorphDistance { get; set; } = 40.0;
        public double BlendDistance { get; set; } = 5.0;
        public double SubdivisionTolerance { get; set; } = 0.1;
        public double LoadTolerance { get; set; } = 0.2;
        public double PrecisionDistance { get; set; } = 0.001;
        public uint ViewLod { get; set; } = 10;
        public uint Order { get; set; } = 0;
    }

    public class TerrainViewComponents<C>
    {
        public Dictionary<Tuple<int, int>, C> Components { get; } = new Dictionary<Tuple<int, int>, C>();
    }
}
