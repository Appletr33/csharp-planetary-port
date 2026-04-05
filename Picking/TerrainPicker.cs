using PlanetaryTerrainRenderer.Math;
using PlanetaryTerrainRenderer.TerrainData;
using System.Collections.Generic;

namespace PlanetaryTerrainRenderer.Picking
{
    public class TerrainPicker
    {
        public bool Raycast(TileTree tree, Ray ray, out Vector3 hitPoint)
        {
            hitPoint = new Vector3(0, 0, 0);
            float nearestDistance = float.MaxValue;
            bool hit = false;

            // Simplified mock implementation of quad-tree intersection.
            // In a full implementation, you would intersect the ray against each TileTree bound,
            // recursively testing lower LODs (sub-quads) if an intersection occurs,
            // parsing down until you intersect the highest loaded geometry layer (using the Atlas).
            
            // For now, bounding box check against entire volume
            BoundingBox terrainBounds = new BoundingBox 
            { 
                Min = new Vector3(-1000, -100, -1000), 
                Max = new Vector3(1000, 100, 1000) 
            };

            if (terrainBounds.Intersects(ray, out float dist))
            {
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    hitPoint = new Vector3(
                        ray.Origin.X + ray.Direction.X * dist,
                        ray.Origin.Y + ray.Direction.Y * dist,
                        ray.Origin.Z + ray.Direction.Z * dist
                    );
                    hit = true;
                }
            }

            return hit;
        }
    }
}
