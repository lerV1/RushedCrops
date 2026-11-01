using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Places the cut tree stump by the garden gate where the player finds their tools:
    // the hoe stuck blade-down in the stump and the watering can on the ground beside it.
    // The map builder calls this, and it can be run again from the menu.
    public static class ToolStumpSetup
    {
        public const string RootName = "Tool stump";
        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        private const string FlatStumpPath = "Assets/_Game/Models/Tools/Flat_OakStump01.asset";
        private static readonly Vector3 Spot = new Vector3(-30.2f, 0f, -19.6f); // just west of the garden gate
        private const int GroundLayer = 6;

        [MenuItem("Rushed Crops/Farming/Place tool stump")]
        public static void PlaceInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Place the stump outside Play.");
            var farm = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup);
            if (farm == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");
            Place(farm);
            EditorSceneManager.MarkSceneDirty(farm.gameObject.scene);
        }

        public static GameObject Place(Transform parent)
        {
            Transform old = parent.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Place tool stump");
            root.transform.SetParent(parent, false);
            root.transform.position = Ground(Spot);

            // The pack's oak stump, restyled like the rest of the map: flat shading and a flat dark wood colour.
            var stump = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Vegetation/OakStump01.prefab"), root.transform);
            stump.transform.localPosition = Vector3.zero;
            stump.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            stump.transform.localScale = Vector3.one * 1.4f;
            StyleStump(stump);
            float top = TopOf(stump);

            // The hoe, blade down in the top of the stump with the handle leaning out.
            var hoeSource = FirstLod0(AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Tools/Hoe01.prefab"));
            var hoe = new GameObject("Hoe pickup", typeof(MeshFilter), typeof(MeshRenderer));
            hoe.transform.SetParent(root.transform, false);
            hoe.GetComponent<MeshFilter>().sharedMesh = hoeSource.sharedMesh;
            hoe.GetComponent<MeshRenderer>().sharedMaterial = hoeSource.GetComponent<MeshRenderer>().sharedMaterial;
            Quaternion lean = Quaternion.Euler(0f, 30f, 0f) * Quaternion.AngleAxis(205f, Vector3.forward); // blade (+Y) down, tilted 25 degrees
            hoe.transform.rotation = lean;
            hoe.transform.position = new Vector3(root.transform.position.x, top, root.transform.position.z) - lean * new Vector3(0f, 0.46f, 0f);
            AddPickup(hoe, ToolType.Hoe, "hoe");

            // The watering can on the ground beside the stump, spout pointing away from the gate.
            var can = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Tools/WateringCan.fbx"), root.transform);
            can.name = "Watering can pickup";
            can.transform.position = Ground(root.transform.position + new Vector3(0.6f, 0f, 0.15f)) + Vector3.up * 0.125f;
            can.transform.rotation = Quaternion.Euler(0f, -110f, 0f);
            AddPickup(can, ToolType.WateringCan, "watering can");
            return root;
        }

        private static void AddPickup(GameObject tool, ToolType type, string displayName)
        {
            // A trigger box round the model so the look ray finds it; the player walks through it.
            Bounds local = new Bounds();
            bool first = true;
            foreach (MeshFilter filter in tool.GetComponentsInChildren<MeshFilter>())
            {
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 toTool = tool.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 corner in Corners(b))
                {
                    Vector3 p = toTool.MultiplyPoint3x4(corner);
                    if (first) { local = new Bounds(p, Vector3.zero); first = false; }
                    else local.Encapsulate(p);
                }
            }
            var box = tool.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = local.center;
            box.size = local.size + Vector3.one * 0.08f;

            var pickup = tool.AddComponent<ToolPickup>();
            var settings = new SerializedObject(pickup);
            settings.FindProperty("tool").enumValueIndex = (int)type;
            settings.FindProperty("displayName").stringValue = displayName;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StyleStump(GameObject stump)
        {
            var darkWood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/World/Cartoon/DarkWood.mat");
            foreach (LODGroup lod in stump.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
            foreach (MeshRenderer renderer in stump.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool lod0 = renderer.name.EndsWith("LOD0");
                renderer.enabled = lod0;
                if (!lod0) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                filter.sharedMesh = FlatMesh(filter.sharedMesh);
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = darkWood;
                renderer.sharedMaterials = materials;
            }
        }

        // A copy of the mesh with every triangle given its own corners, so it shades flat like the map.
        private static Mesh FlatMesh(Mesh source)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(FlatStumpPath);
            if (saved != null) return saved;
            var vertices = new List<Vector3>();
            var mesh = new Mesh { name = "Flat_OakStump01" };
            var positions = source.vertices;
            var submeshes = new List<int[]>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] indices = source.GetTriangles(s);
                var flat = new int[indices.Length];
                for (int i = 0; i < indices.Length; i++) { flat[i] = vertices.Count; vertices.Add(positions[indices[i]]); }
                submeshes.Add(flat);
            }
            mesh.SetVertices(vertices);
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++) mesh.SetTriangles(submeshes[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, FlatStumpPath);
            return mesh;
        }

        private static MeshFilter FirstLod0(GameObject prefab)
        {
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name.EndsWith("LOD0")) return filter;
            throw new InvalidOperationException(prefab.name + " has no LOD0 mesh.");
        }

        private static float TopOf(GameObject go)
        {
            float top = float.MinValue;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
                if (renderer.enabled) top = Mathf.Max(top, renderer.bounds.max.y);
            return top;
        }

        private static Vector3 Ground(Vector3 at)
        {
            return Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, 1 << GroundLayer)
                ? hit.point
                : at;
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        }
    }
}
