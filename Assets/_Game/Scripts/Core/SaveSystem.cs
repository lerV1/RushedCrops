using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class SaveData
{
    public int version = 1;
    public int coins;
    public int selectedSlot;
    public int[] slotTypes;
    public int[] slotCounts;
    public int barnRepairStage;
    public string[] bossesBeaten;
    public bool bossDefeated;
    public bool hasWon;
    public int unlockedTubeCount;
    public TubeData[] tubes;
    public CropTileData[] cropTiles;
    public int waterLeft;
    public int playerHealth;
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public bool hasCheckpoint;
    public Vector3 checkpointPosition;
    public Quaternion checkpointRotation;

    [Serializable]
    public class TubeData
    {
        public bool placed;
        public int storedSeeds;
        public float seedTimer;
    }

    [Serializable]
    public class CropTileData
    {
        public int x;
        public int y;
        public int state;
        public int seedResource;
        public float progress;
        public int waterings;
        public bool isWet;
    }
}

[DefaultExecutionOrder(1000)]
public class SaveSystem : MonoBehaviour
{
    private const string SaveKey = "RushedCrops.SaveData";

    [Header("Scene references")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private BarnRepair barnRepair;
    [SerializeField] private BarnTubes barnTubes;
    [SerializeField] private FarmField farmField;
    [SerializeField] private PlayerTools playerTools;
    [SerializeField] private Health playerHealth;
    [SerializeField] private RespawnController respawnController;
    [SerializeField] private Transform player;

    [Header("Save controls")]
    [SerializeField, Min(1f)] private float autosaveSeconds = 30f;
    [SerializeField] private Key saveKey = Key.F5;
    [SerializeField] private Key deleteSaveKey = Key.Delete;

    private float autosaveTimer;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("SaveSystem needs all scene references assigned.", this);
            enabled = false;
            return;
        }

        LoadNow();
        autosaveTimer = autosaveSeconds;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[saveKey].wasPressedThisFrame)
        {
            SaveNow();
        }
        if (keyboard != null && keyboard[deleteSaveKey].wasPressedThisFrame)
        {
            DeleteSave();
        }

        autosaveTimer -= Time.unscaledDeltaTime;
        if (autosaveTimer <= 0f)
        {
            SaveNow();
            autosaveTimer = autosaveSeconds;
        }
    }

    private void OnApplicationQuit()
    {
        if (HasRequiredReferences())
        {
            SaveNow();
        }
    }

    public void SaveNow()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        bool savingAfterDeath = playerHealth.IsDead || respawnController.IsAwaitingRespawn;
        PlayerLook look = player.GetComponent<PlayerLook>();
        SaveData data = new SaveData
        {
            coins = inventory.GetCount(ResourceType.Coins),
            selectedSlot = inventory.SelectedSlot,
            slotTypes = inventory.GetSavedSlotTypes(),
            slotCounts = inventory.GetSavedSlotCounts(),
            barnRepairStage = gameManager.BarnRepairs,
            bossesBeaten = gameManager.GetSavedBosses(),
            bossDefeated = gameManager.IsBossDefeated,
            hasWon = gameManager.HasWon,
            unlockedTubeCount = barnTubes.unlockedTubeCount,
            tubes = CaptureTubes(),
            cropTiles = farmField.GetSaveData(),
            waterLeft = playerTools.WaterLeft,
            // A closed death screen resumes at the checkpoint, never at the fatal position.
            playerHealth = savingAfterDeath ? playerHealth.MaxHealth : playerHealth.CurrentHealth,
            playerPosition = savingAfterDeath ? respawnController.CheckpointPosition : player.position,
            playerRotation = savingAfterDeath ? respawnController.CheckpointRotation :
                look != null ? Quaternion.Euler(0f, look.Yaw, 0f) : player.rotation,
            hasCheckpoint = respawnController.HasCheckpoint,
            checkpointPosition = respawnController.CheckpointPosition,
            checkpointRotation = respawnController.CheckpointRotation
        };

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public bool LoadNow()
    {
        if (!PlayerPrefs.HasKey(SaveKey) || !HasRequiredReferences())
        {
            return false;
        }

        SaveData data;
        try
        {
            data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
        }
        catch (ArgumentException)
        {
            return false;
        }
        if (data == null || data.version != 1 || data.slotTypes == null || data.slotCounts == null ||
            data.slotTypes.Length != Inventory.SlotCount || data.slotCounts.Length != Inventory.SlotCount)
        {
            return false;
        }

        inventory.RestoreState(data.coins, data.slotTypes, data.slotCounts, data.selectedSlot);
        gameManager.RestoreProgress(data.barnRepairStage, data.bossesBeaten, data.bossDefeated, data.hasWon);
        barnRepair.ShowRepairs(gameManager.BarnRepairs);
        barnTubes.RestoreUnlockedTubeCount(data.unlockedTubeCount, gameManager.BarnRepairs);
        RestoreTubes(data.tubes);
        farmField.RestoreSaveData(data.cropTiles);
        playerTools.RestoreWater(data.waterLeft);
        playerHealth.RestoreHealth(data.playerHealth);
        respawnController.RestoreSaveState(data.playerPosition, data.playerRotation, data.checkpointPosition, data.checkpointRotation, data.hasCheckpoint);
        return true;
    }

    public void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    private SaveData.TubeData[] CaptureTubes()
    {
        BossTube[] sceneTubes = barnTubes.Tubes;
        var savedTubes = new SaveData.TubeData[sceneTubes.Length];
        for (int i = 0; i < sceneTubes.Length; i++)
        {
            BossTube tube = sceneTubes[i];
            savedTubes[i] = tube == null ? new SaveData.TubeData() : new SaveData.TubeData
            {
                placed = tube.isPlaced,
                storedSeeds = tube.StoredSeeds,
                seedTimer = tube.SeedTimer
            };
        }
        return savedTubes;
    }

    private void RestoreTubes(SaveData.TubeData[] savedTubes)
    {
        BossTube[] sceneTubes = barnTubes.Tubes;
        for (int i = 0; i < sceneTubes.Length; i++)
        {
            if (sceneTubes[i] == null)
            {
                continue;
            }

            SaveData.TubeData saved = savedTubes != null && i < savedTubes.Length
                ? savedTubes[i]
                : null;
            sceneTubes[i].RestoreState(saved != null && saved.placed,
                saved != null ? saved.storedSeeds : 0,
                saved != null ? saved.seedTimer : 0f);
        }
    }

    private bool HasRequiredReferences()
    {
        return inventory != null && gameManager != null && barnRepair != null && barnTubes != null &&
            farmField != null && playerTools != null && playerHealth != null &&
            respawnController != null && player != null;
    }
}
