/*
    Copyright (c) 2019 SaladBadger

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System.Collections.Generic;

namespace LibDescent.Data
{
    public class DynamicLight
    {
        public Side Source { get; }

        public DynamicLight(Side source)
        {
            Source = source;
        }

        public List<LightDelta> LightDeltas { get; } = new List<LightDelta>();
    }

    public struct LightDelta
    {
        /// <summary>
        /// The side whose light values are modified by the owning light source.
        /// </summary>
        public Side targetSide;
        /// <summary>
        /// The amount to add to target vertices' light values when this light delta is applied.
        /// Typically inverted (i.e. subtracted) when the light is turned off.
        /// </summary>
        public Fix[] vertexDeltas;

        public LightDelta(Side targetSide)
        {
            this.targetSide = targetSide;
            vertexDeltas = new Fix[Side.MaxVertices];
        }
    }
}