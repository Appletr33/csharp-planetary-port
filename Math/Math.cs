using System.Runtime.InteropServices;
using Stride.Core.Mathematics;

namespace PlanetaryTerrainRenderer.Math
{
    public enum FaceRotation : uint
    {
        Identical = 0, // i
        ShiftU = 1,    // x
        RotateCCW = 2, // l
        Backside = 3,  // b
        RotateCW = 4,  // r
        ShiftV = 5,    // y
    }

    public static class FaceRotationExtensions
    {
        public static Vector2 ProjectUV(this FaceRotation rotation, Coordinate coordinate)
        {
            var u = coordinate.UV.X;
            var v = coordinate.UV.Y;
            var odd = (float)(coordinate.Face % 2);

            return rotation switch
            {
                FaceRotation.Identical => new Vector2(u, v),
                FaceRotation.ShiftU => new Vector2(odd, v),
                FaceRotation.RotateCCW => new Vector2(odd, u),
                FaceRotation.Backside => new Vector2(v, u),
                FaceRotation.RotateCW => new Vector2(v, odd),
                FaceRotation.ShiftV => new Vector2(u, odd),
                _ => new Vector2(u, v),
            };
        }

        public static FaceRotation GetFaceRotation(uint face, uint otherFace)
        {
            uint index;
            if (face % 2 == 0)
            {
                index = (6 + otherFace - face) % 6;
            }
            else
            {
                index = (6 + face - otherFace) % 6;
            }

            return (FaceRotation)index;
        }
    }

    public static class MathConstants
    {
        public const double SIGMA = 0.87 * 0.87;

        public const int BLOCK_SIZE = 8;

        public static readonly Matrix[] FACE_MATRICES = new Matrix[]
        {
            new Matrix(-1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(0.0f, 0.0f, -1.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(0.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f)
        };

        public static readonly Matrix[] INVERSE_FACE_MATRICES = new Matrix[]
        {
            new Matrix(-1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, -1.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(0.0f, 0.0f, 1.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f),
            new Matrix(0.0f, 0.0f, 1.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f)
        };

        public static readonly Int2[] NEIGHBOUR_OFFSETS = new Int2[]
        {
            new Int2(0, -1),
            new Int2(1, 0),
            new Int2(0, 1),
            new Int2(-1, 0),
            new Int2(-1, -1),
            new Int2(1, -1),
            new Int2(1, 1),
            new Int2(-1, 1)
        };

        public static readonly uint[][] NEIGHBOURING_FACES = new uint[][]
        {
            new uint[] { 0, 2, 1, 5, 4 },
            new uint[] { 1, 2, 3, 5, 0 },
            new uint[] { 2, 4, 3, 1, 0 },
            new uint[] { 3, 4, 5, 1, 2 },
            new uint[] { 4, 0, 5, 3, 2 },
            new uint[] { 5, 0, 1, 3, 4 }
        };
    }
}
