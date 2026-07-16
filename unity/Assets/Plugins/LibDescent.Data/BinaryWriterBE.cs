using System;
using System.IO;

namespace LibDescent.Data
{
    /// <summary>
    /// BinaryWriter for big-endian data.
    /// </summary>
    public class BinaryWriterBE : BinaryWriter
    {
        public BinaryWriterBE(Stream stream) : base(stream) { }

        private byte[] MaybeReverse(byte[] result)
        {
            if (BitConverter.IsLittleEndian) // reverse array if BitConverter writes little endian
            {
                int n = result.Length;
                byte t;
                for (int i = 0; i < n / 2; ++i)
                {
                    t = result[n - i - 1];
                    result[n - i - 1] = result[i];
                    result[i] = t;
                }
            }
            return result;
        }

        public override void Write(short value)
        {
            base.Write(MaybeReverse(BitConverter.GetBytes(value)));
        }

        public override void Write(ushort value)
        {
            base.Write(MaybeReverse(BitConverter.GetBytes(value)));
        }

        public override void Write(int value)
        {
            base.Write(MaybeReverse(BitConverter.GetBytes(value)));
        }

        public override void Write(uint value)
        {
            base.Write(MaybeReverse(BitConverter.GetBytes(value)));
        }

        public override void Write(long value)
        {
            base.Write(MaybeReverse(BitConverter.GetBytes(value)));
        }

        public override void Write(ulong value)
        {
            base.Write(MaybeReverse(BitConverter.GetBytes(value)));
        }

        public override void Write(float value)
        {
            base.Write(MaybeReverse(BitConverter.GetBytes(value)));
        }

        public override void Write(double value)
        {
            base.Write(MaybeReverse(BitConverter.GetBytes(value)));
        }
    }
}