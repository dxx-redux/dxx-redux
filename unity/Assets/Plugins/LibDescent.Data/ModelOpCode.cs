using System;
using System.Collections.Generic;
using System.Text;

namespace LibDescent.Data
{
    class ModelOpCode
    {
        public const Int16 End = 0;

        public const Int16 Points = 1;
        
        public const Int16 FlatPoly = 2;

        public const Int16 TexturedPoly = 3;

        public const Int16 SortNormal = 4;

        public const Int16 Rod = 5; 

        public const Int16 SubCall = 6;

        public const Int16 DefinePointStart = 7;

        public const Int16 Glow = 8;
    }
}
