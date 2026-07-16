using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace LibDescent.Data
{
    public partial class Segment
    {
        public static Segment FromXML(XElement element)
        {
            if (element.Name != "Segment")
            {
                throw new InvalidDataException("Element is not a Segment.");
            }
            var vertices = element.Element("Vertices").Elements("Vertex");
            var sides = element.Element("Sides").Elements("Side");

            var segment = new Segment((uint)sides.Count(), (uint)vertices.Count());
            vertices.ToList().ConvertAll(v => new LevelVertex(
                double.Parse(v.Attribute("x").Value),
                double.Parse(v.Attribute("y").Value),
                double.Parse(v.Attribute("z").Value))
            ).CopyTo(segment.Vertices);
            sides.ToList().ConvertAll(s =>
            {
                var sideNum = sides.ToList().IndexOf(s);
                var uvls = s.Element("Uvls").Elements("Uvl");
                var side = new Side(segment, (uint)sideNum, (uint)uvls.Count());
                uvls.ToList().ConvertAll(uvl => new Uvl(
                    double.Parse(uvl.Attribute("u").Value, CultureInfo.InvariantCulture),
                    double.Parse(uvl.Attribute("v").Value, CultureInfo.InvariantCulture),
                    double.Parse(uvl.Attribute("l").Value, CultureInfo.InvariantCulture))
                ).CopyTo(side.Uvls);
                return side;
            }).CopyTo(segment.Sides);

            return segment;
        }
    }
}
