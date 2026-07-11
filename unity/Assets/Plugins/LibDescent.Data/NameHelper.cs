using System;
using System.Linq;

namespace LibDescent.Data
{
    public static class NameHelper
    {
        public static byte[] GetNameBytes(string name, int lenght)
        {
            var nameChars = name.ToCharArray();

            return GetNameBytes(nameChars, lenght);
        }

        public static byte[] GetNameBytes(char[] nameChars, int lenght)
        {
            var resultBytes = new byte[lenght];

            var nameBytes = nameChars.Select(c => (byte)c).ToArray();

            if (nameChars.Length >= lenght)
            {
                Array.Copy(nameBytes, resultBytes, lenght);
            }
            else
            {
                Array.Copy(nameBytes, resultBytes, nameBytes.Length);
            }

            return resultBytes;
        }
    }
}
