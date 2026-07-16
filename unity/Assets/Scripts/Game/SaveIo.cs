using System.IO;
using System.Numerics;

namespace D1U.Game
{
    /// <summary>Shared primitives for the savegame codec.</summary>
    public static class SaveIo
    {
        public static void Write(BinaryWriter bw, Vector3 v)
        {
            bw.Write(v.X);
            bw.Write(v.Y);
            bw.Write(v.Z);
        }

        public static Vector3 ReadVec(BinaryReader br)
            => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

        public static void Write(BinaryWriter bw, in Mat3 m)
        {
            Write(bw, m.Right);
            Write(bw, m.Up);
            Write(bw, m.Forward);
        }

        public static Mat3 ReadMat(BinaryReader br)
            => new Mat3 { Right = ReadVec(br), Up = ReadVec(br), Forward = ReadVec(br) };
    }
}
