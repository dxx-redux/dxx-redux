// D1U smoke test: verify LibDescent + D1U.Convert handle everything in the
// hogs directory (M0: parse; M1: decode textures, bake models, convert music).
// Usage: D1U.Smoke [hogsDir]  (default: walk up from cwd looking for d1/hogs/DESCENT.HOG)

using D1U.Convert;
using LibDescent.Data;
using LibDescent.Data.Midi;

string? FindHogsDir()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        var candidate = Path.Combine(dir.FullName, "d1", "hogs");
        if (File.Exists(Path.Combine(candidate, "DESCENT.HOG")))
            return candidate;
        dir = dir.Parent;
    }
    return null;
}

var hogsDir = args.Length > 0 ? args[0] : FindHogsDir();
if (hogsDir == null || !File.Exists(Path.Combine(hogsDir, "DESCENT.HOG")))
{
    Console.Error.WriteLine("FAIL: hogs dir with DESCENT.HOG not found; pass it as the first argument.");
    return 1;
}
Console.WriteLine($"hogs dir: {hogsDir}");
int errors = 0;

// --- 1. base archives (DESCENT.HOG + DESCENT.PIG + palette) ---
var archives = BaseArchives.Load(hogsDir);
var hog = archives.Hog;
var pig = archives.Pig;
Console.WriteLine($"\nDESCENT.HOG: {hog.NumLumps} lumps");
foreach (var group in hog.Lumps
             .GroupBy(l => Path.GetExtension(l.Name).ToLowerInvariant())
             .OrderByDescending(g => g.Count()))
    Console.WriteLine($"  {group.Key,-6} x{group.Count()}");
Console.WriteLine($"\nDESCENT.PIG: {pig.Bitmaps.Count} bitmaps, {pig.Sounds.Count} sounds");
Console.WriteLine($"  gamedata: textures={pig.numTextures} vclips={pig.numVClips} eclips={pig.numEClips} " +
                  $"wclips={pig.numWClips} robots={pig.numRobots} joints={pig.numJoints} " +
                  $"weapons={pig.numWeapons} models={pig.numModels}");
if (pig.Bitmaps.Count < 1000 || pig.numRobots <= 0 || pig.numModels <= 0)
{
    Console.Error.WriteLine("FAIL: PIG gamedata looks empty — wrong pig version or parse failure.");
    errors++;
}

// --- 2b. decode every bitmap to RGBA (M1) ---
int decodeFailures = 0;
long rgbaBytes = 0;
foreach (var bitmap in pig.Bitmaps)
{
    try
    {
        rgbaBytes += TextureDecoder.ToRgba32(bitmap, archives.Palette).Length;
    }
    catch (Exception e)
    {
        if (decodeFailures++ < 5)
            Console.Error.WriteLine($"  bitmap '{bitmap.Name}' FAIL: {e.Message}");
    }
}
Console.WriteLine($"  textures decoded: {pig.Bitmaps.Count - decodeFailures}/{pig.Bitmaps.Count} " +
                  $"({rgbaBytes / (1024 * 1024)} MB RGBA)");
errors += decodeFailures;

// --- 2c. bake every model to triangle lists (M1) ---
int bakeFailures = 0, totalTriangles = 0, totalSubmodels = 0, badTextureSlots = 0;
for (int m = 0; m < pig.numModels; m++)
{
    try
    {
        var baked = ModelBaker.Bake(pig.Models[m]);
        totalTriangles += baked.TriangleCount;
        totalSubmodels += baked.Submodels.Count;
        foreach (var sub in baked.Submodels)
            foreach (var g in sub.Groups)
                if (g.TextureSlot >= 0)
                {
                    int bmp = ModelBaker.ResolveTextureSlot(pig, pig.Models[m], g.TextureSlot);
                    if (bmp <= 0 || bmp >= pig.Bitmaps.Count)
                        badTextureSlots++;
                }
    }
    catch (Exception e)
    {
        if (bakeFailures++ < 5)
            Console.Error.WriteLine($"  model {m} FAIL: {e.GetType().Name}: {e.Message}");
    }
}
Console.WriteLine($"  models baked: {pig.numModels - bakeFailures}/{pig.numModels}, " +
                  $"{totalSubmodels} submodels, {totalTriangles} triangles, bad texture slots={badTextureSlots}");
errors += bakeFailures + (badTextureSlots > 0 ? 1 : 0);

// --- 2d. HMP music -> standard MIDI (M1) ---
try
{
    var hmpData = BaseArchives.GetLumpData(hog, "descent.hmp");
    if (hmpData == null)
    {
        Console.Error.WriteLine("  descent.hmp not found in hog");
        errors++;
    }
    else
    {
        var midi = MIDISequence.LoadHMP(hmpData);
        midi.Convert(MIDIFormat.Type1); // HMI -> standard MIDI type 1
        var midiBytes = midi.Write();
        bool headerOk = midiBytes.Length > 4 && midiBytes[0] == 'M' && midiBytes[1] == 'T'
                                             && midiBytes[2] == 'h' && midiBytes[3] == 'd';
        Console.WriteLine($"  descent.hmp -> MIDI: {midiBytes.Length} bytes, header {(headerOk ? "MThd OK" : "BAD")}");
        if (!headerOk)
            errors++;
    }
}
catch (Exception e)
{
    Console.Error.WriteLine($"  HMP conversion FAIL: {e.GetType().Name}: {e.Message}");
    errors++;
}

// --- helper: parse one RDL lump ---
D1Level? ParseLevel(HOGFile source, string lumpName, ref int errorCount)
{
    var lump = source.Lumps.FirstOrDefault(
        l => string.Equals(l.Name, lumpName, StringComparison.OrdinalIgnoreCase));
    if (lump == null)
    {
        Console.Error.WriteLine($"  {lumpName,-14} FAIL: lump not found");
        errorCount++;
        return null;
    }
    try
    {
        using var ms = new MemoryStream(source.GetLumpData(lump));
        var level = D1Level.CreateFromStream(ms);
        Console.WriteLine($"  {lumpName,-14} segs={level.Segments.Count,4} verts={level.Vertices.Count,5} " +
                          $"objs={level.Objects.Count,4} walls={level.Walls.Count,3} " +
                          $"trig={level.Triggers.Count,3} matcen={level.MatCenters.Count}");
        return level;
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"  {lumpName,-14} FAIL: {e.GetType().Name}: {e.Message}");
        errorCount++;
        return null;
    }
}

// --- 3. built-in First Strike levels ---
var builtinLevels = hog.Lumps
    .Where(l => l.Name.EndsWith(".rdl", StringComparison.OrdinalIgnoreCase))
    .Select(l => l.Name).ToList();
Console.WriteLine($"\nbuilt-in levels ({builtinLevels.Count}):");
foreach (var name in builtinLevels)
    ParseLevel(hog, name, ref errors);

// --- 4. add-on missions (*.msn + *.hog) ---
foreach (var msnPath in Directory.GetFiles(hogsDir, "*.msn").OrderBy(p => p))
{
    try
    {
        var mission = MissionFile.Load(msnPath);
        var missionHogPath = Path.ChangeExtension(msnPath, ".hog");
        if (!File.Exists(missionHogPath))
        {
            Console.Error.WriteLine($"\nmission {Path.GetFileName(msnPath)} FAIL: no matching hog");
            errors++;
            continue;
        }
        var missionHog = new HOGFile(missionHogPath);
        Console.WriteLine($"\nmission {Path.GetFileName(msnPath)}: \"{mission.Name}\" " +
                          $"levels={mission.Levels.Count} secret={mission.SecretLevels.Count} " +
                          $"(hog: {missionHog.NumLumps} lumps)");
        foreach (var levelName in mission.Levels)
            ParseLevel(missionHog, levelName, ref errors);
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"\nmission {Path.GetFileName(msnPath)} FAIL: {e.GetType().Name}: {e.Message}");
        errors++;
    }
}

// --- 5. DXU cache: build, cache-hit, reload roundtrip (M1) ---
Console.WriteLine("\nDXU cache:");
var cacheDir = Path.Combine(Path.GetTempPath(), "d1u-smoke-cache");
if (Directory.Exists(cacheDir))
    Directory.Delete(cacheDir, recursive: true);
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
var dxuPath = D1U.Convert.DxuCache.EnsureBase(hogsDir, cacheDir, s => Console.WriteLine("  " + s));
Console.WriteLine($"  cold build: {stopwatch.ElapsedMilliseconds} ms");
stopwatch.Restart();
D1U.Convert.DxuCache.EnsureBase(hogsDir, cacheDir, s => Console.WriteLine("  " + s));
Console.WriteLine($"  warm ensure: {stopwatch.ElapsedMilliseconds} ms");

stopwatch.Restart();
var baseDxu = D1U.Convert.BaseDxu.Read(dxuPath, out var dxuHeader);
Console.WriteLine($"  reload: {stopwatch.ElapsedMilliseconds} ms -> {baseDxu.Bitmaps.Count} bitmaps, " +
                  $"{baseDxu.Sounds.Count} sounds, {baseDxu.Models.Count} models, " +
                  $"{baseDxu.Songs.Count} songs (order list {baseDxu.SongOrder.Count})");
bool roundtripOk =
    baseDxu.Bitmaps.Count == pig.Bitmaps.Count &&
    baseDxu.Sounds.Count == pig.Sounds.Count &&
    baseDxu.Models.Count == pig.numModels &&
    baseDxu.Songs.Count > 20 &&
    baseDxu.PaletteRaw.Length == 9472 &&
    baseDxu.Bitmaps[1].Indexed.SequenceEqual(pig.Bitmaps[1].Data);
Console.WriteLine($"  roundtrip {(roundtripOk ? "OK" : "MISMATCH")}");
if (!roundtripOk)
    errors++;

// --- 6. gameplay tables -> JSON (debug/modding dump) ---
try
{
    D1U.Smoke.TableJson.DumpAll(pig, cacheDir);
    var jsonFiles = Directory.GetFiles(cacheDir, "*.json").Select(Path.GetFileName).ToArray();
    Console.WriteLine($"  tables JSON: {string.Join(", ", jsonFiles)} -> {cacheDir}");
    if (jsonFiles.Length < 4)
        errors++;
}
catch (Exception e)
{
    Console.Error.WriteLine($"  tables JSON FAIL: {e.GetType().Name}: {e.Message}");
    errors++;
}

// --- 7. bake every level to meshes (M2) ---
// face census over everything EmitSide emits (statics + wall pieces): every
// face must front its own segment's interior (the level materials cull Back,
// LevelViewer.BuildChunk), and coincident faces of different segments must
// front opposite ways — a same-facing pair z-fights under any culling.
(int badFacing, int sameFacing, int opposite) FaceCensus(BakedLevel lvl)
{
    var wallAt = new Dictionary<(int seg, int side), WallRecord>();
    foreach (var w in lvl.Walls)
        wallAt[(w.SegmentIndex, w.SideIndex)] = w;
    int bad = 0, same = 0, opp = 0;
    var byCenter = new Dictionary<(long, long, long), List<System.Numerics.Vector3>>();
    for (int s = 0; s < lvl.Segments.Length; s++)
    {
        var seg = lvl.Segments[s];
        var segCenter = System.Numerics.Vector3.Zero;
        for (int v = 0; v < 8; v++)
            segCenter += lvl.Vertices[seg.Verts[v]];
        segCenter /= 8f;
        for (int sideNum = 0; sideNum < 6; sideNum++)
        {
            // mirror EmitSide: walls render unless WALL_OPEN; plain sides only when solid
            bool emitted = wallAt.TryGetValue((s, sideNum), out var wall)
                ? wall.Type != 4
                : seg.Children[sideNum] == -1;
            if (!emitted)
                continue;
            var sv = D1U.Game.SegmentWorld.SideToVerts[sideNum];
            var c0 = lvl.Vertices[seg.Verts[sv[0]]];
            var c1 = lvl.Vertices[seg.Verts[sv[1]]];
            var c2 = lvl.Vertices[seg.Verts[sv[2]]];
            var c3 = lvl.Vertices[seg.Verts[sv[3]]];
            var n = System.Numerics.Vector3.Cross(c2 - c0, c3 - c1); // covers both diagonals
            if (n.LengthSquared() < 1e-8f)
                continue; // degenerate sliver
            var faceCenter = (c0 + c1 + c2 + c3) / 4f;
            if (System.Numerics.Vector3.Dot(n, segCenter - faceCenter) < 0f)
                bad++;
            var q = faceCenter * 64f;
            var key = ((long)MathF.Round(q.X), (long)MathF.Round(q.Y), (long)MathF.Round(q.Z));
            if (!byCenter.TryGetValue(key, out var list))
                byCenter[key] = list = new List<System.Numerics.Vector3>();
            foreach (var other in list)
            {
                if (System.Numerics.Vector3.Cross(n, other).LengthSquared() >
                    1e-6f * n.LengthSquared() * other.LengthSquared())
                    continue; // shares a center but not a plane
                if (System.Numerics.Vector3.Dot(n, other) > 0f) same++; else opp++;
            }
            list.Add(n);
        }
    }
    return (bad, same, opp);
}

Console.WriteLine("\nlevel baking:");
var missionList = MissionScanner.Scan(hogsDir);
int levelBakeFailures = 0, levelsBaked = 0;
int censusBadFacing = 0, censusSameFacing = 0, censusOpposite = 0;
foreach (var mission in missionList)
{
    var missionHog = new HOGFile(mission.HogPath);
    long tris = 0, chunks = 0, wallPieces = 0, objects = 0;
    foreach (var levelName in mission.LevelNames)
    {
        try
        {
            var lump = missionHog.Lumps.First(
                l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));
            using var ms = new MemoryStream(missionHog.GetLumpData(lump));
            var bakedLevel = LevelBaker.Bake(D1Level.CreateFromStream(ms), pig);
            tris += bakedLevel.StaticTriangleCount;
            chunks += bakedLevel.StaticChunks.Count;
            wallPieces += bakedLevel.DoorPieces.Count;
            objects += bakedLevel.Objects.Count;
            levelsBaked++;
            var (cBad, cSame, cOpp) = FaceCensus(bakedLevel);
            censusBadFacing += cBad;
            censusSameFacing += cSame;
            censusOpposite += cOpp;
        }
        catch (Exception e)
        {
            levelBakeFailures++;
            Console.Error.WriteLine($"  {mission.CacheKey}/{levelName} FAIL: {e.GetType().Name}: {e.Message}");
        }
    }
    Console.WriteLine($"  {mission.CacheKey,-12} levels={mission.LevelNames.Count,2} tris={tris,6} " +
                      $"chunks={chunks,4} wallPieces={wallPieces,4} objects={objects,5}");
}
Console.WriteLine($"  baked {levelsBaked} levels, {levelBakeFailures} failure(s)");
Console.WriteLine($"  face census: wrong-facing={censusBadFacing}, coincident same-facing pairs={censusSameFacing}, " +
                  $"opposite-facing pairs={censusOpposite} (separated by Cull Back)");
errors += levelBakeFailures;
if (censusBadFacing > 0 || censusSameFacing > 0)
    errors++;

// --- 8. mission DXU build + cache hit + reload (M2) ---
Console.WriteLine("\nmission DXU:");
foreach (var key in new[] { "firststrike", "chaos" })
{
    var mission = missionList.FirstOrDefault(m => m.CacheKey == key);
    if (mission == null)
    {
        Console.Error.WriteLine($"  mission '{key}' not found");
        errors++;
        continue;
    }
    var missionPath = MissionDxu.EnsureMission(hogsDir, mission, cacheDir, s => Console.WriteLine("  " + s));
    MissionDxu.EnsureMission(hogsDir, mission, cacheDir, s => Console.WriteLine("  " + s)); // expect cache hit
    var (missionName, missionLevelNames, missionLevels) = MissionDxu.Read(missionPath, out _);
    bool missionOk = missionLevels.Count == mission.LevelNames.Count &&
                     missionLevelNames.Count == missionLevels.Count &&
                     missionLevels.All(l => l.Segments.Length > 0 && l.StaticChunks.Count > 0);
    Console.WriteLine($"  {key}: \"{missionName}\" reload {missionLevels.Count} level(s), " +
                      $"{new FileInfo(missionPath).Length / 1024} KB {(missionOk ? "OK" : "MISMATCH")}");
    if (!missionOk)
        errors++;
}

// --- 9. flight physics + fvi sanity (M3) ---
Console.WriteLine("\nship physics (M3):");
try
{
    var fsMission = missionList.First(m => m.CacheKey == "firststrike");
    var fsPath = D1U.Convert.MissionDxu.EnsureMission(hogsDir, fsMission, cacheDir, null);
    var (_, _, fsLevels) = D1U.Convert.MissionDxu.Read(fsPath, out _);
    var level1 = fsLevels[0];

    var world = new D1U.Game.SegmentWorld(level1);
    var start = level1.Objects.First(o => o.Type == 4); // ObjectType.Player
    var startOrient = new D1U.Game.Mat3
    {
        Right = new System.Numerics.Vector3(start.Orientation[0], start.Orientation[1], start.Orientation[2]),
        Up = new System.Numerics.Vector3(start.Orientation[3], start.Orientation[4], start.Orientation[5]),
        Forward = new System.Numerics.Vector3(start.Orientation[6], start.Orientation[7], start.Orientation[8]),
    };

    // fvi ray checks from the player start
    var fvi = new D1U.Game.Fvi(world);
    var fviInfo = new D1U.Game.FviInfo();
    var fwdQuery = new D1U.Game.FviQuery
    {
        P0 = start.Position,
        P1 = start.Position + startOrient.Forward * 2000f,
        StartSeg = start.Segnum,
        Rad = 0f,
    };
    var fwdHit = fvi.FindVectorIntersection(fwdQuery, fviInfo);
    float fwdDist = System.Numerics.Vector3.Distance(start.Position, fviInfo.HitPoint);
    Console.WriteLine($"  forward ray: {fwdHit} at {fwdDist:F1} units (segs traversed: {fviInfo.NSegs})");
    if (fwdHit != D1U.Game.FviHit.Wall || fwdDist <= 1f || fwdDist >= 2000f)
        errors++;

    var downQuery = new D1U.Game.FviQuery
    {
        P0 = start.Position,
        P1 = start.Position - startOrient.Up * 500f,
        StartSeg = start.Segnum,
        Rad = 2f,
    };
    var downHit = fvi.FindVectorIntersection(downQuery, fviInfo);
    float downDist = System.Numerics.Vector3.Distance(start.Position, fviInfo.HitPoint);
    Console.WriteLine($"  down sweep (r=2): {downHit} at {downDist:F1} units");
    if (downHit != D1U.Game.FviHit.Wall || downDist >= 500f)
        errors++;

    // 10 seconds of full-forward flight at 60 Hz
    var shipParams = new D1U.Game.ShipParams
    {
        Mass = (float)(double)pig.PlayerShip.Mass,
        Drag = (float)(double)pig.PlayerShip.Drag,
        MaxThrust = (float)(double)pig.PlayerShip.MaxThrust,
        MaxRotThrust = (float)(double)pig.PlayerShip.MaxRotationThrust,
        Wiggle = (float)(double)pig.PlayerShip.Wiggle,
        Size = start.Size,
    };
    Console.WriteLine($"  ship: mass={shipParams.Mass:F2} drag={shipParams.Drag:F4} maxThrust={shipParams.MaxThrust:F2} " +
                      $"maxRotThrust={shipParams.MaxRotThrust:F2} size={shipParams.Size:F2}");

    var sim = new D1U.Game.ShipSim(world);
    var state = new D1U.Game.ShipState { Pos = start.Position, Orient = startOrient, Segnum = start.Segnum };
    const float step = 1f / 60f;
    double gameTime = 0;
    float maxSpeed = 0f;
    for (int i = 0; i < 600; i++)
    {
        var controls = new D1U.Game.ShipControls { ForwardTime = step };
        gameTime += step;
        sim.Step(state, shipParams, controls, step, gameTime);
        maxSpeed = Math.Max(maxSpeed, state.Vel.Length());
    }
    float traveled = System.Numerics.Vector3.Distance(start.Position, state.Pos);
    bool inMine = state.Segnum >= 0 && world.GetSegMasks(state.Pos, state.Segnum, 0f).CenterMask == 0;
    // equilibrium of v' = (v + thrust/mass) * (1 - drag) per 64 Hz tick
    float expectedTopSpeed = shipParams.MaxThrust / shipParams.Mass * (1f - shipParams.Drag) / shipParams.Drag;
    Console.WriteLine($"  10s full thrust: traveled {traveled:F1} units, max speed {maxSpeed:F1} " +
                      $"(analytic top {expectedTopSpeed:F1}), final segment {state.Segnum}, in-mine={inMine}");
    if (traveled < 20f || !inMine || maxSpeed < expectedTopSpeed * 0.5f || maxSpeed > expectedTopSpeed * 1.5f)
        errors++;

    // --- 9b. rotation composition vs do_physics_sim_rot (physics.c:349-385) ---
    // vm_matrix_x_matrix (vecmat.c:731-748) transcribed literally: dest = src1 x
    // src0, so the delta matrix (src1) applies in src0's own row basis.
    static float Dot3(float x, float y, float z, System.Numerics.Vector3 v)
        => x * v.X + y * v.Y + z * v.Z;
    static D1U.Game.Mat3 CMatXMat(D1U.Game.Mat3 s0, D1U.Game.Mat3 s1) => new D1U.Game.Mat3
    {
        Right = new System.Numerics.Vector3(
            Dot3(s0.Right.X, s0.Up.X, s0.Forward.X, s1.Right),
            Dot3(s0.Right.Y, s0.Up.Y, s0.Forward.Y, s1.Right),
            Dot3(s0.Right.Z, s0.Up.Z, s0.Forward.Z, s1.Right)),
        Up = new System.Numerics.Vector3(
            Dot3(s0.Right.X, s0.Up.X, s0.Forward.X, s1.Up),
            Dot3(s0.Right.Y, s0.Up.Y, s0.Forward.Y, s1.Up),
            Dot3(s0.Right.Z, s0.Up.Z, s0.Forward.Z, s1.Up)),
        Forward = new System.Numerics.Vector3(
            Dot3(s0.Right.X, s0.Up.X, s0.Forward.X, s1.Forward),
            Dot3(s0.Right.Y, s0.Up.Y, s0.Forward.Y, s1.Forward),
            Dot3(s0.Right.Z, s0.Up.Z, s0.Forward.Z, s1.Forward)),
    };
    // hand copy of physics.c:195-385 (ORIGINAL drag strategy, PF_TURNROLL)
    static void RefSimRot(ref D1U.Game.Mat3 orient, ref System.Numerics.Vector3 rotVel,
                          ref float turnRoll, System.Numerics.Vector3 rotThrust,
                          float mass, float drag, float ft)
    {
        const float FT = 1f / 64f;                       // physics.c:191
        int count = (int)(ft / FT);
        float k = (ft - count * FT) / FT;
        float rotDrag = drag * 5f / 2f;                  // physics.c:218
        var accel = rotThrust * (1f / mass);
        while (count-- > 0) { rotVel += accel; rotVel *= 1f - rotDrag; } // physics.c:231-237
        rotVel += accel * k;                             // physics.c:240-243
        rotVel *= 1f - k * rotDrag;

        if (turnRoll != 0f)                              // physics.c:352-360
            orient = CMatXMat(orient, D1U.Game.Mat3.FromAngles(0f, -turnRoll, 0f));
        orient = CMatXMat(orient, D1U.Game.Mat3.FromAngles( // physics.c:362-368
            rotVel.X * ft, rotVel.Z * ft, rotVel.Y * ft));
        float desired = -rotVel.Y * ((0x4ec4 / 2) / 65536f); // set_object_turnroll
        if (turnRoll != desired)
        {
            float maxRoll = 0x2000 / 65536f * ft;        // ROLL_RATE
            float delta = desired - turnRoll;
            if (Math.Abs(delta) < maxRoll) maxRoll = delta;
            else if (delta < 0f) maxRoll = -maxRoll;
            turnRoll += maxRoll;
        }
        if (turnRoll != 0f)                              // physics.c:374-382
            orient = CMatXMat(orient, D1U.Game.Mat3.FromAngles(0f, turnRoll, 0f));
        orient.Orthonormalize();                         // check_and_fix_matrix
    }
    static float MatDiff(D1U.Game.Mat3 a, D1U.Game.Mat3 b)
    {
        var dr = a.Right - b.Right;
        var du = a.Up - b.Up;
        var df = a.Forward - b.Forward;
        return MathF.Max(MathF.Max(dr.Length(), du.Length()), df.Length());
    }

    var rotParams = shipParams;
    rotParams.Wiggle = 0f; // wiggle would add Vel and pull fvi into a pure-rotation test
    (D1U.Game.Mat3 orient, float turnRoll) RunRot(D1U.Game.Mat3 o0, float pitch, float heading, int steps)
    {
        var rotSim = new D1U.Game.ShipSim(world);
        var rs = new D1U.Game.ShipState { Pos = start.Position, Orient = o0, Segnum = start.Segnum };
        for (int i = 0; i < steps; i++)
            rotSim.Step(rs, rotParams,
                new D1U.Game.ShipControls { PitchTime = pitch * step, HeadingTime = heading * step },
                step, i * step);
        return (rs.Orient, rs.TurnRoll);
    }
    (D1U.Game.Mat3 orient, float turnRoll) RunRotRef(D1U.Game.Mat3 o0, float pitch, float heading, int steps)
    {
        var o = o0;
        var rv = System.Numerics.Vector3.Zero;
        float tr = 0f;
        for (int i = 0; i < steps; i++)
        {
            // read_flying_controls: rotthrust = time * max_rotthrust / frametime
            var rt = new System.Numerics.Vector3(pitch * step, heading * step, 0f)
                     * (rotParams.MaxRotThrust / step);
            RefSimRot(ref o, ref rv, ref tr, rt, rotParams.Mass, rotParams.Drag, step);
        }
        return (o, tr);
    }

    // full pitch at heading 90: must rotate about the ship's OWN right vector
    var h90 = D1U.Game.Mat3.FromAngles(0f, 0f, 0.25f);
    var (pitched, _) = RunRot(h90, 1f, 0f, 12);
    var (pitchedRef, _) = RunRotRef(h90, 1f, 0f, 12);
    float pitchRefDiff = MatDiff(pitched, pitchedRef);
    float rightLock = System.Numerics.Vector3.Dot(pitched.Right, h90.Right);
    float noseDip = System.Numerics.Vector3.Dot(pitched.Forward, h90.Up); // +pitch = nose to -up

    // full yaw at pitch 90 (nose straight down): must swing toward body right
    var p90 = D1U.Game.Mat3.FromAngles(0.25f, 0f, 0f);
    var (yawed, yawRoll) = RunRot(p90, 0f, 1f, 12);
    var (yawedRef, yawRollRef) = RunRotRef(p90, 0f, 1f, 12);
    float yawRefDiff = MatDiff(yawed, yawedRef);
    float yawSwing = System.Numerics.Vector3.Dot(yawed.Forward, p90.Right);

    // steady level yaw banks INTO the turn (negative turnroll for +heading)
    var (_, bankRoll) = RunRot(D1U.Game.Mat3.Identity, 0f, 1f, 12);

    Console.WriteLine($"  rotation vs physics.c: pitch@h90 |dRef|={pitchRefDiff:E1} rightLock={rightLock:F4} " +
                      $"noseDip={noseDip:F3}; yaw@p90 |dRef|={yawRefDiff:E1} swing={yawSwing:F3} " +
                      $"turnroll={yawRoll:F4} (ref {yawRollRef:F4}); level-turn bank={bankRoll:F4}");
    if (pitchRefDiff > 1e-4f || rightLock < 0.999f || noseDip > -0.05f ||
        yawRefDiff > 1e-4f || yawSwing < 0.02f ||
        Math.Abs(yawRoll - yawRollRef) > 1e-4f || bankRoll >= 0f)
        errors++;
}
catch (Exception e)
{
    Console.Error.WriteLine($"  M3 FAIL: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
    errors++;
}

// --- 10. level runtime: doors + triggers + reactor (M4) ---
Console.WriteLine("\nlevel runtime (M4):");
try
{
    var fsMission2 = missionList.First(m => m.CacheKey == "firststrike");
    var fsPath2 = D1U.Convert.MissionDxu.EnsureMission(hogsDir, fsMission2, cacheDir, null);
    var (_, _, fsLevels2) = D1U.Convert.MissionDxu.Read(fsPath2, out _);
    var lvl = fsLevels2[0];
    var world2 = new D1U.Game.SegmentWorld(lvl);

    var clips = new D1U.Game.WallClipInfo[pig.WClips.Length];
    for (int i = 0; i < clips.Length; i++)
        clips[i] = new D1U.Game.WallClipInfo
        {
            PlayTime = pig.WClips[i] != null ? (float)(double)pig.WClips[i].PlayTime : 1f,
            NumFrames = pig.WClips[i] != null ? pig.WClips[i].NumFrames : 1,
            Tmap1 = pig.WClips[i] != null && (pig.WClips[i].Flags & 4) != 0,
        };
    var runtime = new D1U.Game.LevelRuntime(world2, clips);
    int frameEvents = 0;
    runtime.WallFrameChanged += (w, f, t) => frameEvents++;

    int doorWall = lvl.Walls.FindIndex(w =>
        w.Type == 2 && w.Keys <= 1 && (w.Flags & 8) == 0 && (w.Flags & 2) == 0);
    if (doorWall < 0)
    {
        Console.Error.WriteLine("  no plain door found in level01");
        errors++;
    }
    else
    {
        var doorRecord = lvl.Walls[doorWall];
        bool passableBefore = world2.WallPassable[doorWall];
        runtime.BumpWall(doorRecord.SegmentIndex, doorRecord.SideIndex);
        for (int i = 0; i < 60; i++)
            runtime.Tick(1f / 30f, -1, default, 0f); // 2 s: door fully opens
        bool openAfter = world2.WallPassable[doorWall];
        for (int i = 0; i < 360; i++)
            runtime.Tick(1f / 30f, -1, default, 0f); // 12 s: auto doors close again
        bool isAuto = (doorRecord.Flags & 16) != 0;
        bool closedAgain = !world2.WallPassable[doorWall];
        Console.WriteLine($"  door wall {doorWall} (clip {doorRecord.ClipNum}, auto={isAuto}): " +
                          $"before={passableBefore} open={openAfter} " +
                          $"auto-closed={(isAuto ? closedAgain.ToString() : "n/a")} frameEvents={frameEvents}");
        if (passableBefore || !openAfter || (isAuto && !closedAgain) || frameEvents == 0)
            errors++;
    }

    int exitTriggers = lvl.Triggers.Count(t => (t.Flags & 8) != 0);
    Console.WriteLine($"  triggers: {lvl.Triggers.Count} total, exit={exitTriggers}, " +
                      $"reactor targets={lvl.ReactorTargets.Count}");
    if (exitTriggers < 1 || lvl.ReactorTargets.Count < 1)
        errors++;

    int exitTriggerIndex = lvl.Triggers.FindIndex(t => (t.Flags & 8) != 0);
    int exitWall = lvl.Walls.FindIndex(w => w.TriggerIndex == exitTriggerIndex);
    if (exitWall >= 0)
        runtime.CrossedSide(lvl.Walls[exitWall].SegmentIndex, lvl.Walls[exitWall].SideIndex);
    Console.WriteLine($"  exit crossing -> ExitReached={runtime.Player.ExitReached}");
    if (!runtime.Player.ExitReached)
        errors++;

    runtime.DestroyReactor();
    Console.WriteLine($"  reactor destroyed -> {lvl.ReactorTargets.Count} target wall(s) toggled");

    // --- 11. object visuals census ---
    int modelObjects = 0, spriteObjects = 0, noneObjects = 0, badVisuals = 0;
    bool reactorResolved = false;
    foreach (var obj in lvl.Objects)
    {
        var visual = D1U.Convert.ObjectVisuals.Resolve(pig, obj);
        switch (visual.Kind)
        {
            case D1U.Convert.ObjectVisualKind.Model:
                modelObjects++;
                if (visual.ModelNum < 0 || visual.ModelNum >= pig.numModels)
                    badVisuals++;
                if (obj.Type == 9)
                    reactorResolved = true;
                break;
            case D1U.Convert.ObjectVisualKind.Sprite:
                spriteObjects++;
                if (visual.VClipNum < 0 || visual.VClipNum >= pig.VClips.Length ||
                    pig.VClips[visual.VClipNum] == null || pig.VClips[visual.VClipNum].NumFrames <= 0)
                    badVisuals++;
                break;
            default:
                noneObjects++;
                break;
        }
    }
    Console.WriteLine($"  object visuals: {modelObjects} models, {spriteObjects} sprites, {noneObjects} none, " +
                      $"bad={badVisuals}, reactor resolved={reactorResolved}");
    if (badVisuals > 0 || !reactorResolved || modelObjects < 10 || spriteObjects < 5)
        errors++;
}
catch (Exception e)
{
    Console.Error.WriteLine($"  M4 FAIL: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
    errors++;
}

// --- 12. combat: weapons hit robots, reactor destruction opens exit (M5) ---
Console.WriteLine("\ncombat (M5 phase 1):");
try
{
    var fsMission3 = missionList.First(m => m.CacheKey == "firststrike");
    var fsPath3 = D1U.Convert.MissionDxu.EnsureMission(hogsDir, fsMission3, cacheDir, null);
    var (_, _, fsLevels3) = D1U.Convert.MissionDxu.Read(fsPath3, out _);
    var lvl3 = fsLevels3[0];
    var world3 = new D1U.Game.SegmentWorld(lvl3);

    var clips3 = new D1U.Game.WallClipInfo[pig.WClips.Length];
    for (int i = 0; i < clips3.Length; i++)
        clips3[i] = new D1U.Game.WallClipInfo
        {
            PlayTime = pig.WClips[i] != null ? (float)(double)pig.WClips[i].PlayTime : 1f,
            NumFrames = pig.WClips[i] != null ? pig.WClips[i].NumFrames : 1,
            Tmap1 = pig.WClips[i] != null && (pig.WClips[i].Flags & 4) != 0,
        };
    var runtime3 = new D1U.Game.LevelRuntime(world3, clips3);

    static float[] DiffArray(LibDescent.Data.Fix[] source)
    {
        var result = new float[source.Length];
        for (int k = 0; k < source.Length; k++)
            result[k] = (float)(double)source[k];
        return result;
    }

    var robotStats = new D1U.Game.RobotStats[pig.numRobots];
    for (int i = 0; i < pig.numRobots; i++)
    {
        var r = pig.Robots[i];
        var gunPoints = new System.Numerics.Vector3[8];
        for (int g = 0; g < 8; g++)
            gunPoints[g] = new System.Numerics.Vector3(
                (float)(double)r.GunPoints[g].X, (float)(double)r.GunPoints[g].Y, (float)(double)r.GunPoints[g].Z);
        robotStats[i] = new D1U.Game.RobotStats
        {
            Strength = (float)(double)r.Strength,
            ModelNum = r.ModelNum,
            DeathVClip = r.DeathVClipNum,
            DeathSound = r.DeathSoundNum,
            WeaponType = r.WeaponType,
            NumGuns = r.NumGuns,
            GunPoints = gunPoints,
            FieldOfView = DiffArray(r.FieldOfView),
            FiringWait = DiffArray(r.FiringWait),
            TurnTime = DiffArray(r.TurnTime),
            MaxSpeed = DiffArray(r.MaxSpeed),
            CircleDistance = DiffArray(r.CircleDistance),
            RapidfireCount = (sbyte[])r.RapidfireCount.Clone(),
            AttackType = (int)r.AttackType != 0,
            IsBoss = (int)r.BossFlag != 0,
            Score = r.ScoreValue,
            SeeSound = r.SeeSound,
            AttackSound = r.AttackSound,
            ClawSound = r.ClawSound,
            ContainsType = r.ContainsType,
            ContainsId = r.ContainsID,
            ContainsCount = r.ContainsCount,
            ContainsProb = r.ContainsProbability,
        };
    }

    var allWeaponStats = new D1U.Game.WeaponStats[pig.numWeapons];
    for (int i = 0; i < pig.numWeapons; i++)
    {
        var w = pig.Weapons[i];
        allWeaponStats[i] = new D1U.Game.WeaponStats
        {
            Speed = (float)(double)w.Speed[D1U.Game.ObjectSystem.Difficulty],
            Strength = (float)(double)w.Strength[D1U.Game.ObjectSystem.Difficulty],
            Lifetime = (float)(double)w.Lifetime,
            FireWait = (float)(double)w.FireWait,
            DamageRadius = (float)(double)w.DamageRadius,
            Homing = w.HomingFlag,
            FiringSound = w.FiringSound,
            WallHitVClip = w.WallHitVClip,
            WallHitSound = w.WallHitSound,
        };
    }

    var objs = new D1U.Game.ObjectSystem(world3,
        record => { var v = D1U.Convert.ObjectVisuals.Resolve(pig, record); return (v.ModelNum, v.VClipNum); },
        robotStats, 200f) { Runtime = runtime3 };
    objs.SetWeaponTable(allWeaponStats);

    var startObj = lvl3.Objects.First(o => o.Type == 4);
    var robots = objs.Objects.Where(o => o.Type == 2).ToList();
    Console.WriteLine($"  objects: {objs.Objects.Count} total, {objs.RobotsAlive} robots alive");

    // laser 1 stats at Hotshot
    var laserInfo = pig.Weapons[0];
    var laser = new D1U.Game.WeaponStats
    {
        Speed = (float)(double)laserInfo.Speed[D1U.Game.ObjectSystem.Difficulty],
        Strength = (float)(double)laserInfo.Strength[D1U.Game.ObjectSystem.Difficulty],
        Lifetime = (float)(double)laserInfo.Lifetime,
    };

    // shoot a robot from 12 units away inside its own segment (guaranteed LOS)
    var target = robots.OrderBy(r => System.Numerics.Vector3.Distance(r.Pos, startObj.Position)).First();
    float shieldsBefore = target.Shields;
    var aim = System.Numerics.Vector3.Normalize(target.Pos - startObj.Position);
    var firePos = target.Pos - aim * 12f;
    objs.FireWeapon(laser, 0, firePos, aim, target.Segnum);
    for (int step = 0; step < 120; step++)
        objs.MoveWeapons(1f / 60f);
    bool robotHit = target.Shields < shieldsBefore;
    Console.WriteLine($"  laser at robot (id {target.SubId}, from 12 units): " +
                      $"shields {shieldsBefore:F1} -> {target.Shields:F1} hit={robotHit} " +
                      $"(laser dmg {laser.Strength:F1})");
    if (!robotHit)
        errors++;

    // and confirm the player-start shot correctly stops at world geometry
    objs.FireWeapon(laser, 0, startObj.Position, aim, startObj.Segnum);
    int weaponsAlive = objs.Objects.Count(o => o.Type == 5 && !o.Dead);
    for (int step = 0; step < 600; step++)
        objs.MoveWeapons(1f / 60f);
    int weaponsAfter = objs.Objects.Count(o => o.Type == 5 && !o.Dead);
    Console.WriteLine($"  wall-blocked shot: weapons {weaponsAlive} -> {weaponsAfter} (expired/impacted)");
    if (weaponsAfter != 0)
        errors++;

    // destroy the reactor -> exit wall must open
    var reactor = objs.Objects.First(o => o.Type == 9);
    var (exitSeg, exitSide) = lvl3.ReactorTargets[0];
    int exitWallIdx = runtime3.WallAt(exitSeg, exitSide);
    bool exitBefore = world3.WallPassable[exitWallIdx];
    objs.Damage(reactor, 10000f, reactor.Pos);
    for (int step = 0; step < 120; step++)
        runtime3.Tick(1f / 30f, -1, default, 0f);
    bool exitOpen = world3.WallPassable[exitWallIdx];
    Console.WriteLine($"  reactor destroyed={runtime3.ReactorDestroyed}, exit wall passable {exitBefore} -> {exitOpen}");
    if (!runtime3.ReactorDestroyed || !exitOpen)
        errors++;

    // --- 13. robot AI: sees the player, fires, and hits (M5 phase 2) ---
    var shooter = objs.Objects.First(o =>
        o.Type == 2 && !o.Dead && o.SubId < robotStats.Length && robotStats[o.SubId].WeaponType >= 0);
    var facing = shooter.Orient.Forward;
    var probe = new D1U.Game.Fvi(world3);
    var probeInfo = new D1U.Game.FviInfo();
    probe.FindVectorIntersection(new D1U.Game.FviQuery
    {
        P0 = shooter.Pos,
        P1 = shooter.Pos + facing * 40f,
        StartSeg = shooter.Segnum,
        Rad = 1f,
    }, probeInfo);
    float openDist = System.Numerics.Vector3.Distance(probeInfo.HitPoint, shooter.Pos);
    objs.PlayerPos = shooter.Pos + facing * Math.Max(6f, Math.Min(15f, openDist - 4f));
    objs.PlayerSeg = world3.FindPointSeg(objs.PlayerPos, shooter.Segnum);
    objs.PlayerSize = 4.7f;
    objs.PlayerVel = default;
    objs.PlayerAlive = true;
    float playerDamage = 0f;
    int soundEvents = 0;
    objs.PlayerHit += (dmg, src) => playerDamage += dmg;
    objs.Sound += (s, p) => soundEvents++;
    for (int step = 0; step < 600; step++)
    {
        objs.UpdateAi(1f / 60f);
        objs.MoveWeapons(1f / 60f);
    }
    Console.WriteLine($"  robot AI: shooter robot id {shooter.SubId} (weapon {robotStats[shooter.SubId].WeaponType}) " +
                      $"aware={shooter.Aware} playerDamage={playerDamage:F1} soundEvents={soundEvents}");
    if (!shooter.Aware || playerDamage <= 0f)
        errors++;

    // --- 14. matcens spawn robots (level04 has them) ---
    var lvl4 = fsLevels3[3];
    Console.WriteLine($"  matcens in level04: {lvl4.Matcens.Count}");
    if (lvl4.Matcens.Count == 0)
    {
        errors++;
    }
    else
    {
        var world4 = new D1U.Game.SegmentWorld(lvl4);
        var objs4 = new D1U.Game.ObjectSystem(world4,
            record => { var v = D1U.Convert.ObjectVisuals.Resolve(pig, record); return (v.ModelNum, v.VClipNum); },
            robotStats, 200f);
        objs4.SetWeaponTable(allWeaponStats);
        int robotsBefore = objs4.RobotsAlive;
        objs4.TriggerMatcen(lvl4.Matcens[0].SegmentIndex);
        for (int step = 0; step < 60 * 30; step++)
            objs4.TickMatcens(1f / 60f);
        Console.WriteLine($"  matcen spawn: robots {robotsBefore} -> {objs4.RobotsAlive}");
        if (objs4.RobotsAlive <= robotsBefore)
            errors++;
    }

    // --- 15. homing missile acquires and curves onto a robot ---
    // use a robot the AI test never disturbed, with probed open space in front
    var calmBot = objs.Objects.First(o => o.Type == 2 && !o.Dead && !o.Aware);
    var botFacing = calmBot.Orient.Forward;
    probe.FindVectorIntersection(new D1U.Game.FviQuery
    {
        P0 = calmBot.Pos,
        P1 = calmBot.Pos + botFacing * 60f,
        StartSeg = calmBot.Segnum,
        Rad = 1f,
    }, probeInfo);
    float openAhead = System.Numerics.Vector3.Distance(probeInfo.HitPoint, calmBot.Pos);
    var homingFrom = calmBot.Pos + botFacing * Math.Max(12f, Math.Min(28f, openAhead - 4f));
    var dir0 = System.Numerics.Vector3.Normalize(calmBot.Pos - homingFrom);
    var perp = System.Numerics.Vector3.Normalize(
        System.Numerics.Vector3.Cross(dir0, new System.Numerics.Vector3(0, 1, 0)));
    var offDir = System.Numerics.Vector3.Normalize(dir0 + perp * 0.35f); // ~19 degrees off
    int homingSeg = world3.FindPointSeg(homingFrom, calmBot.Segnum);
    var missile = objs.FireWeapon(allWeaponStats[15], 15, homingFrom, offDir, homingSeg);
    float botShieldsBefore = calmBot.Shields;
    bool acquired = false;
    for (int step = 0; step < 240 && !missile.Dead; step++)
    {
        objs.MoveWeapons(1f / 60f);
        acquired |= missile.HomingTarget >= 0;
    }
    Console.WriteLine($"  homing: fired {System.Numerics.Vector3.Distance(homingFrom, calmBot.Pos):F0} units out, " +
                      $"acquired={acquired}, robot shields {botShieldsBefore:F1} -> {calmBot.Shields:F1}");
    if (!acquired)
        errors++;

    // --- 16. death drops: contained powerups spawn with velocity, bounce, and expire ---
    int dropTables = Enumerable.Range(0, robotStats.Length)
        .Count(k => robotStats[k].ContainsCount > 0 && robotStats[k].ContainsProb > 0);
    Console.WriteLine($"  drop tables: {dropTables}/{robotStats.Length} robot types can drop contents");
    if (dropTables == 0)
        errors++;

    var carrier = objs.Objects.First(o => o.Type == 2 && !o.Dead);
    carrier.ContainsType = 7;
    carrier.ContainsId = 1; // energy — never replaced by the energy rule
    carrier.ContainsCount = 3;
    int powerupsBefore = objs.Objects.Count(o => o.Type == 7 && !o.Dead);
    int scoreBefore = objs.Score;
    int firstNewId = objs.Objects.Count; // only THIS kill's eggs, not earlier fights' leftovers
    objs.Damage(carrier, 10000f, carrier.Pos);
    var drops = objs.Objects.Skip(firstNewId)
        .Where(o => o.Type == 7 && !o.Dead && o.SubId == 1).ToList();
    float speed0 = drops.Count > 0 ? drops[0].Vel.Length() : 0f;
    for (int step = 0; step < 300; step++)
        objs.MovePowerups(1f / 60f);
    float speed1 = drops.Count > 0 ? drops[0].Vel.Length() : 0f;
    int powerupsAfter = objs.Objects.Count(o => o.Type == 7 && !o.Dead);
    Console.WriteLine($"  drops: powerups {powerupsBefore} -> {powerupsAfter} " +
                      $"(3 energy dropped, all expiring), drop speed {speed0:F1} -> {speed1:F1}, " +
                      $"score +{objs.Score - scoreBefore}");
    if (drops.Count != 3 || drops.Any(d => d.LifeLeft <= 0f) || speed1 >= speed0 ||
        drops.Any(d => d.Segnum < 0))
        errors++;

    // --- 17. out-of-sight pathfinding: a provoked robot closes on a hidden player ---
    var hunter = objs.Objects.First(o => o.Type == 2 && !o.Dead && !o.Aware);
    // walk the segment graph for a spot several rooms away from the hunter
    int farSeg = hunter.Segnum;
    var depthOf = new Dictionary<int, int> { [hunter.Segnum] = 0 };
    var bfs = new Queue<int>();
    bfs.Enqueue(hunter.Segnum);
    while (bfs.Count > 0)
    {
        int seg = bfs.Dequeue();
        if (depthOf[seg] >= 6)
            continue;
        var ss = world3.Sides[seg];
        for (int sn = 0; sn < 6; sn++)
        {
            int child = ss[sn].Child;
            if (child < 0 || depthOf.ContainsKey(child) || !world3.IsPassable(ss[sn]))
                continue;
            depthOf[child] = depthOf[seg] + 1;
            if (depthOf[child] > depthOf[farSeg])
                farSeg = child;
            bfs.Enqueue(child);
        }
    }
    objs.PlayerPos = world3.SegmentCenter(farSeg);
    objs.PlayerSeg = farSeg;
    objs.PlayerVel = default;
    hunter.Aware = true;
    hunter.Provoked = true; // as if shot: still robots chase too
    float distBefore = System.Numerics.Vector3.Distance(hunter.Pos, objs.PlayerPos);
    for (int step = 0; step < 60 * 15; step++)
        objs.UpdateAi(1f / 60f);
    float distAfter = System.Numerics.Vector3.Distance(hunter.Pos, objs.PlayerPos);
    Console.WriteLine($"  pathfinding: hidden player {depthOf[farSeg]} rooms away, " +
                      $"distance {distBefore:F0} -> {distAfter:F0} " +
                      $"(robot id {hunter.SubId}, behavior 0x{hunter.Behavior:X2})");
    if (depthOf[farSeg] < 3 || distAfter > distBefore * 0.6f)
        errors++;

    // --- 18. briefings: TXB decodes and every level + the ending have messages ---
    var briefText = D1U.Convert.BriefingScript.LoadText(hog, "briefing.tex", "briefing.txb");
    int levelsWithBriefing = 0;
    for (int lv = 1; lv <= 27; lv++)
        if (briefText != null && D1U.Convert.BriefingScript.ScreensForLevel(lv)
                .Any(s => D1U.Convert.BriefingScript.GetMessage(briefText, s.MessageNum) != null))
            levelsWithBriefing++;
    var endText = D1U.Convert.BriefingScript.LoadText(hog, "endreg.tex", "endreg.txb", "ending.tex", "ending.txb");
    int endScreens = endText == null ? 0
        : D1U.Convert.BriefingScript.ScreensForLevel(D1U.Convert.BriefingScript.EndingLevelNum)
            .Count(s => D1U.Convert.BriefingScript.GetMessage(endText, s.MessageNum) != null);
    var introScreens = briefText == null ? 0
        : D1U.Convert.BriefingScript.ScreensForLevel(1)
            .Count(s => D1U.Convert.BriefingScript.GetMessage(briefText, s.MessageNum) != null);
    Console.WriteLine($"  briefings: text {briefText?.Length ?? 0} chars, " +
                      $"levels with briefing {levelsWithBriefing}/27, " +
                      $"level-1 screens (intro+moon) {introScreens}, ending screens {endScreens}");
    if (briefText == null || briefText.Length < 5000 || levelsWithBriefing < 27 || endScreens == 0)
        errors++;

    // --- 19. savegame roundtrip: weapons + runtime + objects restore exactly ---
    runtime3.Player.Shields = 47.5f;
    runtime3.Player.Keys = 6;
    var loadout = new D1U.Game.PlayerWeapons { LaserLevel = 2, Quad = true, Concussions = 7, HasVulcan = true, VulcanAmmo = 300 };
    var saveMs = new System.IO.MemoryStream();
    var saveBw = new System.IO.BinaryWriter(saveMs);
    loadout.Save(saveBw);
    runtime3.Save(saveBw);
    objs.Save(saveBw);

    var worldB = new D1U.Game.SegmentWorld(lvl3);
    var runtimeB = new D1U.Game.LevelRuntime(worldB, clips3);
    var objsB = new D1U.Game.ObjectSystem(worldB,
        record => { var v = D1U.Convert.ObjectVisuals.Resolve(pig, record); return (v.ModelNum, v.VClipNum); },
        robotStats, 200f) { Runtime = runtimeB };
    objsB.SetWeaponTable(allWeaponStats);
    var loadoutB = new D1U.Game.PlayerWeapons();
    var loadBr = new System.IO.BinaryReader(new System.IO.MemoryStream(saveMs.ToArray()));
    loadoutB.Load(loadBr);
    runtimeB.Load(loadBr);
    objsB.Load(loadBr);

    bool passableMatch = true;
    for (int w = 0; w < world3.WallPassable.Length; w++)
        passableMatch &= world3.WallPassable[w] == worldB.WallPassable[w];
    int aliveA = objs.Objects.Count(o => !o.Dead), aliveB = objsB.Objects.Count(o => !o.Dead);
    var aliveBotA = objs.Objects.First(o => o.Type == 2 && !o.Dead);
    var aliveBotB = objsB.Objects[aliveBotA.Id];
    Console.WriteLine($"  savegame: weapons L{loadoutB.LaserLevel + 1} quad={loadoutB.Quad} conc={loadoutB.Concussions} vulcan={loadoutB.VulcanAmmo}, " +
                      $"shields {runtimeB.Player.Shields}, keys {runtimeB.Player.Keys}, reactorDestroyed={runtimeB.ReactorDestroyed}");
    Console.WriteLine($"  savegame: objects {objs.Objects.Count} -> {objsB.Objects.Count} (alive {aliveA} -> {aliveB}), " +
                      $"walls passable match={passableMatch}, robots {objs.RobotsAlive} -> {objsB.RobotsAlive}, score {objs.Score} -> {objsB.Score}");
    if (loadoutB.LaserLevel != 2 || !loadoutB.Quad || loadoutB.Concussions != 7 || loadoutB.VulcanAmmo != 300 ||
        Math.Abs(runtimeB.Player.Shields - 47.5f) > 0.001f || runtimeB.Player.Keys != 6 ||
        !runtimeB.ReactorDestroyed || !passableMatch ||
        objsB.Objects.Count != objs.Objects.Count || aliveA != aliveB ||
        objsB.RobotsAlive != objs.RobotsAlive || objsB.Score != objs.Score ||
        aliveBotB.Pos != aliveBotA.Pos || aliveBotB.Aware != aliveBotA.Aware)
        errors++;

    // --- 20. player death spills the loadout (drop_player_eggs) ---
    var richLoadout = new D1U.Game.PlayerWeapons
    {
        LaserLevel = 3, Quad = true, HasVulcan = true, VulcanAmmo = 392,
        Concussions = 7, Homings = 2, Smarts = 1,
    };
    int powerupsBeforeDeath = objsB.Objects.Count(o => o.Type == 7 && !o.Dead);
    objsB.DropPlayerEggs(objsB.Objects[aliveBotA.Id].Pos, default, objsB.Objects[aliveBotA.Id].Segnum,
        richLoadout, runtimeB.Player);
    int spilled = objsB.Objects.Count(o => o.Type == 7 && !o.Dead) - powerupsBeforeDeath;
    // 3 laser + quad + vulcan gun + 2 ammo boxes + 1x conc-4 + 3x conc-1 + 2 homing + 1 smart + shield + energy = 16
    Console.WriteLine($"  death drops: {spilled} powerups spilled (expected 16)");
    if (spilled != 16)
        errors++;

    // --- 22. netgame loopback: join, state, fire, door, pickup, frags ---
    using (var host = D1U.Game.NetSession.Host("firststrike", 7, "HOST", 28650))
    using (var client = D1U.Game.NetSession.Join("127.0.0.1", "PILOT42", 28650))
    {
        var clientSawFire = new List<(int slot, byte weapon)>();
        int clientDoor = -1, hostPickup = -1;
        client.RemoteFire += (slot, weapon, p, d) => clientSawFire.Add((slot, weapon));
        client.RemoteDoor += w => clientDoor = w;
        host.RemotePickup += id => hostPickup = id;

        double now = 0;
        void Pump(double seconds)
        {
            for (double t = 0; t < seconds; t += 0.02)
            {
                now += 0.02;
                host.Update(now);
                client.Update(now);
            }
        }

        Pump(1.5); // client knocks at t=0.., host welcomes
        bool joined = client.Connected && client.MissionKey == "firststrike" && client.LevelNumber == 7;

        host.SendState(new System.Numerics.Vector3(10, 20, 30), D1U.Game.Mat3.Identity, default);
        host.SendFire(11, new System.Numerics.Vector3(1, 2, 3), new System.Numerics.Vector3(0, 0, 1));
        host.SendDoor(42);
        client.SendPickup(123);
        client.SendDied(0); // host killed the client
        Pump(0.5);

        bool stateSeen = client.Players.TryGetValue(0, out var hostGhost) &&
                         Math.Abs(hostGhost.Pos.X - 10f) < 0.01f;
        bool fireSeen = clientSawFire.Count == 1 && clientSawFire[0] == (0, (byte)11);
        bool frags = host.LocalFrags == 1 && client.Players[0].Frags == 1;
        Console.WriteLine($"  netgame: joined={joined} (slot {client.LocalSlot}), host state seen={stateSeen}, " +
                          $"fire relayed={fireSeen}, door={clientDoor}, pickup={hostPickup}, " +
                          $"host frags={host.LocalFrags}/client-view={client.Players[0].Frags}");
        if (!joined || !stateSeen || !fireSeen || clientDoor != 42 || hostPickup != 123 || !frags)
            errors++;
    }

    // --- 21. reactor self-destruct countdown: T-n, voices, mine explosion ---
    // runtime3 destroyed its reactor earlier and ticked ~4 s since
    int secondsAtStart = runtime3.CountdownSecondsLeft;
    var voiceIds = new List<int>();
    bool exploded = false;
    runtimeB.CountdownSound += id => voiceIds.Add(id);
    runtimeB.MineExploded += () => exploded = true;
    for (int step = 0; step < 60 * 60 && !exploded; step++)
        runtimeB.Tick(1f / 60f, -1, default, 0f);
    Console.WriteLine($"  countdown: active={runtime3.CountdownActive} T-{secondsAtStart} of {runtime3.TotalCountdown} " +
                      $"(Hotshot=40); loaded copy ran to explosion={exploded}, " +
                      $"voices [{string.Join(",", voiceIds.Take(6))}...] incl 12s-warning={voiceIds.Contains(114)} zero={voiceIds.Contains(100)}");
    if (!runtime3.CountdownActive || runtime3.TotalCountdown != 40 ||
        secondsAtStart <= 0 || secondsAtStart >= 40 ||
        !exploded || !voiceIds.Contains(114) || !voiceIds.Contains(100) || !voiceIds.Contains(33))
        errors++;

    // --- 23. review fixes: winding, 2x grab reach, egg size, reactor fire, net eggs ---
    // (a) every door face must front toward its own segment's interior (Cull Back)
    int badWinding = 0;
    var doorTris = new List<(System.Numerics.Vector3 A, System.Numerics.Vector3 B,
                             System.Numerics.Vector3 C, System.Numerics.Vector3 N,
                             System.Numerics.Vector3 Cen)>();
    foreach (var doorPiece in lvl3.DoorPieces)
    {
        var wseg = lvl3.Segments[doorPiece.SegmentIndex];
        var segCenter = System.Numerics.Vector3.Zero;
        for (int v = 0; v < 8; v++) segCenter += lvl3.Vertices[wseg.Verts[v]];
        segCenter /= 8f;
        var geom = doorPiece.Geometry;
        for (int t = 0; t + 2 < geom.Positions.Count; t += 3)
        {
            var wa = geom.Positions[t];
            var wb = geom.Positions[t + 1];
            var wc = geom.Positions[t + 2];
            var wn = System.Numerics.Vector3.Cross(wb - wa, wc - wa);
            if (System.Numerics.Vector3.Dot(wn, segCenter - wa) < 0f)
                badWinding++;
            if (wn.LengthSquared() > 1e-10f)
                doorTris.Add((wa, wb, wc, wn, (wa + wb + wc) / 3f));
        }
    }

    // (a2) no two door triangles may be coplanar, same-facing AND overlapping —
    // such a pair z-fights even with Cull Back (a portal's front+back walls
    // must front opposite ways, one into each segment)
    static bool InsideTri(System.Numerics.Vector3 p, System.Numerics.Vector3 a,
                          System.Numerics.Vector3 b, System.Numerics.Vector3 c,
                          System.Numerics.Vector3 n)
    {
        float eps = 1e-4f * n.LengthSquared(); // strictly inside, scale-relative
        return System.Numerics.Vector3.Dot(System.Numerics.Vector3.Cross(b - a, p - a), n) > eps &&
               System.Numerics.Vector3.Dot(System.Numerics.Vector3.Cross(c - b, p - b), n) > eps &&
               System.Numerics.Vector3.Dot(System.Numerics.Vector3.Cross(a - c, p - c), n) > eps;
    }
    int zfightPairs = 0;
    for (int i = 0; i < doorTris.Count; i++)
        for (int j = i + 1; j < doorTris.Count; j++)
        {
            var ti = doorTris[i];
            var tj = doorTris[j];
            if (System.Numerics.Vector3.Dot(ti.N, tj.N) <= 0f)
                continue; // opposite-facing: Cull Back separates them
            if (System.Numerics.Vector3.Cross(ti.N, tj.N).LengthSquared() >
                1e-6f * ti.N.LengthSquared() * tj.N.LengthSquared())
                continue; // not parallel
            if (Math.Abs(System.Numerics.Vector3.Dot(
                    System.Numerics.Vector3.Normalize(ti.N), tj.Cen - ti.A)) > 1e-2f)
                continue; // parallel but on different planes
            if (InsideTri(tj.Cen, ti.A, ti.B, ti.C, ti.N) ||
                InsideTri(ti.Cen, tj.A, tj.B, tj.C, tj.N))
                zfightPairs++;
        }

    // (b) powerup_grab_cheat_all: a dropped egg at 1.9x combined radii is grabbed
    var grabEgg = objsB.Objects.Last(o => o.Type == 7 && !o.Dead);
    float grabEggSize = grabEgg.Size; // DropEgg must use Powerup_info size (3.0 default)
    var grabState = new D1U.Game.PlayerState();
    var grabWeapons = new D1U.Game.PlayerWeapons();
    var grabShipPos = grabEgg.Pos + new System.Numerics.Vector3(1.9f * (grabEgg.Size + 4.66f), 0f, 0f);
    int grabBefore = objsB.Objects.Count(o => o.Type == 7 && !o.Dead);
    objsB.PickupScan(grabShipPos, 4.66f, grabEgg.Segnum, grabState, grabWeapons);
    int grabAfter = objsB.Objects.Count(o => o.Type == 7 && !o.Dead);

    // (c) the reactor returns fire once hit (do_controlcen_frame)
    var worldC = new D1U.Game.SegmentWorld(lvl3);
    var runtimeC = new D1U.Game.LevelRuntime(worldC, clips3);
    var objsC = new D1U.Game.ObjectSystem(worldC,
        record => { var v = D1U.Convert.ObjectVisuals.Resolve(pig, record); return (v.ModelNum, v.VClipNum); },
        robotStats, 200f) { Runtime = runtimeC };
    objsC.SetWeaponTable(allWeaponStats);
    int reactorShots = 0;
    var reactorC = objsC.Objects.FirstOrDefault(o => o.Type == 9 && !o.Dead);
    if (reactorC != null)
    {
        objsC.PlayerAlive = true;
        objsC.PlayerPos = reactorC.Pos + new System.Numerics.Vector3(20f, 0f, 0f);
        objsC.PlayerSeg = reactorC.Segnum;
        objsC.Damage(reactorC, 1f, reactorC.Pos); // wakes the defense guns
        for (int i = 0; i < 300; i++)
            objsC.TickReactor(1f / 60f);
        reactorShots = objsC.Objects.Count(o => o.Type == 5 && o.ParentId == reactorC.Id);
    }

    // (d) MsgEggs roundtrip + a malformed datagram must not wedge the pump
    bool eggsOk;
    using (var host2 = D1U.Game.NetSession.Host("firststrike", 1, "H", 28652))
    using (var client2 = D1U.Game.NetSession.Join("127.0.0.1", "C", 28652))
    {
        (int netId, byte subId, System.Numerics.Vector3 pos, System.Numerics.Vector3 vel)[] gotEggs = null;
        host2.RemoteEggs += (slot, eggList) => gotEggs = eggList;
        double now2 = 0;
        void Pump2(double seconds)
        {
            for (double t = 0; t < seconds; t += 0.02)
            {
                now2 += 0.02;
                host2.Update(now2);
                client2.Update(now2);
            }
        }
        Pump2(1.5);
        using (var rogue = new System.Net.Sockets.UdpClient())
            rogue.Send(new byte[] { 0xD1, 3 }, 2, "127.0.0.1", 28652); // truncated poison
        client2.SendEggs(new[]
        {
            (200001, (byte)13, new System.Numerics.Vector3(1, 2, 3), System.Numerics.Vector3.Zero),
            (200002, (byte)2, new System.Numerics.Vector3(4, 5, 6), System.Numerics.Vector3.Zero),
        });
        Pump2(0.5);
        eggsOk = gotEggs != null && gotEggs.Length == 2 &&
                 gotEggs[0].netId == 200001 && gotEggs[1].subId == 2;
    }

    Console.WriteLine($"  review fixes: bad winding={badWinding}, coplanar zfight pairs={zfightPairs}, " +
                      $"grab@1.9x={grabAfter < grabBefore}, egg size={grabEggSize:F1}, " +
                      $"reactor shots in 5s={reactorShots}, net eggs ok={eggsOk}");
    if (badWinding > 0 || zfightPairs > 0 || grabAfter >= grabBefore || grabEggSize < 2.5f ||
        reactorShots < 4 || !eggsOk)
        errors++;
}
catch (Exception e)
{
    Console.Error.WriteLine($"  M5 FAIL: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
    errors++;
}

Console.WriteLine(errors == 0 ? "\nSMOKE OK" : $"\nSMOKE FAILED: {errors} error(s)");
return errors == 0 ? 0 : 2;
