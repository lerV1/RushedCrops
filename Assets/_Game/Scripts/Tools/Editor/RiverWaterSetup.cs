using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Lets the player fill the watering can from the river. The water surface gets a collider the
    // look ray can hit, which touches nothing, so the player still wades through, and a WaterSource.
    // The map builder calls this after making the river, and it can be run again from the menu.
    public static class RiverWaterSetup
    {
        public const string WaterPath = "02_River/River water";
        private const string CollisionMeshPath = "Assets/_Game/Models/Farm/RiverWaterCollision.asset";
        private const int WaterLayer = 4; // Unity's built-in "Water" layer

        [MenuItem("Rushed Crops/Farming/Set up river water")]
        public static void SetUpInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the river outside Play.");
            var water = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(WaterPath);
            if (water == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");
            SetUp(water.gameObject);
            EditorSceneManager.MarkSceneDirty(water.gameObject.scene);
        }

        public static void SetUp(GameObject water)
        {
            Undo.RecordObject(water, "Set up river water");
            water.layer = WaterLayer;

            var collider = water.GetComponent<MeshCollider>();
            if (collider == null) collider = Undo.AddComponent<MeshCollider>(water);
            collider.sharedMesh = DoubleSided(water.GetComponent<MeshFilter>().sharedMesh);
            collider.excludeLayers = ~0; // no contacts with anything; raycasts still hit it

            if (water.GetComponent<WaterSource>() == null) Undo.AddComponent<WaterSource>(water);
        }

        // Raycasts only hit the front of a mesh collider's triangles, and the river ribbon faces down
        // (its shader draws both sides). A copy with every triangle in both directions is hit from above.
        private static Mesh DoubleSided(Mesh source)
        {
            int[] triangles = source.triangles;
            var both = new int[triangles.Length * 2];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                both[i] = triangles[i];
                both[i + 1] = triangles[i + 1];
                both[i + 2] = triangles[i + 2];
                both[triangles.Length + i] = triangles[i];
                both[triangles.Length + i + 1] = triangles[i + 2];
                both[triangles.Length + i + 2] = triangles[i + 1];
            }
            var mesh = new Mesh { name = "RiverWaterCollision", indexFormat = source.indexFormat };
            mesh.vertices = source.vertices;
            mesh.triangles = both;
            mesh.RecalculateBounds();

            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(CollisionMeshPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, CollisionMeshPath);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, saved);
            UnityEngine.Object.DestroyImmediate(mesh);
            return saved;
        }
    }
}
