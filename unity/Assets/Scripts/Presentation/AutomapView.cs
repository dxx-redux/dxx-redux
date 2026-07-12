using System;
using System.Collections.Generic;
using D1U.Convert;
using D1U.Game;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// The Tab automap: a wireframe of the visited part of the mine, ported
    /// from automap.c/mapedges.c — same edge dedup, coplanar-edge pruning,
    /// wall color rules and frontier hiding. While open the level renderers
    /// are hidden, the sim is paused, and the camera orbits the ship
    /// (mouse = rotate, wheel = zoom).
    /// </summary>
    public sealed class AutomapView : MonoBehaviour
    {
        // BM_XRGB(r,g,b) values from mapedges.c / automap.c, /63
        static readonly Color WallNormal = new Color(29 / 63f, 29 / 63f, 29 / 63f);
        static readonly Color WallDoor = new Color(5 / 63f, 27 / 63f, 5 / 63f);
        static readonly Color DoorBlue = new Color(0f, 0f, 31 / 63f);
        static readonly Color DoorGold = new Color(31 / 63f, 31 / 63f, 0f);
        static readonly Color DoorRed = new Color(31 / 63f, 0f, 0f);
        static readonly Color FuelcenColor = new Color(29 / 63f, 27 / 63f, 13 / 63f);
        static readonly Color ReactorColor = new Color(29 / 63f, 0f, 0f);
        static readonly Color MatcenColor = new Color(29 / 63f, 0f, 31 / 63f);
        static readonly Color PlayerStartColor = new Color(31 / 63f, 0f, 31 / 63f);
        static readonly Color HostageColor = new Color(0f, 31 / 63f, 0f);
        static readonly Color KeyRed = new Color(1f, 5 / 63f, 5 / 63f);
        static readonly Color KeyBlue = new Color(5 / 63f, 5 / 63f, 1f);
        static readonly Color KeyGold = new Color(1f, 1f, 10 / 63f);
        static readonly Color ShipColor = new Color(0.2f, 1f, 0.2f);

        [Flags]
        enum EdgeFlags : byte
        {
            Used = 1, Defining = 2, Frontier = 4, Secret = 8, NoFade = 16, Grate = 32,
        }

        sealed class EdgeInfo
        {
            public int V0, V1;
            public Color Color;
            public EdgeFlags Flags;
            public readonly List<(int seg, int side)> Faces = new List<(int, int)>(4);
        }

        BakedLevel level;
        SegmentWorld world;
        LibDescent.Data.WClip[] wclips;
        Shader shader;
        Material material;
        Mesh linesMesh;
        Mesh markerMesh;

        // orbit view (automap.c: PITCH_DEFAULT 9000 fixang, ZOOM i2f(20*10))
        float yaw;
        float pitch = 9000f / 65536f * 360f;
        float dist = 200f;

        public static AutomapView Create(Transform parent, BakedLevel level, SegmentWorld world,
                                         LibDescent.Data.WClip[] wclips, Shader shader)
        {
            var go = new GameObject("Automap");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<AutomapView>();
            view.level = level;
            view.world = world;
            view.wclips = wclips;
            view.shader = shader;

            view.material = RuntimeMaterials.Cutout(shader);
            view.material.name = "automap";
            view.material.hideFlags = HideFlags.HideAndDontSave;
            if (view.material.HasProperty("_BaseColor"))
                view.material.SetColor("_BaseColor", Color.white);
            if (view.material.HasProperty("_Cull"))
                view.material.SetInt("_Cull", 0);

            view.linesMesh = new Mesh { name = "automap_lines" };
            view.linesMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var linesGo = new GameObject("lines");
            linesGo.transform.SetParent(go.transform, false);
            linesGo.AddComponent<MeshFilter>().sharedMesh = view.linesMesh;
            linesGo.AddComponent<MeshRenderer>().sharedMaterial = view.material;

            view.markerMesh = new Mesh { name = "automap_markers" };
            var markersGo = new GameObject("markers");
            markersGo.transform.SetParent(go.transform, false);
            markersGo.AddComponent<MeshFilter>().sharedMesh = view.markerMesh;
            markersGo.AddComponent<MeshRenderer>().sharedMaterial = view.material;
            return view;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (linesMesh != null) Destroy(linesMesh);
            if (markerMesh != null) Destroy(markerMesh);
        }

        // ------------------------------------------------------------------
        // edge list (map_edge_list_build port)

        public void Rebuild(bool[] visited, ObjectSystem objects, int playerStartSeg, bool reactorAlive)
        {
            var edges = new Dictionary<(int, int), EdgeInfo>();

            for (int s = 0; s < level.Segments.Length; s++)
                if (visited[s])
                    AddSegmentEdges(edges, s, playerStartSeg, reactorAlive);
            for (int s = 0; s < level.Segments.Length; s++)
                if (!visited[s])
                    AddUnknownSegmentEdges(edges, s);

            // prune edges whose adjoining faces are nearly coplanar (mapedges.c:309)
            foreach (var e in edges.Values)
            {
                for (int i = 0; i < e.Faces.Count && (e.Flags & EdgeFlags.Defining) != 0; i++)
                    for (int j = 1; j < e.Faces.Count; j++)
                    {
                        if (i == j || e.Faces[i].seg == e.Faces[j].seg)
                            continue;
                        var n1 = world.Sides[e.Faces[i].seg][e.Faces[i].side].Normals[0];
                        var n2 = world.Sides[e.Faces[j].seg][e.Faces[j].side].Normals[0];
                        if (System.Numerics.Vector3.Dot(n1, n2) > 0.9f)
                        {
                            e.Flags &= ~EdgeFlags.Defining;
                            break;
                        }
                    }
            }

            // draw filter (draw_automap:722-770)
            var positions = new List<Vector3>();
            var colors = new List<Color32>();
            foreach (var e in edges.Values)
            {
                if ((e.Flags & EdgeFlags.Used) == 0)
                    continue;
                if ((e.Flags & EdgeFlags.Frontier) != 0 &&
                    (e.Flags & EdgeFlags.Secret) == 0 && e.Color == WallNormal)
                    continue; // wall against unexplored space: don't reveal it
                if ((e.Flags & (EdgeFlags.Defining | EdgeFlags.Grate)) == 0)
                    continue;
                positions.Add(ToUnity(level.Vertices[e.V0]));
                positions.Add(ToUnity(level.Vertices[e.V1]));
                var c = (Color32)e.Color;
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

            BuildMarkers(visited, objects);
        }

        void AddSegmentEdges(Dictionary<(int, int), EdgeInfo> edges, int segnum,
                             int playerStartSeg, bool reactorAlive)
        {
            var seg = level.Segments[segnum];
            var sides = world.Sides[segnum];

            for (int sn = 0; sn < 6; sn++)
            {
                bool hidden = false, noFade = false, haveColor = false;
                var color = WallNormal;

                if (seg.Children[sn] < 0)
                    haveColor = true; // solid side: normal wall color

                switch (seg.Function) // segment special coloring (mapedges.c:162)
                {
                    case 1: color = FuelcenColor; haveColor = true; break;
                    case 3: if (reactorAlive) { color = ReactorColor; haveColor = true; } break;
                    case 4: color = MatcenColor; haveColor = true; break;
                }

                int wallIdx = sides[sn].WallIndex;
                if (wallIdx >= 0)
                {
                    var wall = level.Walls[wallIdx];
                    switch (wall.Type)
                    {
                        case 2: // door
                            haveColor = true;
                            if ((wall.Keys & 2) != 0) { color = DoorBlue; noFade = true; }
                            else if ((wall.Keys & 8) != 0) { color = DoorGold; noFade = true; }
                            else if ((wall.Keys & 4) != 0) { color = DoorRed; noFade = true; }
                            else if (wall.ClipNum >= wclips.Length || wclips[wall.ClipNum] == null ||
                                     (wclips[wall.ClipNum].Flags & 8) == 0) // !WCF_HIDDEN
                            {
                                color = WallDoor;
                                // colored doors show their key color from both sides
                                int child = seg.Children[sn];
                                if (child >= 0)
                                {
                                    int cside = world.FindConnectSide(segnum, child);
                                    int cwall = cside >= 0 ? world.Sides[child][cside].WallIndex : -1;
                                    byte keys = cwall >= 0 ? level.Walls[cwall].Keys : (byte)0;
                                    if ((keys & 2) != 0) { color = DoorBlue; noFade = true; }
                                    else if ((keys & 8) != 0) { color = DoorGold; noFade = true; }
                                    else if ((keys & 4) != 0) { color = DoorRed; noFade = true; }
                                }
                            }
                            else
                            {
                                color = WallNormal; // secret door: looks like a plain wall
                                hidden = true;
                            }
                            break;
                        case 5: // closed wall
                            hidden = true;
                            color = WallNormal;
                            haveColor = true;
                            break;
                        case 1: // blastable (hostage doors)
                            color = WallDoor;
                            haveColor = true;
                            break;
                    }
                }

                if (segnum == playerStartSeg)
                {
                    color = PlayerStartColor;
                    haveColor = true;
                }

                if (!haveColor)
                    continue;

                var order = SegmentWorld.SideToVerts[sn];
                int v0 = seg.Verts[order[0]], v1 = seg.Verts[order[1]];
                int v2 = seg.Verts[order[2]], v3 = seg.Verts[order[3]];
                AddEdge(edges, v0, v1, color, segnum, sn, hidden, noFade);
                AddEdge(edges, v1, v2, color, segnum, sn, hidden, noFade);
                AddEdge(edges, v2, v3, color, segnum, sn, hidden, noFade);
                AddEdge(edges, v3, v0, color, segnum, sn, hidden, noFade);
            }
        }

        void AddUnknownSegmentEdges(Dictionary<(int, int), EdgeInfo> edges, int segnum)
        {
            var seg = level.Segments[segnum];
            for (int sn = 0; sn < 6; sn++)
            {
                if (seg.Children[sn] >= 0)
                    continue;
                var order = SegmentWorld.SideToVerts[sn];
                for (int k = 0; k < 4; k++)
                {
                    int va = seg.Verts[order[k]], vb = seg.Verts[order[(k + 1) & 3]];
                    if (va > vb) (va, vb) = (vb, va);
                    if (edges.TryGetValue((va, vb), out var e))
                        e.Flags |= EdgeFlags.Frontier;
                }
            }
        }

        void AddEdge(Dictionary<(int, int), EdgeInfo> edges, int va, int vb, Color color,
                     int segnum, int side, bool hidden, bool noFade)
        {
            if (va > vb) (va, vb) = (vb, va);
            if (!edges.TryGetValue((va, vb), out var e))
            {
                e = new EdgeInfo { V0 = va, V1 = vb, Color = color, Flags = EdgeFlags.Used | EdgeFlags.Defining };
                edges[(va, vb)] = e;
            }
            else if (color != WallNormal)
            {
                e.Color = color;
            }
            if (e.Faces.Count < 4)
                e.Faces.Add((segnum, side));
            if (hidden) e.Flags |= EdgeFlags.Secret;
            if (noFade) e.Flags |= EdgeFlags.NoFade;
        }

        // ------------------------------------------------------------------
        // markers: hostages always, key powerups in visited segments (automap.c:384)

        readonly List<Vector3> markerPos = new List<Vector3>();
        readonly List<Color32> markerCol = new List<Color32>();
        int staticMarkerCount;

        void BuildMarkers(bool[] visited, ObjectSystem objects)
        {
            markerPos.Clear();
            markerCol.Clear();
            if (objects != null)
            {
                foreach (var obj in objects.Objects)
                {
                    if (obj.Dead)
                        continue;
                    if (obj.Type == 3)
                        AddStar(ToUnity(obj.Pos), obj.Size, HostageColor);
                    else if (obj.Type == 7 && obj.Segnum >= 0 && obj.Segnum < visited.Length && visited[obj.Segnum])
                    {
                        if (obj.SubId == 4) AddStar(ToUnity(obj.Pos), 4f, KeyBlue);
                        else if (obj.SubId == 5) AddStar(ToUnity(obj.Pos), 4f, KeyRed);
                        else if (obj.SubId == 6) AddStar(ToUnity(obj.Pos), 4f, KeyGold);
                    }
                }
            }
            staticMarkerCount = markerPos.Count;
        }

        void AddStar(Vector3 center, float size, Color color)
        {
            var c = (Color32)color;
            void Line(Vector3 a, Vector3 b)
            {
                markerPos.Add(a); markerPos.Add(b);
                markerCol.Add(c); markerCol.Add(c);
            }
            Line(center - Vector3.right * size, center + Vector3.right * size);
            Line(center - Vector3.up * size, center + Vector3.up * size);
            Line(center - Vector3.forward * size, center + Vector3.forward * size);
        }

        /// <summary>Per-frame: orbit input, camera placement, and the ship arrow.</summary>
        public void UpdateView(Camera cam, Vector3 shipPos, Mat3 shipOrient)
        {
            yaw += Input.GetAxis("Mouse X") * 3f;
            pitch = Mathf.Clamp(pitch + Input.GetAxis("Mouse Y") * 3f, -85f, 85f);
            dist = Mathf.Clamp(dist * (1f - Input.GetAxis("Mouse ScrollWheel") * 0.5f), 25f, 2000f);

            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = shipPos - rot * Vector3.forward * dist;
            cam.transform.rotation = rot;

            // ship arrow (player marker)
            markerPos.RemoveRange(staticMarkerCount, markerPos.Count - staticMarkerCount);
            markerCol.RemoveRange(staticMarkerCount, markerCol.Count - staticMarkerCount);
            var fwd = new Vector3(shipOrient.Forward.X, shipOrient.Forward.Y, shipOrient.Forward.Z);
            var right = new Vector3(shipOrient.Right.X, shipOrient.Right.Y, shipOrient.Right.Z);
            var up = new Vector3(shipOrient.Up.X, shipOrient.Up.Y, shipOrient.Up.Z);
            var nose = shipPos + fwd * 8f;
            var tailL = shipPos - fwd * 5f - right * 4f;
            var tailR = shipPos - fwd * 5f + right * 4f;
            var fin = shipPos - fwd * 5f + up * 4f;
            var c = (Color32)ShipColor;
            void Line(Vector3 a, Vector3 b)
            {
                markerPos.Add(a); markerPos.Add(b);
                markerCol.Add(c); markerCol.Add(c);
            }
            Line(tailL, nose);
            Line(tailR, nose);
            Line(tailL, tailR);
            Line(fin, nose);

            markerMesh.Clear();
            markerMesh.SetVertices(markerPos);
            markerMesh.SetColors(markerCol);
            var indices = new int[markerPos.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;
            markerMesh.SetIndices(indices, MeshTopology.Lines, 0);
            markerMesh.RecalculateBounds();
        }

        static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
