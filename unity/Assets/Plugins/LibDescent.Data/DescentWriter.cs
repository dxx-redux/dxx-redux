using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LibDescent.Data
{
    public class DescentWriter : BinaryWriter
    {
        public DescentWriter(Stream input) : base(input)
        {
        }

        public void WriteByte(byte value)
        {
            base.Write(value);
        }

        public void WriteSByte(sbyte value)
        {
            base.Write(value);
        }

        public void WriteInt16(Int16 value)
        {
            base.Write(value);
        }

        public void WriteUInt16(UInt16 value)
        {
            base.Write(value);
        }

        public void WriteInt32(Int32 value)
        {
            base.Write(value);
        }

        public void WriteFix(Fix value)
        {
            this.WriteInt32(value.Value);
        }

        public void WriteFixVector(FixVector a)
        {
            this.WriteFix(a.X);
            this.WriteFix(a.Y);
            this.WriteFix(a.Z);
        }

        public void WriteMany<T>(int count, T[] items, Action<DescentWriter, T> writeAction)
        {
            for (var i = 0; i < count; i++)
            {
                writeAction(this, items[i]);
            }
        }
    }
}
