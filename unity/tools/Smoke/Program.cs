// D1U M0 smoke test: verify LibDescent parses everything in the hogs directory.
// Usage: D1U.Smoke [hogsDir]  (default: walk up from cwd looking for d1/hogs/DESCENT.HOG)

using LibDescent.Data;

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

// --- 1. DESCENT.HOG container ---
var hog = new HOGFile(Path.Combine(hogsDir, "DESCENT.HOG"));
Console.WriteLine($"\nDESCENT.HOG: {hog.NumLumps} lumps");
foreach (var group in hog.Lumps
             .GroupBy(l => Path.GetExtension(l.Name).ToLowerInvariant())
             .OrderByDescending(g => g.Count()))
    Console.WriteLine($"  {group.Key,-6} x{group.Count()}");

// --- 2. DESCENT.PIG (bitmaps + sounds + embedded gamedata tables) ---
var pig = new Descent1PIGFile(macPig: false, loadData: true);
using (var fs = File.OpenRead(Path.Combine(hogsDir, "DESCENT.PIG")))
    pig.Read(fs);
Console.WriteLine($"\nDESCENT.PIG: {pig.Bitmaps.Count} bitmaps, {pig.Sounds.Count} sounds");
Console.WriteLine($"  gamedata: textures={pig.numTextures} vclips={pig.numVClips} eclips={pig.numEClips} " +
                  $"wclips={pig.numWClips} robots={pig.numRobots} joints={pig.numJoints} " +
                  $"weapons={pig.numWeapons} models={pig.numModels}");
if (pig.Bitmaps.Count < 1000 || pig.numRobots <= 0 || pig.numModels <= 0)
{
    Console.Error.WriteLine("FAIL: PIG gamedata looks empty — wrong pig version or parse failure.");
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
