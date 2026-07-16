using System.IO;

namespace LibDescent.Data
{
    public class DescentReader : BinaryReader
    {
        public DescentReader(Stream input) : base(input)
        {
        }

        public Fix ReadFix()
        {
            var intvalue = base.ReadInt32();

            Fix fix = new Fix(intvalue);

            return fix;
        }

        public FixVector ReadFixVector()
        {
            var x = this.ReadFix();
            var y = this.ReadFix();
            var z = this.ReadFix();


            FixVector result = new FixVector(x, y, z);

            return result;
        }

    }
}
