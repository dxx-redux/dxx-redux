using System;
using System.IO;

namespace LibDescent.Data
{
    /// <summary>
    /// BinaryReader for big-endian data.
    /// </summary>
    public class BinaryReaderBE : BinaryReader
    {
        public BinaryReaderBE(Stream stream) : base(stream) { }
        private byte[] buffer = new byte[16];

        protected override void FillBuffer(int numBytes)
        {
            if (BaseStream.Read(buffer, 0, numBytes) < numBytes)
                throw new EndOfStreamException();
            if (BitConverter.IsLittleEndian) // reverse array if BitConverter reads little endian
            {
                byte t;
                for (int i = 0; i < numBytes / 2; ++i)
                {
                    t = buffer[numBytes - i - 1];
                    buffer[numBytes - i - 1] = buffer[i];
                    buffer[i] = t;
                }
            }
        }

        public override short ReadInt16()
        {
            FillBuffer(2);
            return BitConverter.ToInt16(buffer, 0);
        }

        public override ushort ReadUInt16()
        {
            FillBuffer(2);
            return BitConverter.ToUInt16(buffer, 0);
        }

        public override int ReadInt32()
        {
            FillBuffer(4);
            return BitConverter.ToInt32(buffer, 0);
        }

        public override uint ReadUInt32()
        {
            FillBuffer(4);
            return BitConverter.ToUInt32(buffer, 0);
        }

        public override long ReadInt64()
        {
            FillBuffer(8);
            return BitConverter.ToInt64(buffer, 0);
        }

        public override ulong ReadUInt64()
        {
            FillBuffer(8);
            return BitConverter.ToUInt64(buffer, 0);
        }

        public override float ReadSingle()
        {
            FillBuffer(4);
            return BitConverter.ToSingle(buffer, 0);
        }

        public override double ReadDouble()
        {
            FillBuffer(8);
            return BitConverter.ToDouble(buffer, 0);
        }
    }
}