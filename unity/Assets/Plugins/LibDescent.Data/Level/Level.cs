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
using System.IO;
using System.Linq;

namespace LibDescent.Data
{
    public interface ILevel
    {
        string LevelName { get; set; }
        List<LevelVertex> Vertices { get; }
        List<Segment> Segments { get; }
        List<LevelObject> Objects { get; }
        List<Wall> Walls { get; }
        IReadOnlyList<ITrigger> Triggers { get; }
        List<Side> ReactorTriggerTargets { get; }
        IReadOnlyList<IMatCenter> MatCenters { get; }

        void WriteToStream(Stream stream);
    }

    internal class DescentLevelCommon
    {
        public string LevelName { get; set; }

        public List<LevelVertex> Vertices { get; } = new List<LevelVertex>();

        public List<Segment> Segments { get; } = new List<Segment>();

        public List<LevelObject> Objects { get; } = new List<LevelObject>();

        public List<Wall> Walls { get; } = new List<Wall>();

        public const int MaxReactorTriggerTargets = 10;
        public List<Side> ReactorTriggerTargets { get; } = new List<Side>(MaxReactorTriggerTargets);
    }

    public class D1Level : ILevel
    {
        private DescentLevelCommon _commonData = new DescentLevelCommon();

        public List<D1Trigger> Triggers { get; } = new List<D1Trigger>();

        public List<MatCenter> MatCenters { get; } = new List<MatCenter>();

        #region ILevel implementation
        public string LevelName { get => _commonData.LevelName; set => _commonData.LevelName = value; }
        public List<LevelVertex> Vertices => _commonData.Vertices;
        public List<Segment> Segments => _commonData.Segments;
        public List<LevelObject> Objects => _commonData.Objects;
        public List<Wall> Walls => _commonData.Walls;
        IReadOnlyList<ITrigger> ILevel.Triggers => Triggers;
        public List<Side> ReactorTriggerTargets => _commonData.ReactorTriggerTargets;
        IReadOnlyList<IMatCenter> ILevel.MatCenters => MatCenters;
        #endregion

        public static D1Level CreateFromStream(Stream stream)
        {
            return new D1LevelReader(stream).Load();
        }

        public void WriteToStream(Stream stream)
        {
            new D1LevelWriter(this, stream).Write();
        }
    }

    public class D2Level : ILevel
    {
        private DescentLevelCommon _commonData = new DescentLevelCommon();

        public List<D2Trigger> Triggers { get; } = new List<D2Trigger>();

        public List<MatCenter> MatCenters { get; } = new List<MatCenter>();

        public string PaletteName { get; set; }

        public const int DefaultBaseReactorCountdownTime = 30;

        /// <summary>
        /// The countdown time when the reactor is destroyed, on Insane difficulty.
        /// Lower difficulties use multiples of this value.
        /// </summary>
        public int BaseReactorCountdownTime { get; set; } = DefaultBaseReactorCountdownTime;

        /// <summary>
        /// How many "shields" the reactor has.
        /// Null causes Descent 2 to use the default strength, based on the level number.
        /// </summary>
        public int? ReactorStrength { get; set; } = null;

        public List<DynamicLight> DynamicLights { get; } = new List<DynamicLight>();

        public List<AnimatedLight> AnimatedLights { get; } = new List<AnimatedLight>();

        public Segment SecretReturnSegment { get; set; }

        public FixMatrix SecretReturnOrientation { get; set; }

        #region ILevel implementation
        public string LevelName { get => _commonData.LevelName; set => _commonData.LevelName = value; }
        public List<LevelVertex> Vertices => _commonData.Vertices;
        public List<Segment> Segments => _commonData.Segments;
        public List<LevelObject> Objects => _commonData.Objects;
        public List<Wall> Walls => _commonData.Walls;
        IReadOnlyList<ITrigger> ILevel.Triggers => Triggers;
        public List<Side> ReactorTriggerTargets => _commonData.ReactorTriggerTargets;
        IReadOnlyList<IMatCenter> ILevel.MatCenters => MatCenters;
        #endregion

        public static D2Level CreateFromStream(Stream stream)
        {
            return new D2LevelReader(stream).Load();
        }

        public void WriteToStream(Stream stream)
        {
            new D2LevelWriter(this, stream, false).Write();
        }
    }

    public enum FogType
    {
        Water = 0,
        Lava = 1,
        LightFog = 2,
        HeavyFog = 3
    }

    public struct FogPreset
    {
        public Color color;
        public float density;
    }

    public class D2XXLLevel : D2Level, ILevel
    {
        public new List<D2XXLTrigger> Triggers { get; } = new List<D2XXLTrigger>();

        public new List<IMatCenter> MatCenters { get; } = new List<IMatCenter>();

        public List<SegmentGroup> SegmentGroups { get; } = new List<SegmentGroup>();

#pragma warning disable CA1819 // Callers can modify the contents of the array; this is by design.
        public FogPreset[] FogPresets { get; } = new FogPreset[]
#pragma warning restore CA1819
        {
            // Default values are adapted from DLE.
            // Water
            new FogPreset
            {
                color = new Color(255, (int)(255.0f * 0.2f), (int)(255.0f * 0.4f), (int)(255.0f * 0.6f)),
                density = (11f / 20f)
            },
            // Lava
            new FogPreset
            {
                color = new Color(255, (int)(255.0f * 0.8f), (int)(255.0f * 0.4f), (int)(255.0f * 0.0f)),
                density = (2f / 20f)
            },
            // LightFog
            new FogPreset
            {
                color = new Color(255, (int)(255.0f * 0.7f), (int)(255.0f * 0.7f), (int)(255.0f * 0.7f)),
                density = (4f / 20f)
            },
            // HeavyFog
            new FogPreset
            {
                color = new Color(255, (int)(255.0f * 0.7f), (int)(255.0f * 0.7f), (int)(255.0f * 0.7f)),
                density = (11f / 20f)
            }
        };

        #region ILevel implementation
        IReadOnlyList<ITrigger> ILevel.Triggers => Triggers;
        IReadOnlyList<IMatCenter> ILevel.MatCenters => MatCenters;
        #endregion

        public static new D2XXLLevel CreateFromStream(Stream stream)
        {
            return new D2XXLLevelReader(stream).Load();
        }

        public new void WriteToStream(Stream stream)
        {
            new D2XXLLevelWriter(this, stream).Write();
        }
    }

    public static partial class Extensions
    {
        // It's not clear why .NET doesn't define this already, but it doesn't.
        // Remove if that changes.
        public static int IndexOf<T>(this IReadOnlyList<T> list, T obj)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Equals(obj))
                {
                    return i;
                }
            }
            return -1;
        }

        public static IReadOnlyList<MatCenter> GetRobotMatCenters(this ILevel level)
        {
            return level.MatCenters.Where(m => m is MatCenter).Cast<MatCenter>().ToList();
        }

        public static IReadOnlyList<PowerupMatCenter> GetPowerupMatCenters(this ILevel level)
        {
            return level.MatCenters.Where(m => m is PowerupMatCenter).Cast<PowerupMatCenter>().ToList();
        }

        public static IReadOnlyList<ITrigger> GetWallTriggers(this ILevel level)
        {
            return level.Triggers.Where(t => t.ConnectedWalls.Count > 0).ToList();
        }

        public static IReadOnlyList<ITrigger> GetObjectTriggers(this ILevel level)
        {
            return level.Triggers.Where(t => t.ConnectedObjects.Count > 0).ToList();
        }
    }
}
