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
Console.WriteLine("\nlevel baking:");
var missionList = MissionScanner.Scan(hogsDir);
int levelBakeFailures = 0, levelsBaked = 0;
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
errors += levelBakeFailures;

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
}
catch (Exception e)
{
    Console.Error.WriteLine($"  M3 FAIL: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
    errors++;
}

Console.WriteLine(errors == 0 ? "\nSMOKE OK" : $"\nSMOKE FAILED: {errors} error(s)");
return errors == 0 ? 0 : 2;
