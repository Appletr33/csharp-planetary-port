using PlanetaryTerrainRenderer.Math;
using Silk.NET.Input;
using System;

namespace PlanetaryTerrainRenderer.Debug
{
    public class CameraController
    {
        public Vector3 Position { get; set; }
        public Vector3 Forward { get; set; }
        public Vector3 Up { get; set; }
        
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        
        public float MovementSpeed { get; set; } = 50.0f;
        public float MouseSensitivity { get; set; } = 0.1f;

        public CameraController()
        {
            Position = new Vector3(0, 50, 0);
            Forward = new Vector3(0, 0, -1);
            Up = new Vector3(0, 1, 0);
        }

        public void ProcessMouseMovement(float xoffset, float yoffset)
        {
            xoffset *= MouseSensitivity;
            yoffset *= MouseSensitivity;

            Yaw += xoffset;
            Pitch += yoffset;

            if (Pitch > 89.0f) Pitch = 89.0f;
            if (Pitch < -89.0f) Pitch = -89.0f;

            UpdateCameraVectors();
        }

        public void ProcessKeyboard(IKeyboard keyboard, double deltaTime)
        {
            float velocity = MovementSpeed * (float)deltaTime;

            if (keyboard.IsKeyPressed(Key.W))
                Position = new Vector3(Position.X + Forward.X * velocity, Position.Y + Forward.Y * velocity, Position.Z + Forward.Z * velocity);
            if (keyboard.IsKeyPressed(Key.S))
                Position = new Vector3(Position.X - Forward.X * velocity, Position.Y - Forward.Y * velocity, Position.Z - Forward.Z * velocity);
            
            // Basic approximation cross product for Right vector
            Vector3 right = new Vector3(
                Forward.Y * Up.Z - Forward.Z * Up.Y,
                Forward.Z * Up.X - Forward.X * Up.Z,
                Forward.X * Up.Y - Forward.Y * Up.X
            );

            if (keyboard.IsKeyPressed(Key.A))
                Position = new Vector3(Position.X - right.X * velocity, Position.Y - right.Y * velocity, Position.Z - right.Z * velocity);
            if (keyboard.IsKeyPressed(Key.D))
                Position = new Vector3(Position.X + right.X * velocity, Position.Y + right.Y * velocity, Position.Z + right.Z * velocity);
        }

        private void UpdateCameraVectors()
        {
            float yawRad = Yaw * (float)(System.Math.PI / 180.0);
            float pitchRad = Pitch * (float)(System.Math.PI / 180.0);

            Vector3 front;
            front.X = (float)System.Math.Cos(yawRad) * (float)System.Math.Cos(pitchRad);
            front.Y = (float)System.Math.Sin(pitchRad);
            front.Z = (float)System.Math.Sin(yawRad) * (float)System.Math.Cos(pitchRad);

            // Normalize
            float length = (float)System.Math.Sqrt(front.X * front.X + front.Y * front.Y + front.Z * front.Z);
            Forward = new Vector3(front.X / length, front.Y / length, front.Z / length);
        }
    }
}
