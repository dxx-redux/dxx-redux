using System.Collections.Generic;
using D1U.Convert;
using D1U.Game;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Live HUD minimap (minimap.c port): the automap wireframe rendered into a
    /// small RenderTexture by an auto-leveled, slightly tilted top-down camera
    /// centered on the ship, limited to segments within a few hops. The edge
    /// list covers the whole level (MAP_EDGE_BUILD_ALL) — proximity, not
    /// exploration, is the limiter. Everything lives on a private layer so the
    /// main and mirror cameras never see it.
    /// </summary>
    public sealed class MinimapView : MonoBehaviour
    {
        public const int Layer = 30; // reserved for the PiP map (unnamed is fine)

        // ---- tuning (minimap.c:40-47) ----
        const float TiltDeg = 3277f / 65536f * 360f;      // ~18° off straight-down
        const float LevelRate = 3f;                       // auto-level slew per second
        const float MinVec = 1f / 16f;                    // degenerate-direction floor
        const float AxisHyst = 0.75f;                     // north-up axis re-pick
        static readonly int[] Hops = { 4, 6, 9 };         // near, medium, far
        static readonly float[] CamDist = { 55f, 85f, 125f };

        // automap wall palette (mapedges.c, shared with AutomapView)
        static readonly Color WallNormal = new Color(29 / 63f, 29 / 63f, 29 / 63f);
        static readonly Color WallDoor = new Color(5 / 63f, 27 / 63f, 5 / 63f);
        static readonly Color DoorBlue = new Color(0f, 0f, 31 / 63f);
        static readonly Color DoorGold = new Color(31 / 63f, 31 / 63f, 0f);
        static readonly Color DoorRed = new Color(31 / 63f, 0f, 0f);
        static readonly Color FuelcenColor = new Color(29 / 63f, 27 / 63f, 13 / 63f);
        static readonly Color ReactorColor = new Color(29 / 63f, 0f, 0f);
        static readonly Color MatcenColor = new Color(29 / 63f, 0f, 31 / 63f);
        static readonly Color ShipColor = new Color(0.2f, 1f, 0.2f);
        static readonly Color[] NetShipColors =
        {
            new Color(0.2f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f), new Color(0.3f, 1f, 0.3f),
            new Color(1f, 1f, 0.3f), new Color(1f, 0.4f, 1f), new Color(0.3f, 1f, 1f),
            new Color(1f, 0.7f, 0.3f), new Color(0.8f, 0.8f, 0.8f),
        };

        struct Edge
        {
            public int V0, V1;
            public Color32 Color;
            public int S0, S1, S2, S3; // owning segments (-1 = unused)
        }

        BakedLevel level;
        SegmentWorld world;
        Material material;
        Mesh linesMesh, markerMesh;
        Camera cam;
        RenderTexture rt;

        readonly List<Edge> edgeCache = new List<Edge>();
        Vector3[] segUp;         // per-segment (unnormalized ok) up vectors
        byte[] depth;            // 0 = out of range, 1 = ship's segment
        int lastSegnum = -1, lastRange = -1;
        Vector3 upSm, northSm;   // smoothed dirs; zero = uninitialized
        int northAxis = -1;

        public RenderTexture Texture => rt;
        public bool Active => cam != null && cam.enabled;

        public static MinimapView Create(Transform parent, BakedLevel level, SegmentWorld world,
                                         LibDescent.Data.WClip[] wclips, Shader shader)
        {
            var go = new GameObject("Minimap") { layer = Layer };
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<MinimapView>();
            view.level = level;
            view.world = world;

            view.material = RuntimeMaterials.Cutout(shader);
            view.material.name = "minimap";
            view.material.hideFlags = HideFlags.HideAndDontSave;
            if (view.material.HasProperty("_BaseColor"))
                view.material.SetColor("_BaseColor", Color.white);
            if (view.material.HasProperty("_Cull"))
                view.material.SetInt("_Cull", 0);

            view.linesMesh = new Mesh { name = "minimap_lines" };
            view.linesMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var linesGo = new GameObject("lines") { layer = Layer };
            linesGo.transform.SetParent(go.transform, false);
            linesGo.AddComponent<MeshFilter>().sharedMesh = view.linesMesh;
            linesGo.AddComponent<MeshRenderer>().sharedMaterial = view.material;

            view.markerMesh = new Mesh { name = "minimap_markers" };
            var markersGo = new GameObject("markers") { layer = Layer };
            markersGo.transform.SetParent(go.transform, false);
            markersGo.AddComponent<MeshFilter>().sharedMesh = view.markerMesh;
            markersGo.AddComponent<MeshRenderer>().sharedMaterial = view.material;

            var camGo = new GameObject("minimap_cam") { layer = Layer };
            camGo.transform.SetParent(go.transform, false);
            view.cam = camGo.AddComponent<Camera>();
            view.cam.cullingMask = 1 << Layer;
            view.cam.clearFlags = CameraClearFlags.SolidColor;
            view.cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // game view shows through
            view.cam.fieldOfView = 58f;
            view.cam.nearClipPlane = 1f;
            view.cam.farClipPlane = 400f;
            view.cam.enabled = false; // on only while the PiP is shown

            view.BuildEdges(wclips);
            view.BuildSegUps();
            return view;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (linesMesh != null) Destroy(linesMesh);
            if (markerMesh != null) Destroy(markerMesh);
            if (rt != null) { rt.Release(); Destroy(rt); }
        }

        /// <summary>(Re)size the PiP target. Returns the live texture.</summary>
        public RenderTexture EnsureTexture(int sidePx)
        {
            sidePx = Mathf.Max(32, sidePx);
            if (rt == null || rt.width != sidePx)
            {
                if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
                rt = new RenderTexture(sidePx, sidePx, 16) { name = "minimap_rt" };
                cam.targetTexture = rt;
            }
            return rt;
        }

        public void SetEnabled(bool on) => cam.enabled = on;

        // ------------------------------------------------------------------
        // static data: full-level edge list + per-segment up vectors

        void BuildEdges(LibDescent.Data.WClip[] wclips)
        {
            // map_edge_list_build(ALL): every segment contributes its colored
            // sides; shared edges dedup; near-coplanar joins aren't "defining"
            var dict = new Dictionary<(int, int), (Edge e, List<(int seg, int side)> faces, bool defining)>();
            for (int s = 0; s < level.Segments.Length; s++)
            {
                var seg = level.Segments[s];
                var sides = world.Sides[s];
                for (int sn = 0; sn < 6; sn++)
                {
                    bool haveColor = false;
                    var color = WallNormal;
                    if (seg.Children[sn] < 0)
                        haveColor = true;
                    switch (seg.Function)
                    {
                        case 1: color = FuelcenColor; haveColor = true; break;
                        case 3: color = ReactorColor; haveColor = true; break;
                        case 4: color = MatcenColor; haveColor = true; break;
                    }
                    int wallIdx = sides[sn].WallIndex;
                    if (wallIdx >= 0)
                    {
                        var wall = level.Walls[wallIdx];
                        if (wall.Type == 2) // door
                        {
                            haveColor = true;
                            if ((wall.Keys & 2) != 0) color = DoorBlue;
                            else if ((wall.Keys & 8) != 0) color = DoorGold;
                            else if ((wall.Keys & 4) != 0) color = DoorRed;
                            else if (wall.ClipNum >= wclips.Length || wclips[wall.ClipNum] == null ||
                                     (wclips[wall.ClipNum].Flags & 8) == 0)
                                color = WallDoor;
                            else
                                color = WallNormal; // secret door looks like plain wall
                        }
                        else if (wall.Type == 5) { color = WallNormal; haveColor = true; }
                        else if (wall.Type == 1) { color = WallDoor; haveColor = true; }
                    }
                    if (!haveColor)
                        continue;

                    var order = SegmentWorld.SideToVerts[sn];
                    for (int k = 0; k < 4; k++)
                    {
                        int va = seg.Verts[order[k]], vb = seg.Verts[order[(k + 1) & 3]];
                        if (va > vb) (va, vb) = (vb, va);
                        if (!dict.TryGetValue((va, vb), out var entry))
                        {
                            entry = (new Edge { V0 = va, V1 = vb, Color = color, S0 = s, S1 = -1, S2 = -1, S3 = -1 },
                                     new List<(int, int)>(4), true);
                        }
                        else if (color != WallNormal)
                        {
                            entry.e.Color = color;
                        }
                        // record owning segments (up to 4) for the hop filter
                        if (entry.e.S0 < 0) entry.e.S0 = s;
                        else if (entry.e.S1 < 0 && entry.e.S0 != s) entry.e.S1 = s;
                        else if (entry.e.S2 < 0 && entry.e.S0 != s && entry.e.S1 != s) entry.e.S2 = s;
                        else if (entry.e.S3 < 0 && entry.e.S0 != s && entry.e.S1 != s && entry.e.S2 != s) entry.e.S3 = s;
                        if (entry.faces.Count < 4)
                            entry.faces.Add((s, sn));
                        dict[(va, vb)] = entry;
                    }
                }
            }

            // coplanar prune (mapedges.c:309): edges between nearly parallel
            // faces of different segments don't define shape — drop them
            edgeCache.Clear();
            foreach (var entry in dict.Values)
            {
                bool defining = true;
                var faces = entry.faces;
                for (int i = 0; i < faces.Count && defining; i++)
                    for (int j = i + 1; j < faces.Count; j++)
                    {
                        if (faces[i].seg == faces[j].seg)
                            continue;
                        var n1 = world.Sides[faces[i].seg][faces[i].side].Normals[0];
                        var n2 = world.Sides[faces[j].seg][faces[j].side].Normals[0];
                        if (System.Numerics.Vector3.Dot(n1, n2) > 0.9f)
                        {
                            defining = false;
                            break;
                        }
                    }
                if (defining)
                    edgeCache.Add(entry.e);
            }
            depth = new byte[level.Segments.Length];
        }

        // segment "up" = top-side centroid minus bottom-side centroid (minimap.c:105)
        void BuildSegUps()
        {
            segUp = new Vector3[level.Segments.Length];
            var top = SegmentWorld.SideToVerts[1];    // WTOP (segment.h:40)
            var bottom = SegmentWorld.SideToVerts[3]; // WBOTTOM (segment.h:42)
            for (int s = 0; s < level.Segments.Length; s++)
            {
                var seg = level.Segments[s];
                Vector3 vtop = Vector3.zero, vbot = Vector3.zero;
                for (int i = 0; i < 4; i++)
                {
                    vtop += ToUnity(level.Vertices[seg.Verts[top[i]]]);
                    vbot += ToUnity(level.Vertices[seg.Verts[bottom[i]]]);
                }
                var up = vtop - vbot;
                segUp[s] = up.magnitude >= MinVec ? up.normalized : Vector3.zero;
            }
        }

        // ------------------------------------------------------------------
        // per-frame

        static void Smooth(ref Vector3 cur, Vector3 target, float dt)
        {
            float k = Mathf.Min(1f, dt * LevelRate);
            if (cur == Vector3.zero) { cur = target; return; }
            cur += (target - cur) * k;
            if (cur.magnitude < MinVec) cur = target; // near-opposite blend collapsed
            else cur.Normalize();
        }

        /// <summary>Update the camera + meshes. netShips may be null.</summary>
        public void UpdateView(Vector3 shipPos, in Mat3 orient, int shipSeg, float dt,
                               int range, bool northUp,
                               IReadOnlyDictionary<int, GameObject> netShips)
        {
            range = Mathf.Clamp(range, 0, 2);
            int hops = Hops[range];
            float dist = CamDist[range];

            if (shipSeg != lastSegnum || range != lastRange)
            {
                ComputeDepths(shipSeg, hops);
                lastSegnum = shipSeg;
                lastRange = range;
                RebuildLines(hops);
            }

            // auto-level: average up over in-range segments, then smooth
            var upTarget = Vector3.zero;
            for (int s = 0; s < depth.Length; s++)
                if (depth[s] != 0)
                    upTarget += segUp[s];
            if (upTarget.magnitude < MinVec)
            {
                if (upSm == Vector3.zero)
                    return; // no usable orientation yet
                upTarget = upSm;
            }
            else
            {
                upTarget.Normalize();
            }
            Smooth(ref upSm, upTarget, dt);

            // in-plane "north" (screen-up): ship heading, or a world axis
            Vector3 northTarget;
            var fwd = new Vector3(orient.Forward.X, orient.Forward.Y, orient.Forward.Z);
            if (!northUp)
            {
                northTarget = fwd - upSm * Vector3.Dot(fwd, upSm);
                if (northTarget.magnitude < MinVec)
                {
                    if (northSm == Vector3.zero) return;
                    northTarget = northSm;
                }
                else northTarget.Normalize();
            }
            else
            {
                float[] comp = { Mathf.Abs(upSm.x), Mathf.Abs(upSm.y), Mathf.Abs(upSm.z) };
                if (northAxis < 0 || comp[northAxis] > AxisHyst)
                {
                    northAxis = 0;
                    if (comp[1] < comp[northAxis]) northAxis = 1;
                    if (comp[2] < comp[northAxis]) northAxis = 2;
                }
                northTarget = northAxis == 0 ? Vector3.right : northAxis == 1 ? Vector3.up : Vector3.forward;
                northTarget -= upSm * Vector3.Dot(northTarget, upSm);
                if (northTarget.magnitude < MinVec)
                {
                    if (northSm == Vector3.zero) return;
                    northTarget = northSm;
                }
                else northTarget.Normalize();
            }
            Smooth(ref northSm, northTarget, dt);
            northSm -= upSm * Vector3.Dot(northSm, upSm); // re-orthogonalize
            if (northSm.magnitude < MinVec) { northSm = Vector3.zero; return; }
            northSm.Normalize();

            // camera above the ship, looking down with a slight tilt (minimap.c:242)
            float st = Mathf.Sin(TiltDeg * Mathf.Deg2Rad), ct = Mathf.Cos(TiltDeg * Mathf.Deg2Rad);
            var camF = -upSm * ct + northSm * st;
            var camU = northSm * ct + upSm * st;
            cam.transform.SetPositionAndRotation(shipPos - camF * dist,
                Quaternion.LookRotation(camF, camU));

            RebuildMarkers(shipPos, orient, netShips);
        }

        void ComputeDepths(int startSeg, int maxDepth)
        {
            System.Array.Clear(depth, 0, depth.Length);
            if (startSeg < 0 || startSeg >= depth.Length)
                return;
            var queue = new Queue<int>();
            depth[startSeg] = 1;
            queue.Enqueue(startSeg);
            while (queue.Count > 0)
            {
                int s = queue.Dequeue();
                if (depth[s] > maxDepth)
                    continue; // fringe ring is marked but not expanded
                var sides = world.Sides[s];
                for (int sn = 0; sn < 6; sn++)
                {
                    int child = sides[sn].Child;
                    if (child >= 0 && depth[child] == 0)
                    {
                        depth[child] = (byte)(depth[s] + 1);
                        queue.Enqueue(child);
                    }
                }
            }
        }

        void RebuildLines(int hops)
        {
            var positions = new List<Vector3>(edgeCache.Count);
            var colors = new List<Color32>(edgeCache.Count);
            foreach (var e in edgeCache)
            {
                int dmin = 255;
                if (e.S0 >= 0 && depth[e.S0] != 0 && depth[e.S0] < dmin) dmin = depth[e.S0];
                if (e.S1 >= 0 && depth[e.S1] != 0 && depth[e.S1] < dmin) dmin = depth[e.S1];
                if (e.S2 >= 0 && depth[e.S2] != 0 && depth[e.S2] < dmin) dmin = depth[e.S2];
                if (e.S3 >= 0 && depth[e.S3] != 0 && depth[e.S3] < dmin) dmin = depth[e.S3];
                if (dmin > hops + 1)
                    continue; // no face in range
                var c = e.Color;
                if (dmin > hops) // fringe ring fades out (minimap.c:322)
                {
                    c.r = (byte)(c.r / 2);
                    c.g = (byte)(c.g / 2);
                    c.b = (byte)(c.b / 2);
                }
                positions.Add(ToUnity(level.Vertices[e.V0]));
                positions.Add(ToUnity(level.Vertices[e.V1]));
                colors.Add(c);
                colors.Add(c);
            }
            linesMesh.Clear();
            linesMesh.SetVertices(positions);
            linesMesh.SetColors(colors);
            var indices = new int[positions.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;
            linesMesh.SetIndices(indices, MeshTopology.Lines, 0);
            linesMesh.RecalculateBounds();
        }

        readonly List<Vector3> markerPos = new List<Vector3>();
        readonly List<Color32> markerCol = new List<Color32>();

        void RebuildMarkers(Vector3 shipPos, in Mat3 orient,
                            IReadOnlyDictionary<int, GameObject> netShips)
        {
            markerPos.Clear();
            markerCol.Clear();
            AddArrow(shipPos,
                new Vector3(orient.Forward.X, orient.Forward.Y, orient.Forward.Z),
                new Vector3(orient.Right.X, orient.Right.Y, orient.Right.Z),
                new Vector3(orient.Up.X, orient.Up.Y, orient.Up.Z),
                ShipColor);
            if (netShips != null)
                foreach (var kv in netShips)
                {
                    if (kv.Value == null)
                        continue;
                    var t = kv.Value.transform;
                    AddArrow(t.position, t.forward, t.right, t.up,
                        NetShipColors[kv.Key & 7]);
                }

            markerMesh.Clear();
            markerMesh.SetVertices(markerPos);
            markerMesh.SetColors(markerCol);
            var indices = new int[markerPos.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;
            markerMesh.SetIndices(indices, MeshTopology.Lines, 0);
            markerMesh.RecalculateBounds();
        }

        void AddArrow(Vector3 pos, Vector3 fwd, Vector3 right, Vector3 up, Color color)
        {
            var c = (Color32)color;
            var nose = pos + fwd * 8f;
            var tailL = pos - fwd * 5f - right * 4f;
            var tailR = pos - fwd * 5f + right * 4f;
            var fin = pos - fwd * 5f + up * 4f;
            void Line(Vector3 a, Vector3 b)
            {
                markerPos.Add(a); markerPos.Add(b);
                markerCol.Add(c); markerCol.Add(c);
            }
            Line(tailL, nose);
            Line(tailR, nose);
            Line(tailL, tailR);
            Line(fin, nose);
        }

        static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
