using System;
using UnityEngine;

// Every sound the game can play. Each has its clips in the SoundBank (one is picked at random each time).
public enum Sfx
{
    Footstep, Jump, Land, Swing,
    Chop, TreeFall, TreeLand, Mine, RockBreak,
    Till, Plant, Water, FillCan, Harvest,
    Pickup, ToolPickup, ToolSwitch, Coins, ShopOpen, ShopClose, Click, Repair, Victory,
    PlayerHurt, MiniBite, MiniHurt, MiniDie,
    BossEmerge, BossRoar, BossSlam, BossSpit, BossCharge, BossHurt, BossDie, ShotLand,
}

// The clips for each sound and how loud and how varied in pitch it plays. Made by Rushed Crops > Audio >
// Set up sounds from the files in Assets/_Game/Audio/Sfx (named <Sound>_01.ogg, <Sound>_02.ogg, ...).
[CreateAssetMenu(menuName = "Rushed Crops/Sound bank", fileName = "DA_Sounds")]
public class SoundBank : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public Sfx sound;
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("Each play picks a pitch between these, so repeats don't sound copy-pasted.")]
        public Vector2 pitch = new Vector2(0.94f, 1.06f);
    }

    [SerializeField] private Entry[] entries = new Entry[0];

    private Entry[] byId;

    public Entry Get(Sfx sound)
    {
        if (byId == null || byId.Length == 0)
        {
            byId = new Entry[Enum.GetValues(typeof(Sfx)).Length];
            foreach (Entry entry in entries)
                if (entry != null) byId[(int)entry.sound] = entry;
        }
        return byId[(int)sound];
    }

    public void SetEntries(Entry[] all)
    {
        entries = all;
        byId = null;
    }
}
