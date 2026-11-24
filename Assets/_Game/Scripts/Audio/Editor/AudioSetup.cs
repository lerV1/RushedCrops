using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Sets up the game's sound: import settings for everything in Assets/_Game/Audio, the sound bank (each
    // Sfx gets the files named <Sfx>_01.ogg, <Sfx>_02.ogg, ... in Audio/Sfx) and the GameAudio player in
    // SC_Farm, wired to the player. Safe to run again. See Assets/_Game/Audio/CREDITS.md for the sources.
    public static class AudioSetup
    {
        public const string RootName = "GAMEPLAY_Audio";
        public const string BankPath = "Assets/_Game/Data/DA_Sounds.asset";
        private const string SfxFolder = "Assets/_Game/Audio/Sfx";
        private const string AmbienceFolder = "Assets/_Game/Audio/Ambience";

        // How loud each sound is and how much its pitch varies; anything not listed plays at 0.8, +-6%.
        private static readonly Dictionary<Sfx, (float volume, float low, float high)> Mix = new Dictionary<Sfx, (float, float, float)>
        {
            { Sfx.Footstep, (0.28f, 0.9f, 1.1f) }, { Sfx.Land, (0.5f, 0.75f, 0.85f) }, { Sfx.Jump, (0.35f, 0.95f, 1.1f) },
            { Sfx.Swing, (0.45f, 0.9f, 1.15f) }, { Sfx.Chop, (0.85f, 0.9f, 1.05f) }, { Sfx.Mine, (0.8f, 0.9f, 1.1f) },
            { Sfx.TreeFall, (0.9f, 0.7f, 0.85f) }, { Sfx.TreeLand, (1f, 0.6f, 0.7f) }, { Sfx.RockBreak, (0.9f, 0.8f, 0.95f) },
            { Sfx.Till, (0.7f, 0.9f, 1.1f) }, { Sfx.Plant, (0.5f, 0.95f, 1.15f) }, { Sfx.Water, (0.45f, 1f, 1.2f) },
            { Sfx.Harvest, (0.6f, 1f, 1.2f) }, { Sfx.Pickup, (0.4f, 0.95f, 1.2f) }, { Sfx.Click, (0.5f, 1f, 1f) },
            { Sfx.Coins, (0.6f, 0.95f, 1.05f) }, { Sfx.PlayerHurt, (0.6f, 0.75f, 0.85f) }, { Sfx.MiniBite, (0.7f, 1.1f, 1.3f) },
            { Sfx.MiniHurt, (0.7f, 1.1f, 1.35f) }, { Sfx.MiniDie, (0.8f, 1f, 1.2f) }, { Sfx.BossRoar, (1f, 0.75f, 0.9f) },
            { Sfx.BossSlam, (1f, 0.55f, 0.65f) }, { Sfx.BossCharge, (1f, 0.8f, 0.9f) }, { Sfx.BossHurt, (0.85f, 0.85f, 1f) },
            { Sfx.BossDie, (1f, 0.75f, 0.8f) }, { Sfx.BossEmerge, (1f, 0.7f, 0.8f) }, { Sfx.Victory, (0.9f, 1f, 1f) },
        };

        [MenuItem("Rushed Crops/Audio/Set up sounds")]
        public static void SetUp()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the sounds outside Play.");
            if (EditorSceneManager.GetActiveScene().path != FarmWorldBuilder.ScenePath)
                throw new InvalidOperationException("Open SC_Farm first.");

            ImportSettings();
            SoundBank bank = MakeBank();

            var player = GameObject.Find("Player");
            if (player == null) throw new InvalidOperationException("SC_Farm has no Player.");
            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);
            var audio = root.GetComponent<GameAudio>();
            if (audio == null) audio = root.AddComponent<GameAudio>();
            audio.Configure(bank, Clip(AmbienceFolder + "/Birds.ogg"), Clip(AmbienceFolder + "/Crickets.ogg"), Clip(AmbienceFolder + "/Wind.ogg"),
                player.GetComponent<PlayerTools>(), player.GetComponent<Health>(), player.GetComponentInChildren<CameraFeel>(true));
            EditorUtility.SetDirty(audio);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Sounds set up: " + Enum.GetValues(typeof(Sfx)).Length + " sound effects, birds, crickets, wind and the river.");
        }

        // Effects: mono (they are placed in the world), unpacked on load so they start instantly.
        // Ambience: stereo loops, kept compressed in memory.
        private static void ImportSettings()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Game/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool effect = path.StartsWith(SfxFolder);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = effect ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = effect ? 0.5f : 0.4f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = effect;
                importer.loadInBackground = !effect;
                importer.SaveAndReimport();
            }
        }

        private static SoundBank MakeBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            var entries = new List<SoundBank.Entry>();
            foreach (Sfx sound in Enum.GetValues(typeof(Sfx)))
            {
                var clips = new List<AudioClip>();
                foreach (string guid in AssetDatabase.FindAssets(sound + "_ t:AudioClip", new[] { SfxFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (name.Substring(0, name.LastIndexOf('_')) == sound.ToString()) clips.Add(Clip(path));
                }
                clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                var mix = Mix.TryGetValue(sound, out var m) ? m : (0.8f, 0.94f, 1.06f);
                entries.Add(new SoundBank.Entry { sound = sound, clips = clips.ToArray(), volume = mix.Item1, pitch = new Vector2(mix.Item2, mix.Item3) });
            }
            bank.SetEntries(entries.ToArray());
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            return bank;
        }

        private static AudioClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing sound " + path + ".");
            return clip;
        }
    }
}
