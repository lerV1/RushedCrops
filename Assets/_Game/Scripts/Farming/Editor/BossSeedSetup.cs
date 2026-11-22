using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Sets up the rare boss seeds. Every evil mini crop has a small chance to drop its crop's boss seed; planted in
    // the crop field and watered, it grows like its crop and then the matching boss climbs out:
    //   Carrot King, Tomato Brute, Onion Overlord (also on the map), and Beet Baron, Cabbage Colossus,
    //   Wheat Warlord (only from seeds).
    public static class BossSeedSetup
    {
        private const float GrowSeconds = 120f;
        private const int WaterNeeded = 2;

        [MenuItem("Rushed Crops/Farming/Set up boss seeds")]
        public static void SetUpAll()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the boss seeds outside Play.");

            // Pictures first: the icon renderer switches scenes, so it must run before anything below adds
            // unsaved changes to this one. It only runs when a boss seed has no picture yet.
            // The boss models get their cartoon colours first, so their pictures look right.
            foreach (var (_, bossModel) in ItemIconRenderer.BossSeeds) CropBossSetup.SetUpModelMaterials(bossModel);
            if (BossSeedIconsMissing()) ItemIconRenderer.RenderAll();

            CropBoss[] bosses = CropBossSetup.MakeGrownBossPrefabs(); // carrot, tomato, onion, beet, cabbage, wheat
            MakeSeed(FarmPlotSetup.BossSeedAssets[0], "Assets/_Game/Data/DA_Carrot.asset", "Carrot King", ResourceType.CarrotKingSeed, bosses[0]);
            MakeSeed(FarmPlotSetup.BossSeedAssets[1], "Assets/_Game/Data/DA_Tomato.asset", "Tomato Brute", ResourceType.TomatoBruteSeed, bosses[1]);
            MakeSeed(FarmPlotSetup.BossSeedAssets[2], "Assets/_Game/Data/DA_Onion.asset", "Onion Overlord", ResourceType.OnionOverlordSeed, bosses[2]);
            MakeSeed(FarmPlotSetup.BossSeedAssets[3], "Assets/_Game/Data/DA_Beet.asset", "Beet Baron", ResourceType.BeetBaronSeed, bosses[3]);
            MakeSeed(FarmPlotSetup.BossSeedAssets[4], "Assets/_Game/Data/DA_Cabbage.asset", "Cabbage Colossus", ResourceType.CabbageColossusSeed, bosses[4]);
            MakeSeed(FarmPlotSetup.BossSeedAssets[5], "Assets/_Game/Data/DA_Wheat.asset", "Wheat Warlord", ResourceType.WheatWarlordSeed, bosses[5]);
            AssetDatabase.SaveAssets();

            MiniCropSetup.AssignBossSeeds();
            ItemDropSetup.Models(); // boss seeds lie on the ground as a seed sack

            // Let the crop field grow them.
            var garden = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup);
            if (garden != null)
            {
                foreach (FarmPlot plot in garden.GetComponentsInChildren<FarmPlot>(true)) FarmPlotSetup.AssignCrops(plot);
                EditorSceneManager.MarkSceneDirty(garden.gameObject.scene);
            }
            else
            {
                Debug.LogWarning("Open SC_Farm and run Rushed Crops > Farming > Update crops in crop field to let the field grow boss seeds.");
            }
            Debug.Log("Boss seeds set up: every mini crop can drop its boss seed, and the crop field grows them into bosses.");
        }

        private static bool BossSeedIconsMissing()
        {
            var icons = AssetDatabase.LoadAssetAtPath<ItemIcons>("Assets/_Game/Data/DA_ItemIcons.asset");
            if (icons == null) return true;
            foreach (var (seed, _) in ItemIconRenderer.BossSeeds)
            {
                if (icons.Get(seed) == null) return true;
            }
            return false;
        }

        // A boss seed grows like its crop (same growth models), only slower and thirstier, then gives its boss.
        private static void MakeSeed(string path, string basePath, string displayName, ResourceType seed, CropBoss boss)
        {
            var baseCrop = AssetDatabase.LoadAssetAtPath<CropData>(basePath);
            if (baseCrop == null) throw new InvalidOperationException("Missing crop asset " + basePath);

            var data = AssetDatabase.LoadAssetAtPath<CropData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CropData>();
                AssetDatabase.CreateAsset(data, path);
            }

            var settings = new SerializedObject(data);
            var from = new SerializedObject(baseCrop);
            settings.FindProperty("cropType").enumValueIndex = from.FindProperty("cropType").enumValueIndex;
            settings.FindProperty("displayName").stringValue = displayName;
            settings.FindProperty("seedResource").enumValueIndex = (int)seed;
            // Never harvested (the boss comes out instead), but keep a sensible value.
            settings.FindProperty("harvestResource").enumValueIndex = from.FindProperty("harvestResource").enumValueIndex;
            settings.FindProperty("harvestAmount").intValue = 1;
            settings.FindProperty("growSeconds").floatValue = GrowSeconds;
            settings.FindProperty("waterNeeded").intValue = WaterNeeded;
            settings.FindProperty("grownBoss").objectReferenceValue = boss;

            SerializedProperty stages = settings.FindProperty("stageModels");
            SerializedProperty baseStages = from.FindProperty("stageModels");
            stages.arraySize = baseStages.arraySize;
            for (int i = 0; i < baseStages.arraySize; i++)
            {
                stages.GetArrayElementAtIndex(i).objectReferenceValue = baseStages.GetArrayElementAtIndex(i).objectReferenceValue;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }
    }
}
