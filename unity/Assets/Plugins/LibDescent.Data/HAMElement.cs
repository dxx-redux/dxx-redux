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
    /// <summary>
    /// The type of a HAM Element
    /// </summary>
    public enum HAMType
    {
        TMAPInfo,
        VClip,
        EClip,
        WClip,
        Robot,
        Weapon,
        Sound,
        Reactor,
        Ship,
        Powerup,
        Model,
        Gauge,
        Cockpit,
        XLAT,
    }
    /// <summary>
    /// A reference from another HAM element.
    /// </summary>
    public struct HAMReference
    {
        public HAMType Type;
        public HAMElement element;
        public int Tag;
    }

    /// <summary>
    /// Base class for all reference managed types. 
    /// </summary>
    public abstract class HAMElement
    {
        private List<HAMReference> references = new List<HAMReference>();
        public List<HAMReference> References { get => references; }

        /// <summary>
        /// Counts a reference to this HAM element.
        /// </summary>
        /// <param name="type">The type of the element referencing this element.</param>
        /// <param name="elem">The element referencing this element.</param>
        /// <param name="tag">The field that the element is using to reference this element.</param>
        public void AddReference(HAMType type, HAMElement elem, int tag)
        {
            HAMReference reference;
            reference.Type = type; reference.element = elem; reference.Tag = tag;
            references.Add(reference);
        }

        /// <summary>
        /// Clears a reference to this HAM element.
        /// </summary>
        /// <param name="type">The type of the element referencing this element.</param>
        /// <param name="elem">The element referencing this element.</param>
        /// <param name="tag">The field that the element is using to reference this element.</param>
        public void ClearReference(HAMType type, HAMElement elem, int tag)
        {
            foreach (HAMReference reference in references)
            {
                if (reference.Type == type && reference.element == elem && reference.Tag == tag)
                {
                    references.Remove(reference);
                    return;
                }
            }
        }

        /// <summary>
        /// Transfers all references to another HAM element.
        /// </summary>
        /// <param name="other">The element to transfer references to.</param>
        public void TransferReferences(HAMElement other)
        {
            other.references = references;
        }

        //TODO: Rewrite for the updated reference manager...
        public string GetReferences(IElementManager manager)
        {
            /*if (references.Count == 0)
                return "No references\r\n";
            StringBuilder stringBuilder = new StringBuilder();
            foreach (HAMReference reference in references)
            {
                stringBuilder.Append(reference.Type.ToString());
                switch (reference.Type)
                {
                    case HAMType.VClip:
                        stringBuilder.AppendFormat(" {0}: {1} ", datafile.VClipNames[reference.ID], VClip.GetTagName(reference.Tag));
                        break;
                    case HAMType.EClip:
                        stringBuilder.AppendFormat(" {0}: {1} ", datafile.EClipNames[reference.ID], EClip.GetTagName(reference.Tag));
                        break;
                    case HAMType.Robot:
                        stringBuilder.AppendFormat(" {0}: {1} ", datafile.RobotNames[reference.ID], Robot.GetTagName(reference.Tag));
                        break;
                    case HAMType.Weapon:
                        stringBuilder.AppendFormat(" {0}: {1} ", datafile.WeaponNames[reference.ID], Weapon.GetTagName(reference.Tag));
                        break;
                    case HAMType.Model:
                        stringBuilder.AppendFormat(" {0}: {1} ", datafile.ModelNames[reference.ID], Polymodel.GetTagName(reference.Tag));
                        break;
                    case HAMType.Powerup:
                        stringBuilder.AppendFormat(" {0}: {1} ", datafile.PowerupNames[reference.ID], Powerup.GetTagName(reference.Tag));
                        break;
                    case HAMType.Ship:
                        stringBuilder.AppendFormat(": {0} ", Ship.GetTagName(reference.Tag));
                        break;
                    case HAMType.Reactor:
                        stringBuilder.AppendFormat(" {0}: {1} ", datafile.ReactorNames[reference.ID], Reactor.GetTagName(reference.Tag));
                        break;
                    case HAMType.TMAPInfo:
                        stringBuilder.AppendFormat(" {0}: EClip ", reference.ID);
                        break;
                }
                stringBuilder.AppendLine();
            }
            return stringBuilder.ToString();*/
            return "it still broke k";
        }
    }
}
