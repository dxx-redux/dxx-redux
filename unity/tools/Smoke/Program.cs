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

Console.WriteLine(errors == 0 ? "\nSMOKE OK" : $"\nSMOKE FAILED: {errors} error(s)");
return errors == 0 ? 0 : 2;
