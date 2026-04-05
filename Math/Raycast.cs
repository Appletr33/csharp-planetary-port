using System;

namespace PlanetaryTerrainRenderer.Math
{
    public struct Ray
    {
        public Vector3 Origin;
        public Vector3 Direction;
        
        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }
    }

    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;
        
        public bool Intersects(Ray ray, out float distance)
        {
            // Simple AABB intersection (Slab method)
            float tmin = (Min.X - ray.Origin.X) / ray.Direction.X;
            float tmax = (Max.X - ray.Origin.X) / ray.Direction.X;

            if (tmin > tmax) { float temp = tmin; tmin = tmax; tmax = temp; }

            float tymin = (Min.Y - ray.Origin.Y) / ray.Direction.Y;
            float tymax = (Max.Y - ray.Origin.Y) / ray.Direction.Y;

            if (tymin > tymax) { float temp = tymin; tymin = tymax; tymax = temp; }

            distance = tmin;
            if ((tmin > tymax) || (tymin > tmax)) return false;

            if (tymin > tmin) tmin = tymin;
            if (tymax < tmax) tmax = tymax;

            float tzmin = (Min.Z - ray.Origin.Z) / ray.Direction.Z;
            float tzmax = (Max.Z - ray.Origin.Z) / ray.Direction.Z;

            if (tzmin > tzmax) { float temp = tzmin; tzmin = tzmax; tzmax = temp; }

            if ((tmin > tzmax) || (tzmin > tmax)) return false;

            if (tzmin > tmin) tmin = tzmin;
            if (tzmax < tmax) tmax = tzmax;

            distance = tmin;
            return true;
        }
    }
}
