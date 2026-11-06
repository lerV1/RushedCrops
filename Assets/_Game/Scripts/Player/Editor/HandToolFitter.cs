using System;
using UnityEditor;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Puts each tool model in the right hand with its handle through the closed fist (found from the finger
    // curl in the GripIdle pose). Run it again after changing the grip pose or a tool model.
    public static class HandToolFitter
    {
        public const string PlayerPrefab = "Assets/_Game/Prefabs/Player/PFB_Player.prefab";
        private const string HandModel = "Assets/_Game/Models/Hands/HandRight.fbx";
        private const string CameraPath = "CameraRoot/Main Camera";
        private const string ArmPath = "CameraRoot/Main Camera/HandsRoot/HandRight";

        // Long-handled tools and where up the handle the hand holds them (0 bottom, 1 top).
        private static readonly (string name, float gripFraction)[] LongTools = { ("Hoe", 0.68f), ("Hatchet", 0.55f) };
        // Degrees a handle leans across the palm, away from the knuckle line.
        private const float HandleLean = 30f;

        [MenuItem("Rushed Crops/Player/Fit tools to the hand")]
        public static void FitPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                Fit(root);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Fits the tools inside an open copy of the player prefab (used by MeleeSetup too).
        public static void Fit(GameObject player)
        {
            Transform view = player.transform.Find(CameraPath);
            Transform arm = player.transform.Find(ArmPath);
            if (view == null || arm == null) throw new InvalidOperationException("The player is missing its camera or right arm.");

            GripClip().SampleAnimation(arm.gameObject, 0f);
            Transform hand = Bone(arm, "Hand");
            Vector3 fist = (FingerCurlCentre(arm, "Index") + FingerCurlCentre(arm, "Middle") + FingerCurlCentre(arm, "Ring")) / 3f;
            // Along the knuckles, from the little finger towards the thumb: the way a gripped handle runs.
            Vector3 across = (Bone(arm, "Index1").position - Bone(arm, "Pinky1").position).normalized;
            // The blade (each tool's +Z) faces away from the player.
            Vector3 away = Vector3.ProjectOnPlane(view.forward, across).normalized;

            foreach (var (name, gripFraction) in LongTools)
            {
                Transform tool = hand.Find(name);
                if (tool == null) continue;
                Bounds mesh = tool.GetComponent<MeshFilter>().sharedMesh.bounds;
                float gripHeight = mesh.min.y + mesh.size.y * gripFraction;
                // A handle crosses the palm on a diagonal: lean it towards the middle of the screen
                // and a little forward, so the head comes into view instead of sticking straight up.
                Vector3 towardsMiddle = (-view.right * 0.9f + away * 0.4f).normalized;
                Vector3 handle = Lean(across, towardsMiddle, HandleLean);
                tool.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(away, handle).normalized, handle);
                tool.position = fist - handle * gripHeight;
                tool.localScale = Vector3.one;
            }

            Transform can = hand.Find("WateringCan");
            if (can != null)
            {
                Vector3 spout = Quaternion.AngleAxis(-20f, across) * away;
                can.rotation = Quaternion.LookRotation(spout, across);
                can.position = fist;
                can.localScale = Vector3.one * 0.85f;
            }
        }

        private static Vector3 Lean(Vector3 from, Vector3 towards, float degrees)
        {
            Vector3 axis = Vector3.Cross(from, towards).normalized;
            Vector3 a = Quaternion.AngleAxis(degrees, axis) * from;
            Vector3 b = Quaternion.AngleAxis(-degrees, axis) * from;
            return Vector3.Dot(a, towards) > Vector3.Dot(b, towards) ? a : b;
        }

        // The centre of the circle the finger curls round: the middle of the fist for that finger.
        private static Vector3 FingerCurlCentre(Transform arm, string finger)
        {
            Vector3 a = Bone(arm, finger + "1").position;
            Vector3 b = Bone(arm, finger + "2").position;
            Vector3 c = Bone(arm, finger + "3").position;
            Vector3 ab = b - a, ac = c - a, normal = Vector3.Cross(ab, ac);
            if (normal.sqrMagnitude < 1e-10f) return (a + b + c) / 3f; // a straight finger: no curl to read
            return a + (Vector3.Cross(normal, ab) * ac.sqrMagnitude + Vector3.Cross(ac, normal) * ab.sqrMagnitude) / (2f * normal.sqrMagnitude);
        }

        private static AnimationClip GripClip()
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(HandModel))
                if (asset is AnimationClip clip && clip.name == "GripIdle") return clip;
            throw new InvalidOperationException("HandRight.fbx has no GripIdle clip.");
        }

        private static Transform Bone(Transform arm, string name)
        {
            foreach (Transform t in arm.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            throw new InvalidOperationException("The right arm has no bone called " + name + ".");
        }
    }
}
