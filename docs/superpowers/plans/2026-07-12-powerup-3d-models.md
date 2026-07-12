# Powerup 3D Models Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all 22 Descent-1 powerup turntable sprites with runtime-loaded 3D GLB models (via glTFast) through a per-powerup override layer, idle-spinning, with the existing sprite as automatic fallback.

**Architecture:** Presentation-only. A new `OverrideModels` (Unity side) discovers `powerup_<name>.glb` files, loads them once via glTFast behind an abstract `GlbLoader` seam, bakes each to a unit-normalized cached mesh with the game's unlit material, and clones spinning instances on demand. `LevelViewer`'s two object-dispatch sites gain a guard: if a powerup has an override model, instantiate the mesh; else fall back to the current `BillboardSprite`. Pure, testable logic (id→name table, auto-fit math, manifest parse) lives in the UnityEngine-free `D1U.Convert` assembly and is unit-tested by the Smoke harness.

**Tech Stack:** Unity 6000.5.3f1, URP 17.5.0, C# (Unity + netstandard2.0), glTFast (`com.unity.cloud.gltfast`), the existing `D1U.Convert` / `D1U.Game` / `D1U.Presentation` asmdefs, `Smoke` + `PresentationCheck` headless tools.

## Global Constraints

- **Layers 1–2 stay UnityEngine-free.** New pure helpers go in `D1U.Convert` (netstandard2.0) and may use `System.Numerics`, never `UnityEngine`. Only `D1U.Presentation` code uses `UnityEngine`/`GLTFast`.
- **glTFast must not break headless compile-check.** `PresentationCheck` references no glTFast DLL, so the single file that does `using GLTFast;` is excluded from it via `<Compile Remove>`; all other new Presentation code compiles against the abstract `GlbLoader` seam.
- **.NET SDK is user-scoped, not on PATH:** `C:\Users\Yermak\AppData\Local\Microsoft\dotnet\dotnet.exe` (referred to below as `$DOTNET`).
- **Unity editor:** `C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe`. Batch mode fails with a lockfile while the user's editor is open — prefer `PresentationCheck`/`Smoke` for headless checks; run in-editor when the editor is available.
- **Hogs data:** `C:\Users\Yermak\Projects\dxx-redux\d1\hogs`.
- **No original game assets in the repo.** Override models are original content and live in `unity/Assets/StreamingAssets/overrides/models/`.
- **Powerup set = the 22 ids in the spec §3** (`0,1,2,3,4,5,6,10-23,25`). The 7 `exp13`-placeholder ids (`7,8,9,24,26,27,28`) are excluded.
- **Commits** end with: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`. Work on branch `unity`.
- **Spin default:** gentle, `0.15` rev/s about world up.
- Spec: `docs/superpowers/specs/2026-07-12-weapon-powerup-3d-models-design.md`.

## File Structure

**Create (pure, `D1U.Convert` — Smoke-testable):**
- `unity/Assets/Scripts/Convert/PowerupOverrides.cs` — id→override-name table (the 22-powerup allowlist).
- `unity/Assets/Scripts/Convert/ModelAutoFit.cs` — bounds → (center, unit-scale) math.
- `unity/Assets/Scripts/Convert/OverrideManifest.cs` — parse optional per-model `.json` (rotation/scale/keepPbr).
- `unity/Assets/Scripts/Convert/TestGlb.cs` — generates a tiny valid GLB (test fixture for the glTFast spike).

**Create (`D1U.Presentation`):**
- `unity/Assets/Scripts/Presentation/GlbLoader.cs` — abstract `GlbLoader` + `GlbMesh` result (no glTFast).
- `unity/Assets/Scripts/Presentation/OverrideModels.Gltfast.cs` — `GltfastGlbLoader : GlbLoader` (the ONLY `using GLTFast;` file; excluded from PresentationCheck).
- `unity/Assets/Scripts/Presentation/OverrideModels.cs` — discovery, preload, cache, `TryInstantiate`, dispose.
- `unity/Assets/Scripts/Presentation/SpinModel.cs` — idle-spin MonoBehaviour.

**Create (tool):**
- `unity/tools/RefKit/` — console tool producing per-powerup AI-generator reference kits.

**Modify:**
- `unity/Packages/manifest.json` — add glTFast + newtonsoft.
- `unity/tools/PresentationCheck/PresentationCheck.csproj` — `<Compile Remove>` the glTFast file.
- `unity/tools/Smoke/Program.cs` — append unit-test sections before the `SMOKE OK` line (L1257).
- `unity/Assets/Scripts/Presentation/LevelViewer.cs` — construct `overrideModels`, preload gate in `Update` (L793), guards at both dispatch sites (L392–411 static, L604–627 dynamic), dispose (L1963).

---

## Task 1: Add glTFast dependency

**Files:**
- Modify: `unity/Packages/manifest.json`

**Interfaces:**
- Produces: the `GLTFast` namespace available to Unity-side code.

- [ ] **Step 1: Add the two dependency lines**

Edit `unity/Packages/manifest.json`, adding these two entries at the top of `"dependencies"` (keep the rest unchanged):

```json
    "com.unity.cloud.gltfast": "6.9.0",
    "com.unity.nuget.newtonsoft-json": "3.2.1",
```

- [ ] **Step 2: Resolve packages**

If the editor is open, let it resolve (focus the editor). Headless alternative:

Run: `& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" -batchmode -quit -projectPath unity -logFile pkgresolve.log`
Expected: exit 0; `unity/Packages/packages-lock.json` gains a `com.unity.cloud.gltfast` entry.

If version `6.9.0` is rejected, the log lists available versions — use the highest `6.x` it offers and repeat.

- [ ] **Step 3: Commit**

```bash
git add unity/Packages/manifest.json unity/Packages/packages-lock.json
git commit -m "Add glTFast dependency for runtime GLB powerup models

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: `TestGlb` fixture + Smoke test

A minimal, dependency-free GLB generator used to prove glTFast loading without shipping a binary asset. Pure `D1U.Convert` code, so Smoke can validate the container it emits.

**Files:**
- Create: `unity/Assets/Scripts/Convert/TestGlb.cs`
- Test: `unity/tools/Smoke/Program.cs` (append section)

**Interfaces:**
- Produces: `D1U.Convert.TestGlb.Triangle()` → `byte[]` (a valid self-contained `.glb`).

- [ ] **Step 1: Write the failing Smoke test**

Append to `unity/tools/Smoke/Program.cs` immediately **before** the line `Console.WriteLine(errors == 0 ? "\nSMOKE OK" ...` (L1257):

```csharp
// --- N. TestGlb emits a valid GLB container ---
{
    var glb = D1U.Convert.TestGlb.Triangle();
    uint magic = BitConverter.ToUInt32(glb, 0);
    uint version = BitConverter.ToUInt32(glb, 4);
    uint total = BitConverter.ToUInt32(glb, 8);
    uint jsonChunkType = BitConverter.ToUInt32(glb, 16);
    if (magic != 0x46546C67 || version != 2 || total != glb.Length || jsonChunkType != 0x4E4F534A)
    {
        Console.Error.WriteLine($"  TestGlb FAIL: magic={magic:X} ver={version} total={total}/{glb.Length} jtype={jsonChunkType:X}");
        errors++;
    }
    else Console.WriteLine($"  TestGlb: valid GLB, {glb.Length} bytes");
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs`
Expected: FAIL to compile — `TestGlb` does not exist.

- [ ] **Step 3: Implement `TestGlb`**

Create `unity/Assets/Scripts/Convert/TestGlb.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace D1U.Convert
{
    /// <summary>Generates a tiny, self-contained binary glTF (.glb) — a single
    /// red triangle. Used to prove the runtime GLB loader without a binary asset.</summary>
    public static class TestGlb
    {
        public static byte[] Triangle()
        {
            // BIN: 3 VEC3 float positions (36 B) then 3 ushort indices (6 B), pad to 4.
            var bin = new List<byte>();
            void PutF(float v) => bin.AddRange(BitConverter.GetBytes(v));
            void PutU(ushort v) => bin.AddRange(BitConverter.GetBytes(v));
            PutF(0); PutF(0); PutF(0);   // vertex 0
            PutF(1); PutF(0); PutF(0);   // vertex 1
            PutF(0); PutF(1); PutF(0);   // vertex 2
            PutU(0); PutU(1); PutU(2);   // indices
            while (bin.Count % 4 != 0) bin.Add(0);
            int binLen = bin.Count;

            string json =
                "{\"asset\":{\"version\":\"2.0\"}," +
                "\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}]," +
                "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1,\"material\":0}]}]," +
                "\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.8,0.2,0.2,1.0]}}]," +
                "\"buffers\":[{\"byteLength\":" + binLen + "}]," +
                "\"bufferViews\":[" +
                    "{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36,\"target\":34962}," +
                    "{\"buffer\":0,\"byteOffset\":36,\"byteLength\":6,\"target\":34963}]," +
                "\"accessors\":[" +
                    "{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\",\"min\":[0,0,0],\"max\":[1,1,0]}," +
                    "{\"bufferView\":1,\"componentType\":5123,\"count\":3,\"type\":\"SCALAR\"}]}";
            var jsonBytes = new List<byte>(Encoding.UTF8.GetBytes(json));
            while (jsonBytes.Count % 4 != 0) jsonBytes.Add(0x20); // pad JSON chunk with spaces

            int total = 12 + 8 + jsonBytes.Count + 8 + binLen;
            var glb = new List<byte>(total);
            void W(uint v) => glb.AddRange(BitConverter.GetBytes(v));
            W(0x46546C67); W(2); W((uint)total);                              // header: "glTF", ver 2, length
            W((uint)jsonBytes.Count); W(0x4E4F534A); glb.AddRange(jsonBytes); // JSON chunk ("JSON")
            W((uint)binLen); W(0x004E4942); glb.AddRange(bin);               // BIN chunk ("BIN\0")
            return glb.ToArray();
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs`
Expected: PASS — `TestGlb: valid GLB, NNN bytes`, ending `SMOKE OK`.

- [ ] **Step 5: Commit**

```bash
git add unity/Assets/Scripts/Convert/TestGlb.cs unity/tools/Smoke/Program.cs
git commit -m "Add TestGlb fixture + Smoke validation

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: `GlbLoader` seam + glTFast implementation + spike

Establishes the abstract seam that keeps glTFast out of headless compile-check, and proves runtime loading.

**Files:**
- Create: `unity/Assets/Scripts/Presentation/GlbLoader.cs`
- Create: `unity/Assets/Scripts/Presentation/OverrideModels.Gltfast.cs`
- Modify: `unity/tools/PresentationCheck/PresentationCheck.csproj`

**Interfaces:**
- Produces:
  - `abstract class GlbLoader { public abstract Task<GlbMesh> LoadAsync(byte[] glb, string uri); }`
  - `class GlbMesh { public Mesh Mesh; public Texture2D[] BaseTextures; }`
  - `static Func<GlbLoader> GltfastGlbLoader.Factory` registered at runtime.

- [ ] **Step 1: Create the abstract seam (compiles under PresentationCheck)**

`unity/Assets/Scripts/Presentation/GlbLoader.cs`:

```csharp
using System.Threading.Tasks;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>One combined mesh baked from a GLB, plus the base-color texture
    /// per submesh (index-aligned to mesh submeshes). Unity types only — no glTFast.</summary>
    public sealed class GlbMesh
    {
        public Mesh Mesh;
        public Texture2D[] BaseTextures;
    }

    /// <summary>Loads a GLB byte buffer to a single combined <see cref="GlbMesh"/>.
    /// Abstract so the glTFast dependency is isolated to one file the headless
    /// PresentationCheck excludes.</summary>
    public abstract class GlbLoader
    {
        public abstract Task<GlbMesh> LoadAsync(byte[] glb, string uri);
    }
}
```

- [ ] **Step 2: Create the glTFast implementation (excluded from PresentationCheck)**

`unity/Assets/Scripts/Presentation/OverrideModels.Gltfast.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>glTFast-backed GLB loader. THE ONLY file using GLTFast — excluded
    /// from tools/PresentationCheck (which has no glTFast reference).</summary>
    public sealed class GltfastGlbLoader : GlbLoader
    {
        public override async Task<GlbMesh> LoadAsync(byte[] glb, string uri)
        {
            var import = new GltfImport();
            if (!await import.Load(glb, uri: new System.Uri(uri)))
                return null;

            var holder = new GameObject("glb_temp") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            if (!await import.InstantiateMainSceneAsync(holder.transform))
            {
                Object.DestroyImmediate(holder);
                return null;
            }

            var combines = new List<CombineInstance>();
            var textures = new List<Texture2D>();
            foreach (var mf in holder.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null) continue;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                {
                    combines.Add(new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        subMeshIndex = s,
                        transform = mf.transform.localToWorldMatrix, // holder at origin
                    });
                    var mat = mr != null && s < mr.sharedMaterials.Length ? mr.sharedMaterials[s] : null;
                    textures.Add(ExtractBaseTexture(mat));
                }
            }

            var mesh = new Mesh { name = "glb_combined", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(combines.ToArray(), mergeSubMeshes: false, useMatrices: true);
            mesh.RecalculateBounds();
            Object.DestroyImmediate(holder);
            import.Dispose();

            return new GlbMesh { Mesh = mesh, BaseTextures = textures.ToArray() };
        }

        static Texture2D ExtractBaseTexture(Material m)
        {
            if (m == null) return null;
            if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") is Texture2D b) return b;
            if (m.HasProperty("baseColorTexture") && m.GetTexture("baseColorTexture") is Texture2D c) return c;
            return m.mainTexture as Texture2D;
        }
    }

    static class GltfastRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => OverrideModels.GlbLoaderFactory = () => new GltfastGlbLoader();
    }
}
```

- [ ] **Step 3: Exclude the glTFast file from PresentationCheck**

In `unity/tools/PresentationCheck/PresentationCheck.csproj`, immediately after the `<Compile Include="..\..\Assets\Scripts\Presentation\**\*.cs" />` line, add:

```xml
    <Compile Remove="..\..\Assets\Scripts\Presentation\OverrideModels.Gltfast.cs" />
```

- [ ] **Step 4: Verify PresentationCheck compiles the seam**

`OverrideModels` (referenced by the registration) does not exist yet, so add a minimal stub now to keep this task self-contained, then flesh it out in Task 7. Create `unity/Assets/Scripts/Presentation/OverrideModels.cs` with just:

```csharp
using System;
namespace D1U.Presentation { public sealed partial class OverrideModels { public static Func<GlbLoader> GlbLoaderFactory; } }
```

Run: `$DOTNET build unity/tools/PresentationCheck -c Release`
Expected: `Build succeeded` (the `.Gltfast.cs` file is skipped; `GlbLoader.cs` and the stub compile).

- [ ] **Step 5: Runtime spike — prove glTFast loads a GLB**

Add a temporary editor entry point. Create `unity/Assets/Scripts/EditorTools/GltfSpike.cs`:

```csharp
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using D1U.Convert;
using D1U.Presentation;

public static class GltfSpike
{
    [MenuItem("D1U/Spike: Load Test GLB")]
    public static async void Run()
    {
        var path = Path.Combine(Application.temporaryCachePath, "triangle.glb");
        File.WriteAllBytes(path, TestGlb.Triangle());
        var loader = new GltfastGlbLoader();
        var result = await loader.LoadAsync(File.ReadAllBytes(path), "file://" + path.Replace('\\', '/'));
        Debug.Log(result != null && result.Mesh.vertexCount == 3
            ? $"GLTF SPIKE OK: {result.Mesh.vertexCount} verts, {result.Mesh.subMeshCount} submesh"
            : "GLTF SPIKE FAIL");
    }
}
#endif
```

- [ ] **Step 6: Run the spike**

With the editor open, click **D1U ▸ Spike: Load Test GLB** (or run headless via `-executeMethod GltfSpike.Run -logFile spike.log` if the editor is free).
Expected: Console/log shows `GLTF SPIKE OK: 3 verts, 1 submesh`.

- [ ] **Step 7: Commit**

```bash
git add unity/Assets/Scripts/Presentation/GlbLoader.cs unity/Assets/Scripts/Presentation/OverrideModels.Gltfast.cs unity/Assets/Scripts/Presentation/OverrideModels.cs unity/tools/PresentationCheck/PresentationCheck.csproj unity/Assets/Scripts/EditorTools/GltfSpike.cs
git commit -m "glTFast GlbLoader seam + runtime spike (excluded from PresentationCheck)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: `PowerupOverrides` table + Smoke test

**Files:**
- Create: `unity/Assets/Scripts/Convert/PowerupOverrides.cs`
- Test: `unity/tools/Smoke/Program.cs` (append section)

**Interfaces:**
- Produces:
  - `static IReadOnlyDictionary<int,string> PowerupOverrides.ByPowerupId`
  - `static bool PowerupOverrides.TryGetName(int powerupId, out string name)`

- [ ] **Step 1: Write the failing Smoke test**

Append before L1257 in `unity/tools/Smoke/Program.cs`:

```csharp
// --- N. PowerupOverrides table matches the spec §3 set ---
{
    var t = D1U.Convert.PowerupOverrides.ByPowerupId;
    bool ok = t.Count == 22
        && t[3] == "laser" && t[12] == "quad" && t[1] == "energy"
        && t[4] == "key_blue" && t[25] == "invuln" && t[0] == "extralife"
        && !t.ContainsKey(7) && !t.ContainsKey(24) && !t.ContainsKey(28);
    if (!ok) { Console.Error.WriteLine($"  PowerupOverrides FAIL: count={t.Count}"); errors++; }
    else Console.WriteLine($"  PowerupOverrides: {t.Count} powerups mapped");
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs`
Expected: FAIL to compile — `PowerupOverrides` missing.

- [ ] **Step 3: Implement the table**

`unity/Assets/Scripts/Convert/PowerupOverrides.cs`:

```csharp
using System.Collections.Generic;

namespace D1U.Convert
{
    /// <summary>The 22 real D1 powerups (spec §3) that may carry a 3D override
    /// model, keyed by powerup id (ObjectRecord.SubtypeId / d1/main/powerup.h).
    /// The 7 exp13-placeholder ids (7,8,9,24,26,27,28) are intentionally absent.</summary>
    public static class PowerupOverrides
    {
        public static readonly IReadOnlyDictionary<int, string> ByPowerupId = new Dictionary<int, string>
        {
            [3] = "laser", [13] = "vulcan", [14] = "spread", [15] = "plasma",
            [16] = "fusion", [12] = "quad",
            [10] = "cmissile", [11] = "cmissile4", [18] = "hmissile", [19] = "hmissile4",
            [17] = "pbomb", [20] = "smart", [21] = "mega", [22] = "vammo",
            [1] = "energy", [2] = "shield",
            [4] = "key_blue", [5] = "key_red", [6] = "key_gold",
            [23] = "cloak", [25] = "invuln", [0] = "extralife",
        };

        public static bool TryGetName(int powerupId, out string name) =>
            ByPowerupId.TryGetValue(powerupId, out name);
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs`
Expected: PASS — `PowerupOverrides: 22 powerups mapped`.

- [ ] **Step 5: Commit**

```bash
git add unity/Assets/Scripts/Convert/PowerupOverrides.cs unity/tools/Smoke/Program.cs
git commit -m "Add PowerupOverrides id->name table (22 powerups) + test

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: `ModelAutoFit` + Smoke test

**Files:**
- Create: `unity/Assets/Scripts/Convert/ModelAutoFit.cs`
- Test: `unity/tools/Smoke/Program.cs` (append section)

**Interfaces:**
- Produces: `static (System.Numerics.Vector3 center, float scale) ModelAutoFit.Compute(System.Numerics.Vector3 min, System.Numerics.Vector3 max)` — center = midpoint; scale = 1 / largest extent (so `(v-center)*scale` has max extent 1).

- [ ] **Step 1: Write the failing Smoke test**

Append before L1257 in `unity/tools/Smoke/Program.cs`:

```csharp
// --- N. ModelAutoFit normalizes bounds to unit max-extent ---
{
    var (center, scale) = D1U.Convert.ModelAutoFit.Compute(
        new System.Numerics.Vector3(0, 0, 0), new System.Numerics.Vector3(2, 4, 2));
    bool ok = center == new System.Numerics.Vector3(1, 2, 1) && System.Math.Abs(scale - 0.25f) < 1e-6f;
    if (!ok) { Console.Error.WriteLine($"  ModelAutoFit FAIL: center={center} scale={scale}"); errors++; }
    else Console.WriteLine("  ModelAutoFit: unit-normalize OK");
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs`
Expected: FAIL to compile.

- [ ] **Step 3: Implement**

`unity/Assets/Scripts/Convert/ModelAutoFit.cs`:

```csharp
using System;
using System.Numerics;

namespace D1U.Convert
{
    /// <summary>Normalizes an arbitrary model's bounds so instances can be scaled
    /// by the object's on-map diameter. Returns the centroid and a scale that maps
    /// the largest extent to 1: apply as (vertex - center) * scale.</summary>
    public static class ModelAutoFit
    {
        public static (Vector3 center, float scale) Compute(Vector3 min, Vector3 max)
        {
            var center = (min + max) * 0.5f;
            var size = max - min;
            float maxExtent = Math.Max(size.X, Math.Max(size.Y, size.Z));
            float scale = maxExtent > 1e-6f ? 1f / maxExtent : 1f;
            return (center, scale);
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs`
Expected: PASS — `ModelAutoFit: unit-normalize OK`.

- [ ] **Step 5: Commit**

```bash
git add unity/Assets/Scripts/Convert/ModelAutoFit.cs unity/tools/Smoke/Program.cs
git commit -m "Add ModelAutoFit bounds normalization + test

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: `OverrideManifest` parse + Smoke test

**Files:**
- Create: `unity/Assets/Scripts/Convert/OverrideManifest.cs`
- Test: `unity/tools/Smoke/Program.cs` (append section)

**Interfaces:**
- Produces: `class OverrideManifest { public float[] RotationEuler = {0,0,0}; public float ScaleMul = 1f; public bool KeepPbr = false; }` and `static OverrideManifest OverrideManifest.Parse(string json)` (tolerant; missing keys keep defaults).

- [ ] **Step 1: Write the failing Smoke test**

Append before L1257 in `unity/tools/Smoke/Program.cs`:

```csharp
// --- N. OverrideManifest parses rotation/scale/keepPbr ---
{
    var m = D1U.Convert.OverrideManifest.Parse("{\"rotationEuler\":[0,90,0],\"scaleMul\":1.5,\"keepPbr\":true}");
    var d = D1U.Convert.OverrideManifest.Parse("{}");
    bool ok = m.RotationEuler[1] == 90f && System.Math.Abs(m.ScaleMul - 1.5f) < 1e-6f && m.KeepPbr
              && d.ScaleMul == 1f && !d.KeepPbr && d.RotationEuler[0] == 0f;
    if (!ok) { Console.Error.WriteLine("  OverrideManifest FAIL"); errors++; }
    else Console.WriteLine("  OverrideManifest: parse OK");
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs`
Expected: FAIL to compile.

- [ ] **Step 3: Implement (regex-based, no JSON dependency)**

`unity/Assets/Scripts/Convert/OverrideManifest.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace D1U.Convert
{
    /// <summary>Optional per-model tweaks read from powerup_&lt;name&gt;.json next to the
    /// GLB. Hand-parsed (regex) so D1U.Convert stays dependency-free and testable.</summary>
    public sealed class OverrideManifest
    {
        public float[] RotationEuler = { 0f, 0f, 0f };
        public float ScaleMul = 1f;
        public bool KeepPbr = false;

        public static OverrideManifest Parse(string json)
        {
            var m = new OverrideManifest();
            if (string.IsNullOrEmpty(json)) return m;

            var rot = Regex.Match(json, "\"rotationEuler\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (rot.Success)
            {
                var parts = rot.Groups[1].Value.Split(',');
                for (int i = 0; i < 3 && i < parts.Length; i++)
                    if (float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        m.RotationEuler[i] = v;
            }
            var sc = Regex.Match(json, "\"scaleMul\"\\s*:\\s*([-0-9.eE+]+)");
            if (sc.Success && float.TryParse(sc.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                m.ScaleMul = s;
            m.KeepPbr = Regex.IsMatch(json, "\"keepPbr\"\\s*:\\s*true");
            return m;
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs`
Expected: PASS — `OverrideManifest: parse OK`.

- [ ] **Step 5: Commit**

```bash
git add unity/Assets/Scripts/Convert/OverrideManifest.cs unity/tools/Smoke/Program.cs
git commit -m "Add OverrideManifest parser + test

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: `OverrideModels` — discovery, preload, cache, instantiate

Replaces the Task 3 stub with the full implementation. All UnityEngine, no glTFast (uses the `GlbLoader` seam), so PresentationCheck covers it.

**Files:**
- Modify: `unity/Assets/Scripts/Presentation/OverrideModels.cs`

**Interfaces:**
- Consumes: `GlbLoader`, `GlbMesh`, `PowerupOverrides`, `ModelAutoFit`, `OverrideManifest`.
- Produces:
  - `OverrideModels(Shader modelShader, IEnumerable<string> overrideDirs)`
  - `static Func<GlbLoader> GlbLoaderFactory`
  - `bool AnyPresent { get; }`
  - `bool Has(int powerupId)`
  - `IEnumerator PreloadRoutine()`
  - `GameObject TryInstantiate(int powerupId, float radius, float light)`
  - `void Dispose()`

- [ ] **Step 1: Implement**

Replace the entire contents of `unity/Assets/Scripts/Presentation/OverrideModels.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using D1U.Convert;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>Discovers powerup_&lt;name&gt;.glb override models, loads each once via
    /// the GlbLoader seam, bakes a unit-normalized shared mesh with the game's unlit
    /// material, and clones idle-spinning instances on demand. Missing/failed models
    /// leave the powerup on its sprite (caller falls back).</summary>
    public sealed partial class OverrideModels : IDisposable
    {
        public static Func<GlbLoader> GlbLoaderFactory;

        readonly Shader shader;
        readonly Dictionary<int, string> paths = new Dictionary<int, string>();   // powerupId -> glb path
        readonly Dictionary<int, Baked> cache = new Dictionary<int, Baked>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();

        sealed class Baked { public Mesh Mesh; public Material[] Materials; }

        public bool AnyPresent => paths.Count > 0;

        public OverrideModels(Shader modelShader, IEnumerable<string> overrideDirs)
        {
            shader = modelShader;
            foreach (var kv in PowerupOverrides.ByPowerupId)
                foreach (var dir in overrideDirs)
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    var candidate = Path.Combine(dir, $"powerup_{kv.Value}.glb");
                    if (File.Exists(candidate)) { paths[kv.Key] = candidate; break; }
                }
        }

        public bool Has(int powerupId) => cache.ContainsKey(powerupId);

        /// <summary>Load + bake every discovered GLB once. Coroutine so it runs under
        /// the LOADING frame without blocking the main thread.</summary>
        public IEnumerator PreloadRoutine()
        {
            if (GlbLoaderFactory == null) yield break;
            var loader = GlbLoaderFactory();
            foreach (var kv in paths)
            {
                if (cache.ContainsKey(kv.Key)) continue;
                Task<GlbMesh> task;
                try { task = loader.LoadAsync(File.ReadAllBytes(kv.Value), "file://" + kv.Value.Replace('\\', '/')); }
                catch (Exception e) { Debug.LogWarning($"D1U override {kv.Value}: {e.Message}"); continue; }
                while (!task.IsCompleted) yield return null;
                if (task.IsFaulted || task.Result == null) { Debug.LogWarning($"D1U override {kv.Value}: load failed"); continue; }
                var baked = Bake(kv.Value, task.Result);
                if (baked != null) cache[kv.Key] = baked;
            }
        }

        Baked Bake(string path, GlbMesh glb)
        {
            var mesh = glb.Mesh;
            if (mesh == null || mesh.vertexCount == 0) return null;

            // apply optional manifest, then unit-normalize into the mesh
            var manifest = ReadManifest(path);
            var b = mesh.bounds;
            var (center, scale) = ModelAutoFit.Compute(
                new System.Numerics.Vector3(b.min.x, b.min.y, b.min.z),
                new System.Numerics.Vector3(b.max.x, b.max.y, b.max.z));
            scale *= Mathf.Max(0.01f, manifest.ScaleMul);
            var rot = Quaternion.Euler(manifest.RotationEuler[0], manifest.RotationEuler[1], manifest.RotationEuler[2]);
            var uCenter = new Vector3(center.X, center.Y, center.Z);

            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
                verts[i] = rot * ((verts[i] - uCenter) * scale);
            mesh.vertices = verts;                 // mutate the loader's combined mesh in place
            mesh.RecalculateBounds();
            mesh.name = "override_" + Path.GetFileNameWithoutExtension(path);
            mesh.hideFlags = HideFlags.HideAndDontSave;
            ownedMeshes.Add(mesh);                 // own it so Dispose destroys it (no leak)

            var mats = new Material[mesh.subMeshCount];
            for (int s = 0; s < mesh.subMeshCount; s++)
                mats[s] = MakeMaterial(s < glb.BaseTextures.Length ? glb.BaseTextures[s] : null,
                                       Path.GetFileNameWithoutExtension(path) + "_" + s);
            return new Baked { Mesh = mesh, Materials = mats };
        }

        Material MakeMaterial(Texture2D tex, string name)
        {
            var m = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_Cull")) m.SetInt("_Cull", 0);
            if (m.HasProperty("_AlphaClip")) { m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON"); }
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            if (tex != null) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); else m.mainTexture = tex; }
            ownedMaterials.Add(m);
            return m;
        }

        static OverrideManifest ReadManifest(string glbPath)
        {
            var json = Path.ChangeExtension(glbPath, ".json");
            try { return File.Exists(json) ? OverrideManifest.Parse(File.ReadAllText(json)) : new OverrideManifest(); }
            catch { return new OverrideManifest(); }
        }

        /// <summary>Clone a cached model at the object's on-map size, lit + spinning.
        /// Returns null if this powerup has no (loaded) override.</summary>
        public GameObject TryInstantiate(int powerupId, float radius, float light)
        {
            if (!cache.TryGetValue(powerupId, out var baked)) return null;
            var go = new GameObject($"override_pow{powerupId}");
            go.AddComponent<MeshFilter>().sharedMesh = baked.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = baked.Materials;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(light, light, light, 1f));
            mr.SetPropertyBlock(block);
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * 2f); // mesh is unit-sized
            go.AddComponent<SpinModel>();
            return go;
        }

        public void Dispose()
        {
            foreach (var m in ownedMeshes) if (m != null) UnityEngine.Object.DestroyImmediate(m);
            foreach (var m in ownedMaterials) if (m != null) UnityEngine.Object.DestroyImmediate(m);
            ownedMeshes.Clear(); ownedMaterials.Clear(); cache.Clear();
        }
    }
}
```

- [ ] **Step 2: Verify headless compile**

`SpinModel` doesn't exist yet — it's created in Task 8. Do Task 8 next, then run:

Run: `$DOTNET build unity/tools/PresentationCheck -c Release`
Expected (after Task 8): `Build succeeded`.

- [ ] **Step 3: Commit** (together with Task 8)

---

## Task 8: `SpinModel`

**Files:**
- Create: `unity/Assets/Scripts/Presentation/SpinModel.cs`

**Interfaces:**
- Produces: `class SpinModel : MonoBehaviour { public float RevsPerSecond = 0.15f; public Vector3 Axis = Vector3.up; }`

- [ ] **Step 1: Implement**

`unity/Assets/Scripts/Presentation/SpinModel.cs`:

```csharp
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>Gentle idle rotation for override powerup models (frame-rate
    /// independent). Default ~0.15 rev/s about world up.</summary>
    public sealed class SpinModel : MonoBehaviour
    {
        public float RevsPerSecond = 0.15f;
        public Vector3 Axis = Vector3.up;

        void Update() => transform.Rotate(Axis, RevsPerSecond * 360f * Time.deltaTime, Space.World);
    }
}
```

- [ ] **Step 2: Verify headless compile**

Run: `$DOTNET build unity/tools/PresentationCheck -c Release`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add unity/Assets/Scripts/Presentation/OverrideModels.cs unity/Assets/Scripts/Presentation/SpinModel.cs
git commit -m "OverrideModels loader/cache/instantiate + SpinModel

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Wire `OverrideModels` into `LevelViewer`

**Files:**
- Modify: `unity/Assets/Scripts/Presentation/LevelViewer.cs`

**Interfaces:**
- Consumes: `OverrideModels`, `SpinModel`.

- [ ] **Step 1: Add fields**

After `ModelFactory modelFactory;` (L47) add:

```csharp
        OverrideModels overrideModels;
        bool overridesPreloaded, overridePreloadStarted;
```

- [ ] **Step 2: Add the preload gate + helpers**

Replace the `buildQueued` block in `Update` (L793–798):

```csharp
            if (buildQueued)
            {
                if (Application.isPlaying && !EnsureOverridesReady())
                    return; // still preloading; the LOADING frame stays up
                buildQueued = false;
                Build(); // the LOADING frame was drawn by OnGUI last frame
                return;
            }
```

Add these methods near `Build()`:

```csharp
        bool EnsureOverridesReady()
        {
            if (overridesPreloaded) return true;
            if (overrideModels == null)
                overrideModels = new OverrideModels(
                    Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"),
                    OverrideModelDirs());
            if (!overrideModels.AnyPresent) { overridesPreloaded = true; return true; }
            if (!overridePreloadStarted)
            {
                overridePreloadStarted = true;
                StartCoroutine(RunOverridePreload());
            }
            return false;
        }

        System.Collections.IEnumerator RunOverridePreload()
        {
            yield return overrideModels.PreloadRoutine();
            overridesPreloaded = true;
        }

        System.Collections.Generic.IEnumerable<string> OverrideModelDirs()
        {
            yield return Path.Combine(Application.streamingAssetsPath, "overrides", "models");
        }
```

- [ ] **Step 3: Guard the dynamic dispatch site (`CreateObjectView`, L596)**

Prepend a powerup-override branch to the existing model/sprite `if`. Change the line `if (obj.ModelNum >= 0 && obj.ModelNum < baseDxuData.Models.Count)` (L596) into `else if (...)`, and insert the new `if` immediately before it (right after `GameObject view = null;` at L594):

```csharp
            if (obj.Type == 7 && overrideModels != null &&
                overrideModels.TryInstantiate(obj.SubId, obj.Size, light) is {} ov)
            {
                view = ov;
            }
            else if (obj.ModelNum >= 0 && obj.ModelNum < baseDxuData.Models.Count)
```

Nothing else changes: `view` is parented/positioned below as before, and the `obj.Type == 5` weapon-orientation line does not touch powerups (type 7). The powerup id for dynamic objects is `obj.SubId`; radius is `obj.Size`. This single edit is unambiguous — no brace matching required.

- [ ] **Step 4: Guard the static dispatch site (`PopulateObjects`, L392)**

In the `else if (visual.Kind == ObjectVisualKind.Sprite ...)` branch (L392), insert at its top:

```csharp
                    if (obj.Type == 7 && overrideModels != null &&
                        overrideModels.TryInstantiate(obj.SubtypeId, obj.Size, light) is {} ov)
                    {
                        ov.transform.SetParent(parent.transform, false);
                        ov.transform.position = ToUnity(obj.Position);
                        modelCount++;
                        continue;
                    }
```

(`obj.SubtypeId` is the powerup id in the static path; `continue` skips the sprite creation.)

- [ ] **Step 5: Dispose**

Where `modelFactory = null;` on teardown (L1963), add:

```csharp
            overrideModels?.Dispose();
            overrideModels = null;
            overridesPreloaded = overridePreloadStarted = false;
```

- [ ] **Step 6: Verify headless compile**

Run: `$DOTNET build unity/tools/PresentationCheck -c Release`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add unity/Assets/Scripts/Presentation/LevelViewer.cs
git commit -m "Wire OverrideModels into LevelViewer (preload gate + both dispatch sites)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Runtime verification with a stand-in GLB

Prove the whole pipeline end-to-end before any AI art exists, using the `TestGlb` triangle as `powerup_laser.glb`.

**Files:**
- Create: `unity/Assets/StreamingAssets/overrides/models/powerup_laser.glb` (generated, temporary)

- [ ] **Step 1: Write the stand-in GLB**

Add a temporary menu item to `GltfSpike.cs`:

```csharp
    [MenuItem("D1U/Spike: Write StreamingAssets laser stand-in")]
    public static void WriteLaserStandin()
    {
        var dir = Path.Combine(Application.streamingAssetsPath, "overrides", "models");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "powerup_laser.glb"), TestGlb.Triangle());
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("wrote powerup_laser.glb stand-in");
    }
```

Run it (menu **D1U ▸ Spike: Write StreamingAssets laser stand-in**).

- [ ] **Step 2: Play and observe**

Enter Play mode, start First Strike level 1 (which contains a laser powerup), fly to it.
Expected: the laser powerup renders as the (tiny red triangle) override mesh, **spinning**, instead of the sprite; other powerups still show sprites; no console errors. This confirms discovery → preload → dispatch → spin.

- [ ] **Step 3: Prove fallback**

Delete `powerup_laser.glb`, re-enter Play.
Expected: the laser powerup is back to its sprite; no errors.

- [ ] **Step 4: Regression + headless**

Run: `$DOTNET run --project unity/tools/Smoke -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs` → `SMOKE OK`.
Run: `$DOTNET build unity/tools/PresentationCheck -c Release` → `Build succeeded`.

- [ ] **Step 5: Commit (pipeline only; no binary asset)**

Do **not** commit the stand-in `.glb`. Add `unity/Assets/StreamingAssets/overrides/models/*.glb` handling is deferred to real art (Task 12). Commit only the spike menu addition:

```bash
git add unity/Assets/Scripts/EditorTools/GltfSpike.cs
git commit -m "Add laser stand-in spike; verify override pipeline end-to-end

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: `RefKit` tool — AI-generator reference kits

Formalizes the throwaway extractor into a committed tool that emits, per powerup, the images + spec an AI image-to-3D generator needs.

**Files:**
- Create: `unity/tools/RefKit/RefKit.csproj`
- Create: `unity/tools/RefKit/Program.cs`

**Interfaces:**
- Consumes: `BaseArchives`, `TextureDecoder`, `PowerupOverrides`.
- Produces: `overrides/refkit/<name>/{frames.png, view_broadside.png, view_quarter.png, view_muzzle.png, spec.txt}`.

- [ ] **Step 1: Create the csproj**

`unity/tools/RefKit/RefKit.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>D1U.RefKit</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\LibDescent.Data\LibDescent.Data.csproj" />
    <ProjectReference Include="..\D1U.Convert\D1U.Convert.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Port the extractor**

`unity/tools/RefKit/Program.cs` — reuse the validated scratchpad extractor (dumps each powerup's turntable montage + upscaled key frames + a `spec.txt` listing bitmap name, `Powerup.Size`, frame count, and the drop-in contract). Iterate over `PowerupOverrides.ByPowerupId`; for each, resolve `pig.Powerups[id].VClipNum` → frames → PNGs. Use the minimal PNG writer (CRC32 + Adler32 + stored/deflate) and the alpha-composited montage from the extractor. Emit `spec.txt` containing:

```
powerup <id> <name>: bitmap '<frame0 name>' <W>x<H>, <N> frames, Powerup.Size <size>
target: +Y up, +Z forward; any scale (auto-fit to on-map diameter <2*size>); GLB with embedded base-color texture
filename: powerup_<name>.glb  (optional powerup_<name>.json: {"rotationEuler":[x,y,z],"scaleMul":1,"keepPbr":false})
```

- [ ] **Step 3: Run for the laser**

Run: `$DOTNET run --project unity/tools/RefKit -c Release -- C:/Users/Yermak/Projects/dxx-redux/d1/hogs overrides/refkit laser`
Expected: `overrides/refkit/laser/` contains `frames.png`, three `view_*.png`, `spec.txt`.

- [ ] **Step 4: Eyeball**

Open `frames.png` — confirm it shows the laser-cannon turntable clearly (reddish body, gray barrel, stripe).

- [ ] **Step 5: Commit**

```bash
git add unity/tools/RefKit/
git commit -m "Add RefKit tool: per-powerup AI-generator reference kits

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: Laser end-to-end (USER: generate GLB)

- [ ] **Step 1 (USER):** Feed `overrides/refkit/laser/` (frames.png + view_*.png + spec.txt) to the AI generator (Meshy/Tripo/Rodin). Export **GLB with embedded base-color texture**.
- [ ] **Step 2 (USER→me):** Place the result at `unity/Assets/StreamingAssets/overrides/models/powerup_laser.glb`.
- [ ] **Step 3:** Play First Strike L1, approach the laser powerup. Confirm it renders as the 3D cannon, correctly scaled, idle-spinning.
- [ ] **Step 4 (if mis-oriented/scaled):** Add `powerup_laser.json` with `rotationEuler`/`scaleMul`; re-check. (Manifest already parsed by Task 6/7.)
- [ ] **Step 5:** Side-by-side check vs. the original sprite (temporarily remove the GLB to compare) — confirm "as close as possible."
- [ ] **Step 6:** Commit the model as original content:

```bash
git add unity/Assets/StreamingAssets/overrides/models/powerup_laser.glb unity/Assets/StreamingAssets/overrides/models/powerup_laser.json
git commit -m "Add laser 3D override model (AI-generated original content)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 13 (optional/stretch): carved base mesh for AI seeding

Add a `--carve` mode to RefKit: shape-from-silhouette over the turntable's known angles → a rough `base.glb` (positions + projected pixel texture) written into each refkit folder, as an extra structural seed for the generator and to synthesize a top view. Not shipped in-game. Skip unless the image-only kits prove insufficient.

---

## Task 14: Roll out remaining powerups (grouped)

For each powerup below, the procedure is identical to Task 11–12: `RefKit` produces the kit → USER generates the GLB → drop `powerup_<name>.glb` (+ optional `.json`) into StreamingAssets → verify spin/scale/look in Play → commit. No new code. Any id left without a GLB simply stays a sprite.

- [ ] **Primaries:** vulcan(13), spread(14), plasma(15), fusion(16), quad(12)
- [ ] **Secondaries/ammo:** cmissile(10), cmissile4(11), hmissile(18), hmissile4(19), pbomb(17), smart(20), mega(21), vammo(22)
- [ ] **Orbs:** energy(1), shield(2)
- [ ] **Keys:** key_blue(4), key_red(5), key_gold(6) — keep colour-identifiable
- [ ] **Utility:** cloak(23), invuln(25), extralife(0)
- [ ] **Decision:** for the 4-packs (11, 19), either supply distinct GLBs or delete their table rows to let them reuse the singles (they then fall back through to the single model's sprite if no 4-pack GLB — acceptable). Confirm behaviour with the user.

Commit per group.

---

## Task 15: Modding dir + docs + polish

**Files:**
- Modify: `unity/Assets/Scripts/Presentation/LevelViewer.cs` (`OverrideModelDirs`)
- Create: `unity/Assets/StreamingAssets/overrides/README.md`

- [ ] **Step 1: Add an external mod dir (first-match-wins before StreamingAssets)**

In `OverrideModelDirs()` prepend a user-writable path:

```csharp
        System.Collections.Generic.IEnumerable<string> OverrideModelDirs()
        {
            var local = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            yield return Path.Combine(local, "D1XUnity", "overrides", "models");
            yield return Path.Combine(Application.streamingAssetsPath, "overrides", "models");
        }
```

`OverrideModels` already returns on the first existing file per id, so the user dir overrides the shipped pack.

- [ ] **Step 2: Document the drop-in contract**

Write `unity/Assets/StreamingAssets/overrides/README.md` describing filenames (`powerup_<name>.glb`), the id→name table, auto-fit, the optional `.json` manifest, and the mod dir. Mirror spec §3/§7.

- [ ] **Step 3: Verify + commit**

Run: `$DOTNET build unity/tools/PresentationCheck -c Release` → `Build succeeded`.

```bash
git add unity/Assets/Scripts/Presentation/LevelViewer.cs unity/Assets/StreamingAssets/overrides/README.md
git commit -m "Override models: external mod dir + drop-in docs

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:** §2 goals → Tasks 7/9 (mesh + spin + fallback), §3 set → Task 4, §4 decisions: sourcing → Tasks 11–14, format/glTFast → Tasks 1/3, spin → Task 8, materials rebind → Task 7 `MakeMaterial`, sprite fallback → Task 9 guards, auto-fit → Tasks 5/7, scope=all 22 → Task 4. §5 hooks → Task 9. §6 components → Tasks 3–8, 11. §7 contract → Tasks 6/11/12. §8 dirs → Tasks 9/15. §9 error handling → Task 7 (try/catch → skip → sprite). §10 verification → Tasks 2,3,10 + Smoke/PresentationCheck throughout. §11 milestones F0→Tasks 1–3, F1→4–10, F2→11–13, F3→14, F4→15. No gaps.

**Placeholder scan:** the only intentionally user-driven steps are Task 12/14 (AI generation is external per the locked decision) and Task 13 (explicit optional stretch); all code steps contain complete code. Note the inline correction in Task 2 Step 3 (the `F(x,y,z)` helper must be split into single-arg calls) — apply it as written.

**Type consistency:** `GlbLoader.LoadAsync`/`GlbMesh{Mesh,BaseTextures}` (Task 3) match usage in `GltfastGlbLoader` and `OverrideModels.Bake` (Tasks 3/7). `PowerupOverrides.ByPowerupId` (Task 4) used in Task 7 ctor + Task 11. `ModelAutoFit.Compute` signature (Task 5) matches Task 7 call. `OverrideManifest{RotationEuler,ScaleMul,KeepPbr}` (Task 6) matches Task 7 `Bake`. `OverrideModels.TryInstantiate(powerupId,radius,light)` (Task 7) matches both guards (Task 9). `GlbLoaderFactory` declared in the Task 3 stub and the Task 7 full class (same `public static Func<GlbLoader>`), set by Task 3 registration.
