using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RushedCrops.Editor
{
    // Builds the saved map (editor only). Farm and spawn south-west, market in the centre, logging camp east,
    // quarry north over the only river bridge. Wooded hills ring the valley about ten metres past the last
    // place worth visiting; the player is stopped at their foot and a ridge behind hides the world's edge.
    // X is east, Z is north.
    public static class FarmWorldBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/SC_Farm.unity";
        public const string RootName = "WORLD_RushedCrops";
        public const string TerrainPath = "00_Terrain/Walkable valley";
        public const string HillsPath = "00_Terrain/Distant hills";
        public const string BridgePath = "02_River/River bridge";
        public const string FarmGroup = "03_Farm";
        public static readonly string[] Groups = { "00_Terrain", "02_River", FarmGroup, "04_Market", "05_Quarry", "06_LoggingCamp", "07_Forest", "08_Boundary", "09_Sky" };
        public const string WindmillName = "Windmill", CampfireName = "Campfire";

        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        // Generated map assets. The meshes folder is emptied on every rebuild, so it holds nothing else.
        private const string MeshFolder = "Assets/_Game/Models/World";
        private const string MaterialFolder = "Assets/_Game/Materials/World";
        private const int GroundLayer = 6;
        private const float FineReach = 10f;  // metres past the rim the detailed walkable terrain goes
        private const float HillReach = 56f;  // metres past the rim the coarse hills go; the ridge hides the rest
        private const float TreeReach = 26f;  // metres past the rim the forest goes, up the slope below the ridge

        private static readonly Vector2 BarnSpot = V(-41, -2);
        private static readonly Vector2 Yard = V(-36, -15);
        // Big enough for a planted boss to charge and slam with crops still growing round it.
        public static readonly Vector2 Garden = V(-24, -32);
        public static readonly Vector2 GardenHalfSize = V(12, 10); // whole metres, so the tilled soil edge follows the terrain grid
        private static readonly Vector2 Market = V(4, -6);
        private static readonly Vector2 Logging = V(42, -22);
        private static readonly Vector2 Quarry = V(10, 49);
        private static readonly Vector2 Spawn = V(-31, -13);
        private static readonly Vector2 Windmill = V(-50, 13);

        // Flat building pads: centre, radius. Terrain is exactly level inside each radius.
        private static readonly Vector3[] Pads = {
            new Vector3(-36f, -15f, 27f),   // farm: barn and yard
            new Vector3(-24f, -32f, 14f),   // crop field
            new Vector3(4f, -6f, 14f),      // market
            new Vector3(42f, -22f, 15f),    // logging camp
            new Vector3(10f, 49f, 15f),     // quarry
            new Vector3(-50f, 13f, 6f),     // windmill
        };

        // The valley's rim, clockwise from where the river leaves in the west. Its line is the edge of play.
        private static readonly Vector2[] RimGuide = {
            V(-60, 34), V(-48, 45), V(-30, 46), V(-14, 47), V(-11, 56), V(-6, 66), V(2, 72), V(10, 74), V(19, 72), V(27, 66),
            V(32, 57), V(35, 47), V(46, 41), V(60, 37), V(69, 25), V(71, 10), V(68, -8), V(67, -24), V(63, -38), V(52, -47),
            V(36, -47), V(20, -38), V(6, -35), V(-4, -48), V(-12, -54), V(-24, -55), V(-36, -54), V(-48, -48), V(-57, -36),
            V(-60, -20), V(-60, -2), V(-61, 16)
        };
        // Beyond the rim the river bends away inside a gorge, so no one sees where it ends.
        private static readonly Vector2[] RiverGuide = {
            V(-98, 92), V(-96, 62), V(-82, 40), V(-62, 35), V(-40, 29), V(-18, 27), V(6, 24), V(28, 27), V(48, 21), V(66, 25), V(84, 20), V(98, 2), V(102, -34)
        };
        private static readonly Vector2[][] RouteGuides = {
            new[] { V(-33, -14), V(-20, -11), V(-8, -8), V(4, -6) },                             // farm -> market
            new[] { V(4, -6), V(5, 5), V(6, 15), V(6, 24), V(7, 33), V(9, 41), V(10, 47) },      // market -> bridge -> quarry
            new[] { V(4, -6), V(16, -12), V(28, -18), V(42, -22) },                              // market -> logging camp
        };
        private static readonly Vector2[] River = Smooth(RiverGuide);
        private static readonly Vector2[][] Routes = SmoothRoutes(RouteGuides);
        private static readonly Vector2[] Rim = SmoothLoop(RimGuide);
        private static readonly Dictionary<long, float> Heights = new Dictionary<long, float>();

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly List<Light> Lanterns = new List<Light>();
        private static System.Random random;
        private static Transform world;

        [MenuItem("Rushed Crops/World/Rebuild map")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild map", "Delete " + RootName + " and build the farm map again? Unsaved hand edits to the map are lost.", "Rebuild", "Cancel"))
                return;
            Rebuild();
        }

        public static void Rebuild()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before authoring the map.");
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open SC_Farm before building its map.");

            var oldWorld = GameObject.Find(RootName);
            if (oldWorld != null) Object.DestroyImmediate(oldWorld);
            DeleteGeneratedMeshes();

            random = new System.Random(451);
            Materials.Clear();
            Lanterns.Clear();
            Heights.Clear();
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);
            world = new GameObject(RootName).transform;

            MakeMaterials();
            BuildGround(Group("00_Terrain"));
            BuildRiver(Group("02_River"));
            BuildFarm(Group(FarmGroup));
            BuildMarket(Group("04_Market"));
            BuildQuarry(Group("05_Quarry"));
            BuildLogging(Group("06_LoggingCamp"));
            BuildForest(Group("07_Forest"));
            BuildBoundaries(Group("08_Boundary"));
            BuildSky(Group("09_Sky"));
            GatheringSetup.Place(world, false);

            CartoonWorldStyler.Apply();
            WorldGrounding.Apply();
            PlaceOnTops();
            ConnectEnvironment();
            Optimise();

            // The template's flat ground plane, long replaced by the valley terrain.
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Ground") Object.DestroyImmediate(root);
            var player = GameObject.Find("Player");
            if (player != null)
            {
                player.transform.position = new Vector3(Spawn.x, 1.1f, Spawn.y);
                player.transform.rotation = Quaternion.Euler(0f, 150f, 0f);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = world.gameObject;
            Debug.Log("Rushed Crops: rebuilt farm map with farm, market, logging camp, quarry, one river bridge and the valley rim.");
        }

        // ---------------------------------------------------------------- materials

        private static void MakeMaterials()
        {
            Mat("Meadow", new Color(0.44f, 0.61f, 0.25f));
            Mat("MeadowLight", new Color(0.51f, 0.66f, 0.28f));
            Mat("MeadowShade", new Color(0.39f, 0.55f, 0.22f));
            Mat("MeadowGold", new Color(0.59f, 0.67f, 0.30f));
            Mat("Path", new Color(0.72f, 0.54f, 0.31f));
            Mat("PathShoulder", new Color(0.61f, 0.49f, 0.27f));
            Mat("Gravel", new Color(0.66f, 0.62f, 0.48f));
            Mat("Tilled", new Color(0.50f, 0.36f, 0.20f));
            Mat("Riverbank", new Color(0.55f, 0.53f, 0.40f));
            Mat("Water", new Color(0.13f, 0.55f, 0.67f));
            Mat("Wood", new Color(0.30f, 0.18f, 0.10f));
            Mat("Plank", new Color(0.49f, 0.31f, 0.17f));
            Mat("CanvasCream", new Color(0.92f, 0.83f, 0.62f));
            Mat("CanvasRed", new Color(0.53f, 0.19f, 0.12f));
            Mat("Moon", new Color(0.76f, 0.84f, 1f), true);
            Mat("Cloud", new Color(0.97f, 0.98f, 1f));
        }

        private static Material Mat(string name, Color colour, bool emissive = false)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", null);
            material.color = colour;
            material.SetFloat("_Smoothness", name == "Water" ? 0.65f : 0f);
            material.enableInstancing = true;
            if (emissive)
            {
                // URP keeps _EMISSION on only for materials marked as emissive for GI; otherwise it switches
                // the glow off the next time it checks the material. Realtime costs nothing without realtime GI.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * 1.4f);
            }
            EditorUtility.SetDirty(material);
            Materials[name] = material;
            return material;
        }

        // ---------------------------------------------------------------- terrain

        private enum Surface { Meadow, MeadowLight, MeadowShade, MeadowGold, Path, PathShoulder, Gravel, Tilled, Riverbank }

        private static void BuildGround(Transform parent)
        {
            int surfaceCount = Enum.GetValues(typeof(Surface)).Length;
            var vertices = new List<Vector3>();
            var triangles = new List<int>[surfaceCount];
            for (int i = 0; i < surfaceCount; i++) triangles[i] = new List<int>();

            // One-metre faceted grid over the valley and the foot of its hills. Paths, yards and soil are coloured
            // into the ground mesh itself instead of being separate decals that could float above or clip into it.
            RimBounds(FineReach + 2f, out Vector2Int min, out Vector2Int max);
            int columns = max.x - min.x, rows = max.y - min.y;
            var corners = new Surface[columns + 1, rows + 1];
            var rim = new float[columns + 1, rows + 1];
            for (int zi = 0; zi <= rows; zi++)
            for (int xi = 0; xi <= columns; xi++)
            {
                Vector2 p = V(min.x + xi, min.y + zi);
                corners[xi, zi] = SurfaceAt(p);
                rim[xi, zi] = RimDistance(p);
            }
            for (int zi = 0; zi < rows; zi++)
            for (int xi = 0; xi < columns; xi++)
            {
                if (Mathf.Min(Mathf.Min(rim[xi, zi], rim[xi + 1, zi]), Mathf.Min(rim[xi, zi + 1], rim[xi + 1, zi + 1])) > FineReach) continue;
                float x = min.x + xi, z = min.y + zi;
                GroundTriangle(vertices, triangles, V(x, z), V(x, z + 1f), V(x + 1f, z), corners[xi, zi], corners[xi, zi + 1], corners[xi + 1, zi]);
                GroundTriangle(vertices, triangles, V(x + 1f, z), V(x, z + 1f), V(x + 1f, z + 1f), corners[xi + 1, zi], corners[xi, zi + 1], corners[xi + 1, zi + 1]);
            }

            var mesh = new Mesh { name = "Farm valley terrain", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = surfaceCount;
            for (int i = 0; i < surfaceCount; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh, "ValleyTerrain");

            var surfaceMaterials = new Material[surfaceCount];
            for (int i = 0; i < surfaceCount; i++) surfaceMaterials[i] = Materials[((Surface)i).ToString()];
            var ground = MeshObject("Walkable valley", parent, mesh, surfaceMaterials);
            ground.layer = GroundLayer;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            BuildDistantHills(parent);
        }

        // Coarse hills past the walkable terrain, out to just behind the ridge and along the river's gorges.
        // Nothing further is ever in view, so nothing further is built.
        private static void BuildDistantHills(Transform parent)
        {
            const float cell = 5f;
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            RimBounds(HillReach + cell, out Vector2Int min, out Vector2Int max);
            for (float z = min.y; z < max.y; z += cell)
            for (float x = min.x; x < max.x; x += cell)
            {
                Vector2 centre = V(x + cell * 0.5f, z + cell * 0.5f);
                float rim = RimDistance(centre), river = Distance(centre, River);
                if (rim < 4f || (rim > HillReach && river > 32f)) continue;
                Vector3 a = Hill(x, z), b = Hill(x + cell, z), c = Hill(x, z + cell), d = Hill(x + cell, z + cell);
                int surface = river < 6f ? 2 : Mathf.PerlinNoise(centre.x * 0.03f + 5f, centre.y * 0.03f) > 0.5f ? 1 : 0;
                AddTriangle(vertices, triangles[surface], a, c, b);
                AddTriangle(vertices, triangles[surface], b, c, d);
            }
            var mesh = new Mesh { name = "Distant hills" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh, "DistantHills");
            var hills = MeshObject("Distant hills", parent, mesh, new[] { Materials["MeadowShade"], Materials["Meadow"], Materials["Riverbank"] });
            hills.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            hills.AddComponent<MeshCollider>().sharedMesh = mesh; // lets trees beyond the edge be grounded; the player cannot reach it
        }

        // Where the two meshes overlap the hills sit just below the walkable terrain, so the seam is hidden.
        private static Vector3 Hill(float x, float z) => new Vector3(x, Height(x, z) - 0.3f, z);

        // A triangle whose corners lie on different surfaces is split through its edge midpoints and
        // centre, so each corner keeps its own colour and boundaries run smoothly instead of saw-toothed.
        private static void GroundTriangle(List<Vector3> vertices, List<int>[] triangles, Vector2 a, Vector2 b, Vector2 c, Surface sa, Surface sb, Surface sc)
        {
            if (sa == sb && sb == sc)
            {
                AddTriangle(vertices, triangles[(int)sa], P(a), P(b), P(c));
                return;
            }
            Vector2 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f, centre = (a + b + c) / 3f;
            GroundCorner(vertices, triangles[(int)sa], a, ab, centre, ca);
            GroundCorner(vertices, triangles[(int)sb], b, bc, centre, ab);
            GroundCorner(vertices, triangles[(int)sc], c, ca, centre, bc);
        }

        private static void GroundCorner(List<Vector3> vertices, List<int> triangles, Vector2 corner, Vector2 next, Vector2 centre, Vector2 previous)
        {
            AddTriangle(vertices, triangles, P(corner), P(next), P(centre));
            AddTriangle(vertices, triangles, P(corner), P(centre), P(previous));
        }

        private static Surface SurfaceAt(Vector2 p)
        {
            // Low-frequency wobble keeps edges organic without spiky single triangles.
            float wobble = (Mathf.PerlinNoise(p.x * 0.12f + 50f, p.y * 0.12f + 50f) - 0.5f) * 1.4f;
            float river = Distance(p, River);
            if (river < 8f + wobble) return Surface.Riverbank;

            if (Inside(p, Garden, GardenHalfSize)) return Surface.Tilled;
            if (Ellipse(p, Yard + V(-2, 2), 11f, 8f, wobble)) return Surface.Path;
            if (Ellipse(p, Market, 9f, 8f, wobble)) return Surface.Path;
            if (Ellipse(p, Quarry + V(0, 2), 12f, 10f, wobble)) return Surface.Gravel;
            if (Ellipse(p, Logging, 11f, 9f, wobble)) return Surface.PathShoulder;

            float path = PathDistance(p);
            if (path < 1.9f + wobble * 0.4f) return Surface.Path;
            if (path < 2.7f + wobble * 0.6f) return Surface.PathShoulder;

            float shade = Mathf.PerlinNoise((p.x + 190f) * 0.05f, (p.y + 210f) * 0.05f);
            return shade < 0.35f ? Surface.MeadowShade : shade > 0.68f ? Surface.MeadowGold : shade > 0.52f ? Surface.MeadowLight : Surface.Meadow;
        }

        public static float Height(float x, float z)
        {
            // Cached by 1/48 m so the halves and thirds the ground mesh samples each get their own entry.
            long key = ((long)Mathf.RoundToInt(x * 48f) << 32) | (uint)Mathf.RoundToInt(z * 48f);
            if (Heights.TryGetValue(key, out float known)) return known;

            Vector2 p = V(x, z);
            float natural = (Mathf.PerlinNoise((x + 200f) * 0.03f, (z + 200f) * 0.03f) - 0.5f) * 1.6f;
            float level = 0f;
            foreach (Vector3 pad in Pads)
                level = Mathf.Max(level, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pad.z, pad.z + 9f, Vector2.Distance(p, V(pad.x, pad.y)))));
            level = Mathf.Max(level, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 9f, PathDistance(p))));
            float height = Mathf.Lerp(natural, 0f, level);

            // Past the rim the ground climbs (the player is stopped partway up the first slope) to a ridge, then
            // drops a little behind the crest so the end of the hills stays out of sight. The river cuts a gorge.
            float rim = RimDistance(p);
            float riverDistance = Distance(p, River);
            if (rim > -10f)
            {
                float slope = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-10f, 12f, rim)) * 9f;
                float crest = 8f + Mathf.PerlinNoise(x * 0.02f, z * 0.02f) * 14f;
                float ridge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 34f, rim)) * crest * (1f - 0.5f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, HillReach, rim)));
                height += (slope + ridge) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 26f, riverDistance));
            }
            if (riverDistance < 9f) height = Mathf.Min(height, Mathf.Lerp(-1.8f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 9f, riverDistance))));
            Heights[key] = height;
            return height;
        }

        // Metres outside the valley rim (the edge of play); negative inside it.
        public static float RimDistance(Vector3 p) => RimDistance(V(p.x, p.z));

        private static float RimDistance(Vector2 p)
        {
            float distance = Distance(p, Rim);
            bool inside = false;
            for (int i = 0, j = Rim.Length - 1; i < Rim.Length; j = i++)
                if ((Rim[i].y > p.y) != (Rim[j].y > p.y) && p.x < (Rim[j].x - Rim[i].x) * (p.y - Rim[i].y) / (Rim[j].y - Rim[i].y) + Rim[i].x)
                    inside = !inside;
            return inside ? -distance : distance;
        }

        // Whole-metre box round the rim, widened by margin.
        private static void RimBounds(float margin, out Vector2Int min, out Vector2Int max)
        {
            Vector2 low = Rim[0], high = Rim[0];
            foreach (Vector2 p in Rim) { low = Vector2.Min(low, p); high = Vector2.Max(high, p); }
            min = new Vector2Int(Mathf.FloorToInt(low.x - margin), Mathf.FloorToInt(low.y - margin));
            max = new Vector2Int(Mathf.CeilToInt(high.x + margin), Mathf.CeilToInt(high.y + margin));
        }

        // ---------------------------------------------------------------- river

        private static void BuildRiver(Transform parent)
        {
            Ribbon("River water", River, 7.6f, parent, Materials["Water"], -0.65f);
            RiverWaterSetup.SetUp(parent.Find("River water").gameObject);

            for (int i = 4; i < River.Length - 1; i += 7)
            {
                if (RimDistance(River[i]) > -6f) continue;
                Vector2 direction = (River[i + 1] - River[i]).normalized;
                Vector2 side = V(-direction.y, direction.x) * (i % 2 == 0 ? 1f : -1f);
                Vector2 bank = River[i] + side * R(6.2f, 7.4f);
                if (PathDistance(bank) < 7f) continue;
                Prefab("Rocks/Stone0" + random.Next(1, 4), parent, bank, R(0f, 360f), R(1.0f, 1.8f));
                if (random.NextDouble() < 0.6) Prefab("Vegetation/Cattail01", parent, River[i] + side * 6.5f, R(0f, 360f), R(1.1f, 1.5f));
            }

            Vector2 crossing = RiverGuide[6];
            int index = NearestIndex(River, crossing);
            Vector2 flow = (River[Mathf.Min(index + 1, River.Length - 1)] - River[Mathf.Max(index - 1, 0)]).normalized;
            Bridge(parent, crossing, V(-flow.y, flow.x), "River bridge");
            Lantern(parent, crossing + V(3.4f, -10f), 0f);
            Lantern(parent, crossing + V(-3.4f, 10f), 180f);
            RiverExitRapids(parent);
            RiverLife(parent);
            RiverRush(parent);
        }

        // The river's sound, sliding along its course inside the valley to stay nearest the listener.
        private const string RiverClipPath = "Assets/_Game/Audio/Ambience/River.ogg";

        private static void RiverRush(Transform parent)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RiverClipPath);
            if (clip == null) throw new InvalidOperationException("Missing " + RiverClipPath + ".");
            var course = new List<Vector3>();
            for (int i = 0; i < River.Length; i += 3)
                if (RimDistance(River[i]) < 8f) course.Add(new Vector3(River[i].x, WaterLevel, River[i].y));
            var go = new GameObject("River sound");
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.55f;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 6f;
            source.maxDistance = 32f;
            source.dopplerLevel = 0f;
            go.AddComponent<RiverSound>().Configure(course.ToArray());
        }

        // A fishing dock with a rowboat on the farm side of the river, and lily pads in the slack water by the banks.
        // These sit on the water, not the ground, so their names keep the grounding pass away ("(on ...").
        private const float WaterLevel = -0.65f;

        private static void RiverLife(Transform parent)
        {
            Vector2 shore = V(-27f, 20f);
            Vector2 centre = River[NearestIndex(River, shore)];
            Vector2 toBank = (shore - centre).normalized;
            Vector2 dockAt = centre + toBank * 6.6f; // its middle on the waterline, so half of it reaches out over the river
            var dock = Prefab("Other/Dock01", parent, dockAt, YawTowards(dockAt, centre) + 90f, 1.9f);
            dock.name = "Fishing dock (on posts)";
            dock.transform.position = new Vector3(dockAt.x, WaterLevel + 0.35f, dockAt.y);

            Vector2 along = V(-toBank.y, toBank.x);
            Vector2 boatAt = centre + toBank * 4.4f + along * 3.6f;
            var boat = Prefab("Transport/Rowboat01", parent, boatAt, YawTowards(boatAt, centre) + 75f, 1.3f);
            boat.name = "Rowboat (on water)";
            boat.transform.position = new Vector3(boatAt.x, WaterLevel - 0.12f, boatAt.y);
            boat.AddComponent<Bobbing>();

            foreach (float x in new[] { -46f, -10f, 34f, 54f })
            {
                int index = NearestIndex(River, V(x, 25f));
                Vector2 c = River[index];
                Vector2 flow = (River[Mathf.Min(index + 1, River.Length - 1)] - River[Mathf.Max(index - 1, 0)]).normalized;
                Vector2 side = V(-flow.y, flow.x) * (x > 0f ? 1f : -1f);
                for (int i = 0; i < 5; i++)
                {
                    Vector2 at = c + side * R(4.6f, 6.2f) + flow * R(-3f, 3f);
                    bool flower = i == 2;
                    var pad = Prefab(flower ? "Vegetation/LilyFlower01" : "Vegetation/LilyPad01", parent, at, R(0f, 360f), R(1.6f, 2.3f), false);
                    pad.name += " (on water)";
                    pad.transform.position = new Vector3(at.x, WaterLevel + 0.03f, at.y);
                }
            }
        }

        // Where the river leaves the valley a line of boulders crosses the banks and the shallows, so the
        // edge there looks like rapids at the mouth of a gorge rather than an invisible wall.
        private static void RiverExitRapids(Transform parent)
        {
            for (int i = 0; i < River.Length - 1; i++)
            {
                float here = RimDistance(River[i]), next = RimDistance(River[i + 1]);
                if ((here < 0f) == (next < 0f)) continue;
                Vector2 mouth = Vector2.Lerp(River[i], River[i + 1], here / (here - next));
                Vector2 outward = (River[i + 1] - River[i]).normalized * (next > 0f ? 1f : -1f);
                Vector2 across = V(-outward.y, outward.x);
                var rapids = new GameObject("River rapids").transform;
                rapids.SetParent(parent, false);
                rapids.position = At(mouth);
                foreach (float side in new[] { -10.5f, -8f, -5.2f, -2.4f, 0.4f, 3.2f, 6f, 8.5f, 11f })
                {
                    bool bank = Mathf.Abs(side) > 7.5f;
                    string model = bank ? "Rocks/Rock0" + random.Next(1, 4) : "Rocks/Stone0" + random.Next(1, 4);
                    float scale = bank ? R(1.4f, 1.8f) : Mathf.Abs(side) < 4f ? R(3.6f, 4.2f) : R(2.8f, 3.4f); // deeper water needs bigger stones to show
                    Prefab(model, rapids, mouth + across * side + outward * R(-0.6f, 1.2f), R(0f, 360f), scale);
                }
                var foam = ParticleRecipes.Create("Rapids foam", rapids, Vector3.zero);
                foam.transform.SetPositionAndRotation(new Vector3(mouth.x, WaterLevel + 0.05f, mouth.y),
                    Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y)));
                ParticleRecipes.Foam(foam, EffectMaterials.Soft(), 13f);
                foam.Play();
            }
        }

        private static void Bridge(Transform parent, Vector2 centre, Vector2 forward, string name)
        {
            Transform bridge = new GameObject(name).transform;
            bridge.SetParent(parent, false);
            bridge.position = new Vector3(centre.x, 0f, centre.y);
            bridge.rotation = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y));
            const float halfLength = 9f;
            for (int i = 0; i < 45; i++)
            {
                float z = -halfLength + 0.2f + i * 0.4f;
                float y = Mathf.Sin((z + halfLength) / (halfLength * 2f) * Mathf.PI) * 0.45f - 0.05f;
                LocalBox("Bridge plank", bridge, new Vector3(0f, y, z), new Vector3(3.6f, 0.15f, 0.39f), Materials["Plank"], true);
            }
            foreach (float x in new[] { -1.72f, 1.72f })
            {
                for (float z = -7.5f; z <= 7.5f; z += 2.5f)
                    LocalBox("Timber post", bridge, new Vector3(x, 0.1f, z), new Vector3(0.17f, 2.5f, 0.17f), Materials["Wood"], true);
                LocalBox("Handrail", bridge, new Vector3(x, 1.25f, 0f), new Vector3(0.13f, 0.15f, 15.4f), Materials["Plank"], true);
                LocalBox("Low rail", bridge, new Vector3(x, 0.75f, 0f), new Vector3(0.10f, 0.12f, 15.4f), Materials["Wood"], true);
            }
        }

        // ---------------------------------------------------------------- farm

        private static void BuildFarm(Transform parent)
        {
            // The inherited barn, saved broken until the player pays to repair it (see BarnRepair).
            var barn = Prefab("Barn01", parent, BarnSpot, 0f, 1.5f);
            barn.name = BarnRepairSetup.BarnName;
            BarnRepairSetup.SetUp(barn);

            float eastWall = BarnSpot.x + 3.5f * 1.5f;
            Prefab("Other/Ladder01", parent, V(eastWall + 0.12f, BarnSpot.y + 1f), 90f, 1.6f);
            Transform planks = new GameObject("Repair plank stack").transform;
            planks.SetParent(parent, false);
            planks.position = new Vector3(eastWall + 2.2f, 0f, BarnSpot.y - 3f);
            for (int layer = 0; layer < 3; layer++)
            for (int i = 0; i < 4 - layer; i++)
                LocalBox("Repair plank", planks, new Vector3((i - (3 - layer) * 0.5f) * 0.32f, 0.07f + layer * 0.14f, 0f), new Vector3(0.28f, 0.12f, 2.8f), Materials["Plank"], false);
            Prefab("Wood/FirewoodPile02", parent, V(BarnSpot.x - 6.2f, BarnSpot.y + 2f), 90f, 2.2f);
            Prefab("Containers/Barrel01", parent, V(BarnSpot.x - 6.1f, BarnSpot.y - 3.5f), 0f, 1.5f);
            Prefab("Containers/Barrel01", parent, V(BarnSpot.x - 6.1f, BarnSpot.y - 5f), 40f, 1.5f);
            Prefab("Other/Rack01", parent, V(BarnSpot.x + 3.6f, BarnSpot.y - 8.3f), 0f, 1.1f);
            Prefab("Transport/Wheelbarrow01", parent, V(BarnSpot.x - 4.6f, BarnSpot.y - 10.5f), 150f, 1.5f);
            Lantern(parent, V(Yard.x + 7f, Yard.y + 4f), 270f);

            FarmPlotSetup.SetUpField(parent, Garden, GardenHalfSize);
            Vector2 min = Garden - GardenHalfSize, max = Garden + GardenHalfSize;
            FenceLine(parent, V(min.x, min.y), V(max.x, min.y));
            FenceLine(parent, V(min.x, min.y), V(min.x, max.y));
            FenceLine(parent, V(max.x, min.y), V(max.x, Garden.y - 1.1f));
            FenceLine(parent, V(max.x, Garden.y + 1.1f), V(max.x, max.y));
            FenceLine(parent, V(min.x, max.y), V(Garden.x - 1.1f, max.y));
            FenceLine(parent, V(Garden.x + 1.1f, max.y), V(max.x, max.y));
            var gate = Prefab("Other/Gate01", parent, V(Garden.x, max.y), 0f, 1.08f);
            gate.name = "Garden gate";
            // A second way in from the market side, and a second way out of a boss fight.
            var eastGate = Prefab("Other/Gate01", parent, V(max.x, Garden.y), 90f, 1.08f);
            eastGate.name = "Garden gate (east)";
            // Both stand open, swung outward off the planting squares, so the player can walk in.
            foreach (var swing in new[] { gate, eastGate })
            foreach (Transform part in swing.GetComponentsInChildren<Transform>(true))
                if (part.name.StartsWith("Leaf")) part.localRotation = Quaternion.Euler(0f, -100f, 0f);
            Prefab("Containers/Bucket01", parent, V(Garden.x + 2.4f, max.y + 1.2f), 0f, 1.3f);
            Prefab("Containers/Crate01", parent, V(Garden.x - 2.6f, max.y + 1.1f), 10f, 1.6f);
            Lantern(parent, V(Garden.x + 1.6f, max.y + 0.6f), 180f);
            ToolStumpSetup.Place(parent);
            MeleeSetup.Place(parent);

            BuildWindmill(parent);
            // No bosses on the map: they only come from boss seeds planted in the crop field.
        }


        // A windmill on the meadow behind the barn, its sails turning in the breeze round their hub.
        private static void BuildWindmill(Transform parent)
        {
            var mill = Prefab("Windmill01", parent, Windmill, YawTowards(Windmill, Yard), 1.1f);
            mill.name = WindmillName;
            Transform rotor = null;
            foreach (Transform part in mill.GetComponentsInChildren<Transform>(true))
                if (part.name.StartsWith("Rotor")) { rotor = part; break; }
            if (rotor == null) throw new InvalidOperationException("The windmill has no Rotor part to turn.");

            // The sails are flat, so they turn round their thinnest side; the middle of their bounds is the hub.
            Bounds local = new Bounds();
            bool found = false;
            foreach (MeshFilter filter in rotor.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                foreach (Vector3 corner in Corners(mesh))
                {
                    Vector3 p = rotor.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!found) { local = new Bounds(p, Vector3.zero); found = true; }
                    else local.Encapsulate(p);
                }
            }
            Vector3 size = local.size;
            Vector3 axis = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
            rotor.gameObject.AddComponent<Spinner>().Configure(axis, local.center, 38f);
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }

        // ---------------------------------------------------------------- market

        private static void BuildMarket(Transform parent)
        {
            Transform stall = new GameObject("Merchant stall").transform;
            stall.SetParent(parent, false);
            stall.position = At(Market + V(0, -6.5f));
            stall.rotation = Quaternion.Euler(0f, 180f, 0f);
            for (int i = 0; i < 4; i++)
                LocalBox("Awning post", stall, new Vector3(i % 2 == 0 ? -3f : 3f, 1.5f, i < 2 ? -1.5f : 2f), new Vector3(0.16f, 3.2f, 0.16f), Materials["Wood"], true);
            for (int i = 0; i < 10; i++)
            {
                var panel = LocalBox("Striped market awning", stall, new Vector3(-2.88f + i * 0.64f, 3.15f, 0.25f), new Vector3(0.64f, 0.09f, 4.2f), Materials[i % 2 == 0 ? "CanvasCream" : "CanvasRed"], false);
                panel.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
                LocalBox("Awning valance", stall, new Vector3(-2.88f + i * 0.64f, 2.68f, -1.82f), new Vector3(0.64f, 0.4f, 0.07f), Materials[i % 2 == 0 ? "CanvasCream" : "CanvasRed"], false);
            }
            var counter = Prefab("Other/Table01", parent, Market + V(0, -6.2f), 0f, 2.2f);
            counter.name = "Stall counter";
            Prefab("Containers/CrateOpen01", parent, Market + V(-2.2f, -7.6f), 0f, 1.6f);
            Prefab("Containers/CrateOpen02", parent, Market + V(2.2f, -7.6f), 0f, 1.6f);
            Prefab("Containers/Barrel01", parent, Market + V(3.9f, -5.2f), 0f, 1.5f);
            Prefab("Containers/Crate01", parent, Market + V(-3.9f, -5.4f), 15f, 1.6f);

            Vector2 store = Market + V(-9f, 6.5f);
            Prefab("Storage01", parent, store, YawTowards(store, Market), 1.2f);
            Prefab("Transport/Cart01", parent, Market + V(9f, 3f), -110f, 1.35f);
            Lantern(parent, Market + V(-6.5f, -2.5f), 90f);
            Lantern(parent, Market + V(6.5f, -2.5f), 270f);
            MerchantSetup.PlaceMerchant(parent);
        }

        // ---------------------------------------------------------------- quarry

        private static void BuildQuarry(Transform parent)
        {
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Lerp(15f, 165f, i / 6f) * Mathf.Deg2Rad;
                Vector2 spot = Quarry + V(Mathf.Cos(angle) * 13.5f, Mathf.Sin(angle) * 11.5f + 2f);
                Prefab("Rocks/QuarryFace01", parent, spot, YawTowards(spot, Quarry) + 180f, R(1.6f, 2.0f));
            }
            // No loose boulders here: every boulder on the quarry floor can be mined (see GatheringSetup).
            Prefab("Rocks/Rubble01", parent, Quarry + V(-6f, 6f), 30f, 1.4f);
            Prefab("Rocks/Rubble01", parent, Quarry + V(6.5f, 5.5f), 200f, 1.2f);
            for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < 3 - layer; i++)
            {
                var block = Prefab("Rocks/Block03", parent, Quarry + V(5.2f + (i - (2 - layer) * 0.5f) * 1.1f, -3.5f), 90f, 2f);
                block.name += layer == 0 ? "" : " (stacked)";
                block.transform.position += Vector3.up * (0.24f + layer * 0.44f);
            }
            Prefab("Transport/Cart01", parent, Quarry + V(7f, -6.5f), 160f, 1.5f);
            PickaxeSetup.Place(parent);
            Prefab("Shed01", parent, Quarry + V(-9f, -3f), YawTowards(Quarry + V(-9f, -3f), Quarry) + 180f, 1.2f);
            Lantern(parent, Quarry + V(-3.2f, -7f), 0f);
        }

        // ---------------------------------------------------------------- logging camp

        private static void BuildLogging(Transform parent)
        {
            Vector2 shed = Logging + V(5f, 8f);
            Prefab("Shed01", parent, shed, YawTowards(shed, Logging) + 180f, 1.35f);
            Prefab("Wood/LogPile03", parent, Logging + V(0f, -7.5f), 90f, 2.6f);
            Prefab("Wood/LogPile02", parent, Logging + V(8.5f, -5f), 0f, 2.4f);
            Prefab("Other/Sawbuck01", parent, Logging + V(6.5f, 1f), 90f, 1.9f);
            Prefab("Wood/ChoppingBlock01", parent, Logging + V(4.5f, -2.5f), 0f, 2f);
            Prefab("Wood/FirewoodPile03", parent, shed + V(-3.6f, 0f), 0f, 2.2f);
            Prefab("Transport/Cart01", parent, Logging + V(-4f, 6f), 80f, 1.5f);
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45f + R(-12f, 12f)) * Mathf.Deg2Rad;
                Vector2 spot = Logging + V(Mathf.Cos(angle), Mathf.Sin(angle)) * R(11.5f, 13.5f);
                if (PathDistance(spot) < 4f) continue;
                Prefab("Vegetation/PineStump0" + random.Next(1, 3), parent, spot, R(0f, 360f), R(1.6f, 2.1f));
            }
            Lantern(parent, Logging + V(-3f, -7.5f), 0f);

            // The loggers' camp: a tent and a campfire with flames, smoke and a flickering light.
            Vector2 fire = Logging + V(-2f, -1f), tent = Logging + V(-6.5f, -4.5f);
            Prefab("Camping/Tent01", parent, tent, YawTowards(tent, fire), 1.3f);
            var campfire = Prefab("Camping/Campfire01", parent, fire, 0f, 2.1f);
            campfire.name = CampfireName;
            var flames = ParticleRecipes.Create("Flames", campfire.transform, Vector3.up * 0.12f);
            ParticleRecipes.Fire(flames, EffectMaterials.Glow());
            var smoke = ParticleRecipes.Create("Smoke", campfire.transform, Vector3.up * 0.6f);
            ParticleRecipes.Smoke(smoke, EffectMaterials.Soft());
            var glow = new GameObject("Fire light").AddComponent<Light>();
            glow.transform.SetParent(campfire.transform, false);
            glow.transform.localPosition = Vector3.up * 0.7f;
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.2f);
            glow.range = 9f;
            glow.shadows = LightShadows.None;
            campfire.AddComponent<FireFlicker>().Configure(glow);
        }

        // ---------------------------------------------------------------- forest and meadows

        private static void BuildForest(Transform parent)
        {
            // Only conifers: the round-crowned oaks at the logging camp are the trees a hatchet can fell
            // (see GatheringSetup), so no look-alikes stand anywhere else.
            string[] conifer = { "Pine01", "Pine02", "Spruce01", "Spruce02" };
            // Thick on the hills round the rim, in groves inside, and none past where the ridge hides them.
            RimBounds(TreeReach, out Vector2Int min, out Vector2Int max);
            for (float z = min.y; z < max.y; z += 4.6f)
            for (float x = min.x; x < max.x; x += 4.6f)
            {
                Vector2 p = V(x + R(-1.8f, 1.8f), z + R(-1.8f, 1.8f));
                float rim = RimDistance(p);
                if (rim > TreeReach || Blocked(p, 4f, 5.5f, 8.5f)) continue;
                float grove = Mathf.PerlinNoise(p.x * 0.045f + 11f, p.y * 0.045f + 7f);
                float woods = Vector2.Distance(p, Logging) < 30f ? 0.85f : 0f;
                float chance = rim > 10f ? 0.7f : rim > -12f ? 0.92f : Mathf.Max(woods, grove > 0.58f ? 0.75f : 0.06f);
                if (random.NextDouble() > chance) continue;
                Prefab("Vegetation/" + conifer[random.Next(conifer.Length)], parent, p, R(0f, 360f), rim > 6f ? R(1.6f, 2.3f) : R(1.3f, 2.0f));
            }
            for (int group = 0; group < 6; group++)
            {
                Vector2 centre = InValley(8f);
                if (Blocked(centre, 6f, 7f, 10f)) continue;
                for (int i = 0; i < 3; i++)
                    Prefab("Rocks/Stone0" + random.Next(1, 4), parent, centre + V(R(-1.6f, 1.6f), R(-1.6f, 1.6f)), R(0f, 360f), R(1.2f, 2.6f) / (i + 1));
            }
            string[] small = { "Grass01", "Grass02", "Fern01", "Bush01" };
            for (int i = 0; i < 340; i++)
            {
                Vector2 p = InValley(3f);
                if (Blocked(p, 1.5f, 3.4f, 7f)) continue;
                Prefab("Vegetation/" + small[random.Next(small.Length)], parent, p, R(0f, 360f), R(0.8f, 1.4f), false);
            }
            foreach (Vector3 pad in new[] { Pads[0], Pads[2] }) // farm and market
            for (int i = 0; i < 26; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                Vector2 p = V(pad.x, pad.y) + V(Mathf.Cos(angle), Mathf.Sin(angle)) * R(pad.z - 3f, pad.z + 4f);
                if (Blocked(p, -1f, 3.2f, 7f) || Vector2.Distance(p, BarnSpot) < 10f) continue;
                Prefab("Vegetation/Flowers0" + random.Next(1, 4), parent, p, R(0f, 360f), R(1f, 1.5f), false);
            }
        }

        // True when a spot is inside a landmark clearing, too near a path, or too near the river.
        private static bool Blocked(Vector2 p, float clearingMargin, float pathMargin, float riverMargin)
        {
            if (PathDistance(p) < pathMargin || Distance(p, River) < riverMargin) return true;
            if (Inside(p, Garden, GardenHalfSize + V(2f, 2f))) return true;
            foreach (Vector3 pad in Pads)
                if (Vector2.Distance(p, V(pad.x, pad.y)) < pad.z - 4f + clearingMargin) return true;
            return false;
        }

        // A random spot at least inset metres inside the rim.
        private static Vector2 InValley(float inset)
        {
            RimBounds(0f, out Vector2Int min, out Vector2Int max);
            while (true)
            {
                Vector2 p = V(R(min.x, max.x), R(min.y, max.y));
                if (RimDistance(p) < -inset) return p;
            }
        }

        // Walls end to end along the rim, partway up the first slope, so the player is stopped among the trees
        // at the foot of the hills rather than in the open.
        private static void BuildBoundaries(Transform parent)
        {
            const float length = 5f;
            var corners = new List<Vector2> { Rim[0] };
            for (int i = 1; i < Rim.Length; i++)
                if (Vector2.Distance(corners[corners.Count - 1], Rim[i]) >= length || i == Rim.Length - 1) corners.Add(Rim[i]);
            for (int i = 0; i < corners.Count - 1; i++)
            {
                Vector2 from = corners[i], to = corners[i + 1], middle = (from + to) * 0.5f;
                if (to == from) continue;
                var go = new GameObject("Map edge collider");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(new Vector3(middle.x, Height(middle.x, middle.y) + 10f, middle.y),
                    Quaternion.LookRotation(new Vector3(to.x - from.x, 0f, to.y - from.y)));
                go.AddComponent<BoxCollider>().size = new Vector3(1.5f, 50f, Vector2.Distance(from, to) + 1f);
            }
        }

        // ---------------------------------------------------------------- sky and air

        // Low-poly clouds drifting overhead, and pollen by day and fireflies by night round the player.
        private static void BuildSky(Transform parent)
        {
            var crown = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/Models/Cartoon/Crown.asset");
            Mesh puff = crown != null ? crown : Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var clouds = new GameObject("Clouds").transform;
            clouds.SetParent(parent, false);
            clouds.position = new Vector3(5f, 0f, 10f);
            for (int i = 0; i < 18; i++)
            {
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(clouds, false);
                cloud.position = new Vector3(5f + R(-165f, 165f), R(62f, 84f), 10f + R(-165f, 165f));
                cloud.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                int puffs = random.Next(4, 8);
                for (int p = 0; p < puffs; p++)
                {
                    var blob = MeshObject("Cloud puff", cloud, puff, new[] { Materials["Cloud"] });
                    float side = (p - (puffs - 1) * 0.5f) * R(4.5f, 6f);
                    blob.transform.localPosition = new Vector3(side, R(-1f, 2.2f) * (1f - Mathf.Abs(side) / 20f), R(-3f, 3f));
                    blob.transform.localScale = new Vector3(R(9f, 14f), R(4f, 7f), R(7f, 11f)) * (1f - Mathf.Abs(side) / 40f);
                    blob.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            clouds.gameObject.AddComponent<CloudDrift>();

            var life = new GameObject("Ambient life").transform;
            life.SetParent(parent, false);
            var motes = ParticleRecipes.Create("Pollen", life, Vector3.up * 2f);
            ParticleRecipes.Motes(motes, EffectMaterials.Soft());
            var fireflies = ParticleRecipes.Create("Fireflies", life, Vector3.up * 1.2f);
            ParticleRecipes.Fireflies(fireflies, EffectMaterials.Glow());
            life.gameObject.AddComponent<AmbientLife>().Configure(motes, fireflies);
        }

        // ---------------------------------------------------------------- drawing cost

        // Scenery that never moves is marked for static batching, so the GPU draws it in big shared batches, and
        // small plants and stones are culled once they shrink to a few pixels (they cast no shadows either).
        // Anything that moves, falls, grows, opens or can be picked up is left alone.
        private static void Optimise()
        {
            int batched = 0, culled = 0;
            foreach (Renderer renderer in world.GetComponentsInChildren<Renderer>(true))
            {
                if (Moves(renderer.transform)) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.BatchingStatic);
                batched++;
            }
            foreach (string group in new[] { "07_Forest", "02_River", FarmGroup, "04_Market" })
            foreach (Transform item in world.Find(group))
            {
                bool plant = item.name.StartsWith("Grass") || item.name.StartsWith("Fern") || item.name.StartsWith("Bush")
                    || item.name.StartsWith("Flower") || item.name.StartsWith("Cattail") || item.name.StartsWith("Lily");
                bool pebble = item.name.StartsWith("Stone0");
                if (!plant && !pebble) continue;
                var shown = new List<Renderer>();
                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy) shown.Add(renderer);
                if (shown.Count == 0) continue;
                var lod = item.GetComponent<LODGroup>();
                if (lod == null) lod = item.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(plant ? 0.02f : 0.008f, shown.ToArray()) });
                lod.enabled = true;
                lod.RecalculateBounds();
                if (plant) foreach (Renderer renderer in shown) renderer.shadowCastingMode = ShadowCastingMode.Off;
                culled++;
            }
            Debug.Log($"Rushed Crops: {batched} scenery renderers batched, {culled} small plants and stones culled at a distance.");
        }

        private static bool Moves(Transform t) =>
            t.GetComponentInParent<ResourceNode>(true) != null || t.GetComponentInParent<FarmField>(true) != null
            || t.GetComponentInParent<BarnRepair>(true) != null || t.GetComponentInParent<ToolPickup>(true) != null
            || t.GetComponentInParent<Merchant>(true) != null || t.GetComponentInParent<Spinner>(true) != null
            || t.GetComponentInParent<Bobbing>(true) != null || t.GetComponentInParent<CloudDrift>(true) != null
            || t.GetComponentInParent<ParticleSystem>(true) != null || t.GetComponentInParent<WaterSource>(true) != null;

        // ---------------------------------------------------------------- finishing

        // Items that rest on other props rather than on the ground.
        private static void PlaceOnTops()
        {
            Transform market = world.Find("04_Market");
            Transform counter = market.Find("Stall counter");
            if (counter != null && Bounds(counter, out Bounds top))
            {
                const string crops = "Assets/_Game/Prefabs/Crops/";
                string[] produce = { crops + "PFB_Carrot01", crops + "PFB_Cabbage01", crops + "PFB_Carrot01", crops + "PFB_Beet01", crops + "PFB_Cabbage01" };
                for (int i = 0; i < produce.Length; i++)
                {
                    var item = Prefab(produce[i], market, V(top.min.x + 0.3f + i * (top.size.x - 0.6f) / (produce.Length - 1), top.center.z), R(0f, 360f), 1.6f);
                    item.name += " (on counter)";
                    if (Bounds(item.transform, out Bounds b)) item.transform.position += Vector3.up * (top.max.y - b.min.y);
                }
            }
            Transform camp = world.Find("06_LoggingCamp");
            foreach (Transform child in camp)
            {
                if (!child.name.StartsWith("ChoppingBlock") || !Bounds(child, out Bounds block)) continue;
                var axe = Prefab("Tools/Axe01", camp, V(block.center.x, block.center.z), 20f, 1.4f);
                axe.name += " (on block)";
                axe.transform.rotation = Quaternion.Euler(0f, 20f, 12f);
                if (Bounds(axe.transform, out Bounds a)) axe.transform.position += Vector3.up * (block.max.y - 0.12f - a.min.y);
            }
        }

        private static void ConnectEnvironment()
        {
            var cycle = Object.FindAnyObjectByType<EnvironmentCycle>();
            if (cycle == null) throw new InvalidOperationException("WORLD_Environment with EnvironmentCycle is missing from SC_Farm.");
            var serialized = new SerializedObject(cycle);
            var lights = serialized.FindProperty("lanterns");
            lights.arraySize = Lanterns.Count;
            for (int i = 0; i < Lanterns.Count; i++) lights.GetArrayElementAtIndex(i).objectReferenceValue = Lanterns[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Lantern(Transform parent, Vector2 position, float yaw)
        {
            Transform post = new GameObject("Dusk lantern").transform;
            post.SetParent(parent, false);
            post.position = At(position);
            post.rotation = Quaternion.Euler(0f, yaw, 0f);
            LocalBox("Lantern post", post, new Vector3(0f, 1.4f, 0f), new Vector3(0.15f, 3.2f, 0.15f), Materials["Wood"], true);
            LocalBox("Hanging beam", post, new Vector3(0f, 2.9f, 0.3f), new Vector3(0.13f, 0.13f, 0.85f), Materials["Plank"], false);
            var model = Prefab("Other/Lantern01", post, Vector2.zero, 0f, 1.5f, false);
            model.transform.localPosition = new Vector3(0f, 2.85f, 0.58f);
            model.transform.localRotation = Quaternion.identity;
            var go = new GameObject("Warm lantern light");
            go.transform.SetParent(post, false);
            go.transform.localPosition = new Vector3(0f, 2.4f, 0.58f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.57f, 0.22f);
            light.range = 10f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            Lanterns.Add(light);
        }

        private static void FenceLine(Transform parent, Vector2 from, Vector2 to)
        {
            Vector2 direction = (to - from).normalized;
            float length = Vector2.Distance(from, to);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / 1.92f));
            float stretch = length / (count * 1.92f);
            for (int i = 0; i < count; i++)
            {
                var fence = Prefab("Other/Fence01", parent, Vector2.Lerp(from, to, (i + 0.5f) / count), -Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 1f);
                fence.transform.localScale = new Vector3(stretch, 1f, 1f);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject Prefab(string relative, Transform parent, Vector2 position, float yaw, float scale, bool collision = true)
        {
            // A full "Assets/..." path is one of the game's own prefabs; anything else is relative to the Holotna pack.
            string path = (relative.StartsWith("Assets/") ? relative : Pack + relative) + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing prefab: " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.position = At(position);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i] = ColouredVersion(mats[i]);
                }
                renderer.sharedMaterials = mats;
            }
            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = collision;
                collider.gameObject.layer = GroundLayer;
            }
            foreach (var light in go.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
                light.shadows = LightShadows.None;
                Lanterns.Add(light);
            }
            return go;
        }

        private static void Ribbon(string name, Vector2[] points, float halfWidth, Transform parent, Material material, float height)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 direction = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 side = V(-direction.y, direction.x) * halfWidth;
                Vector2 nextDirection = (points[Mathf.Min(i + 2, points.Length - 1)] - points[i]).normalized;
                Vector2 nextSide = V(-nextDirection.y, nextDirection.x) * halfWidth;
                Vector3 a = Flat(points[i] - side, height), b = Flat(points[i] + side, height);
                Vector3 c = Flat(points[i + 1] - nextSide, height), d = Flat(points[i + 1] + nextSide, height);
                AddTriangle(vertices, triangles, a, b, c);
                AddTriangle(vertices, triangles, b, d, c);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh, name.Replace(" ", ""));
            MeshObject(name, parent, mesh, new[] { material });
        }

        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<Renderer>().sharedMaterials = materials;
            return go;
        }

        private static void SaveMesh(Mesh mesh, string name) { AssetDatabase.CreateAsset(mesh, MeshFolder + "/" + name + ".asset"); }

        private static void DeleteGeneratedMeshes()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { MeshFolder }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        private static GameObject LocalBox(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool collision)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (collision) go.layer = GroundLayer;
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static bool Bounds(Transform root, out Bounds bounds)
        {
            bool found = false;
            bounds = new Bounds();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            return go.transform;
        }

        private static Vector2 V(float x, float z) => new Vector2(x, z);
        private static Vector3 P(float x, float z) => new Vector3(x, Height(x, z), z);
        private static Vector3 P(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);
        private static Vector3 At(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);
        private static Vector3 Flat(Vector2 p, float height) => new Vector3(p.x, height, p.y);
        private static float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
        private static float YawTowards(Vector2 from, Vector2 to) => Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;
        private static bool Inside(Vector2 p, Vector2 centre, Vector2 halfSize) => Mathf.Abs(p.x - centre.x) < halfSize.x && Mathf.Abs(p.y - centre.y) < halfSize.y;

        private static bool Ellipse(Vector2 p, Vector2 centre, float radiusX, float radiusZ, float wobble)
        {
            float dx = (p.x - centre.x) / radiusX, dz = (p.y - centre.y) / radiusZ;
            return dx * dx + dz * dz < 1f + wobble * 0.15f;
        }

        private static Vector2[][] SmoothRoutes(Vector2[][] routes)
        {
            var result = new Vector2[routes.Length][];
            for (int i = 0; i < routes.Length; i++) result[i] = Smooth(routes[i]);
            return result;
        }

        // Catmull-Rom curve through the guide points, sampled every two metres or so.
        private static Vector2[] Smooth(Vector2[] points)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[Mathf.Max(0, i - 1)], b = points[i], c = points[i + 1], d = points[Mathf.Min(points.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(b, c) / 2f));
                for (int n = 0; n < steps; n++)
                    result.Add(CatmullRom(a, b, c, d, n / (float)steps));
            }
            result.Add(points[points.Length - 1]);
            return result.ToArray();
        }

        // The same curve through a closed loop of points; the last point repeats the first.
        private static Vector2[] SmoothLoop(Vector2[] points)
        {
            var result = new List<Vector2>();
            int count = points.Length;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = points[(i + count - 1) % count], b = points[i], c = points[(i + 1) % count], d = points[(i + 2) % count];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(b, c) / 2f));
                for (int n = 0; n < steps; n++)
                    result.Add(CatmullRom(a, b, c, d, n / (float)steps));
            }
            result.Add(result[0]);
            return result.ToArray();
        }

        private static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) =>
            0.5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (-a + 3f * b - 3f * c + d) * t * t * t);

        private static float PathDistance(Vector2 p)
        {
            float nearest = float.MaxValue;
            foreach (var route in Routes) nearest = Mathf.Min(nearest, Distance(p, route));
            return nearest;
        }

        private static float Distance(Vector2 p, Vector2[] points)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 segment = points[i + 1] - points[i];
                float t = Mathf.Clamp01(Vector2.Dot(p - points[i], segment) / segment.sqrMagnitude);
                nearest = Mathf.Min(nearest, Vector2.Distance(p, points[i] + segment * t));
            }
            return nearest;
        }

        private static int NearestIndex(Vector2[] points, Vector2 p)
        {
            int best = 0;
            for (int i = 1; i < points.Length; i++)
                if ((points[i] - p).sqrMagnitude < (points[best] - p).sqrMagnitude) best = i;
            return best;
        }

        // The flat "Colored" look of a pack material, or the material itself if there is none.
        // Crop and tool materials keep theirs under _Game/Materials; everything else stays in the pack.
        public static Material ColouredVersion(Material material)
        {
            foreach (string folder in new[] { "Assets/_Game/Materials/Crops/Colored/", "Assets/_Game/Materials/Tools/Colored/", "Assets/Holotna/Medieval Rural/Materials/Colored/" })
            {
                var coloured = AssetDatabase.LoadAssetAtPath<Material>(folder + material.name + ".mat");
                if (coloured != null) return coloured;
            }
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
