using System.Collections.Generic;
using UnityEngine;

// Drop onto an empty GameObject in the WorldGenPrototyping scene.
// Assign VoxelCullerShader and VoxelInstancedShader in the Inspector (same assets as WorldGenPrototypingController).
// Press R in Play mode or use right-click → Regenerate to rebuild.
public class CaveGenPrototypingController : PrototypingControllerBase
{
    [Header("Cave Entry Point")]
    public int Seed          = 42;
    public int TerrainHeight = 30; // Y at which the cave starts

    [Header("Grid")]
    [Tooltip("Half-size of the voxel grid per axis. Grid spans 2×GridHalfSize voxels on each axis. Increase if the cave gets clipped, decrease for faster regeneration.")]
    public int MaxGridHalfSize = 128;

    [Header("Cave Parameters")]
    public WormCaveParams Params = WormCaveParams.Default;

    [Header("Rendering")]
    public ComputeShader VoxelCullerShader;
    public Shader        VoxelInstancedShader;

    // Index 0 = air (skipped by shader), 1 = cave rock, 2–4 = crystal colours.
    private static readonly Color[] DisplayColors =
    {
        Color.clear,
        new(0.72f, 0.68f, 0.62f), // 1 cave rock
        new(1.00f, 0.85f, 0.10f), // 2 yellow crystal
        new(0.90f, 0.15f, 0.10f), // 3 red crystal
        new(0.15f, 0.40f, 0.95f), // 4 blue crystal
    };

    private readonly VoxelPrototypingRenderer _renderer = new();

    private float _regenCountdown = -1f;
    private const float RegenDelay = 0.4f; // seconds after last change before regenerating

    void OnEnable()
    {
        if (Application.isPlaying)
            Regenerate();
    }

    void OnDisable() => _renderer.Release();

    // Called by Unity whenever any serialized field is changed in the Inspector.
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        _regenCountdown = RegenDelay;
    }

    void Update()
    {
        _renderer.Draw();

        if (Input.GetKeyDown(KeyCode.R))
            Regenerate();

        if (_regenCountdown >= 0f)
        {
            _regenCountdown -= Time.deltaTime;
            if (_regenCountdown < 0f)
                Regenerate();
        }
    }

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        _renderer.Release();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var generator = new WormCaveGenerator(Seed, Params);
        var rng       = new System.Random(Seed);

        // Collect all voxels emitted by the cave gen (global positions).
        // type == 0  → carved air;  type > 0 → crystal
        var carved   = new HashSet<Vector3Int>();
        var crystals = new Dictionary<Vector3Int, ushort>();

        generator.GenerateCaveRaw(0, 0, TerrainHeight, rng, (pos, type) =>
        {
            if (type == 0)
                carved.Add(pos);
            else
                crystals[pos] = type;  // crystals overwrite air if both land on same cell
        });

        sw.Stop();
        Debug.Log($"[CavePrototyping] Cave simulation: {carved.Count:N0} carved, {crystals.Count:N0} crystals  ({sw.Elapsed.TotalMilliseconds:F0} ms)");

        if (carved.Count == 0)
        {
            Debug.LogWarning("[CavePrototyping] No voxels generated. Check Params values.");
            return;
        }

        sw.Restart();
        var caveBounds = BuildAndDispatch(carved, crystals);
        sw.Stop();
        Debug.Log($"[CavePrototyping] Grid + GPU dispatch: {sw.Elapsed.TotalMilliseconds:F0} ms");

        FrameCamera(caveBounds);
    }

    private void FrameCamera(Bounds caveBounds)
    {
        var cam = Camera.main;
        if (cam == null) return;

        float distance = caveBounds.size.magnitude * 0.75f;

        var orbit = cam.GetComponent<OrbitCamera>();
        if (orbit != null)
            orbit.Frame(caveBounds.center, distance);
        else
        {
            cam.transform.position = caveBounds.center + new Vector3(0f, caveBounds.size.y * 0.15f, -distance);
            cam.transform.LookAt(caveBounds.center);
        }
    }

    // ── Grid building & GPU ───────────────────────────────────────────────────

    private Bounds BuildAndDispatch(HashSet<Vector3Int> carved, Dictionary<Vector3Int, ushort> crystals)
    {
        // Find bounding box of all cave voxels.
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        int minZ = int.MaxValue, maxZ = int.MinValue;

        foreach (var p in carved)
        {
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
        }
        foreach (var p in crystals.Keys)
        {
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
        }

        const int margin = 2;
        int rawSideX = (maxX - minX) + 2 * margin + 1;
        int rawSideY = (maxY - minY) + 2 * margin + 1;
        int rawSideZ = (maxZ - minZ) + 2 * margin + 1;

        int cap    = MaxGridHalfSize * 2;
        int sideX  = Mathf.Min(rawSideX, cap);
        int sideY  = Mathf.Min(rawSideY, cap);
        int sideZ  = Mathf.Min(rawSideZ, cap);

        if (rawSideX > cap || rawSideY > cap || rawSideZ > cap)
            Debug.LogWarning($"[CavePrototyping] Cave exceeds grid ({rawSideX}×{rawSideY}×{rawSideZ}). Increase MaxGridHalfSize or reduce cave life/steps. Clipping applied.");

        var gridMin = new Vector3Int(minX - margin, minY - margin, minZ - margin);

        // Sparse grid: only cave wall voxels (solid neighbours of carved) + crystals.
        // Leaving the rest as 0 (air) avoids a grey bounding-box artifact.
        int total = sideX * sideY * sideZ;
        var grid  = new uint[total];

        // Determine crystal-type → display-index mapping.
        // WormCaveGenerator uses GetBlockTypeId for its three crystal types.
        ushort yellowId = BlockDataRepository.GetBlockTypeId("YellowLightblock");
        ushort redId    = BlockDataRepository.GetBlockTypeId("RedLightblock");
        ushort blueId   = BlockDataRepository.GetBlockTypeId("BlueLightblock");

        // Cave walls: solid voxels adjacent to carved air.
        var dirs6 = new[]
        {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up,    Vector3Int.down,
            new Vector3Int(0,0,1), new Vector3Int(0,0,-1),
        };

        foreach (var carvedPos in carved)
        {
            foreach (var d in dirs6)
            {
                var wall = carvedPos + d;
                if (carved.Contains(wall)) continue;  // skip if also carved
                var lp = wall - gridMin;
                if ((uint)lp.x >= (uint)sideX || (uint)lp.y >= (uint)sideY || (uint)lp.z >= (uint)sideZ) continue;
                int idx = lp.x + lp.z * sideX + lp.y * sideX * sideZ;
                grid[idx] = 1u; // cave rock colour index
            }
        }

        // Crystals — map real block type IDs to display colour indices.
        foreach (var kvp in crystals)
        {
            var lp = kvp.Key - gridMin;
            if ((uint)lp.x >= (uint)sideX || (uint)lp.y >= (uint)sideY || (uint)lp.z >= (uint)sideZ) continue;
            int idx = lp.x + lp.z * sideX + lp.y * sideX * sideZ;

            uint displayType;
            if      (kvp.Value == yellowId && yellowId != 0) displayType = 2u;
            else if (kvp.Value == redId    && redId    != 0) displayType = 3u;
            else if (kvp.Value == blueId   && blueId   != 0) displayType = 4u;
            else                                              displayType = 2u;

            grid[idx] = displayType;
        }

        _renderer.Dispatch(VoxelCullerShader, grid, sideX, sideY, sideZ, gridMin);
        uint instances = _renderer.SetupRendering(VoxelInstancedShader, DisplayColors);
        Debug.Log($"[CavePrototyping] Grid {sideX}×{sideY}×{sideZ}  WorldMin=({gridMin.x},{gridMin.y},{gridMin.z})  Seed={Seed}  Surface={instances:N0}");

        var worldCenter = new Vector3(gridMin.x + sideX * 0.5f, gridMin.y + sideY * 0.5f, gridMin.z + sideZ * 0.5f);
        var worldSize   = new Vector3(sideX, sideY, sideZ);
        return new Bounds(worldCenter, worldSize);
    }

    [ContextMenu("Reset Params to Default")]
    public void ResetParamsToDefault() => Params = WormCaveParams.Default;
}
