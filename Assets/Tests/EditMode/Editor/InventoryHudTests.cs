using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Edit Mode tests for the hotbar pictures: a slot shows its item's picture and count,
// an item without a picture shows its name, and an empty slot shows nothing.
public class InventoryHudTests
{
    private GameObject inventoryObject;
    private GameObject hudObject;
    private Inventory inventory;
    private InventoryHud hud;
    private ItemIcons icons;
    private Sprite carrotPicture;
    private Image icon;
    private Text nameText;
    private Text countText;

    [SetUp]
    public void SetUp()
    {
        inventoryObject = new GameObject("Inventory");
        inventory = inventoryObject.AddComponent<Inventory>();
        TestMessages.Send(inventory, "Awake");

        // Only carrots have a picture, so stone shows what happens to an item without one.
        carrotPicture = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.zero);
        icons = ScriptableObject.CreateInstance<ItemIcons>();
        var iconSettings = new SerializedObject(icons);
        SerializedProperty list = iconSettings.FindProperty("icons");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).FindPropertyRelative("item").enumValueIndex = (int)ResourceType.Carrots;
        list.GetArrayElementAtIndex(0).FindPropertyRelative("icon").objectReferenceValue = carrotPicture;
        iconSettings.ApplyModifiedPropertiesWithoutUndo();

        hudObject = new GameObject("HUD", typeof(RectTransform));
        icon = Make<Image>("Icon");
        nameText = Make<Text>("Name");
        countText = Make<Text>("Count");
        hud = hudObject.AddComponent<InventoryHud>();
        var hudSettings = new SerializedObject(hud);
        hudSettings.FindProperty("inventory").objectReferenceValue = inventory;
        hudSettings.FindProperty("icons").objectReferenceValue = icons;
        SerializedProperty slots = hudSettings.FindProperty("slots");
        slots.arraySize = 1; // the first hotbar slot is enough to test
        SerializedProperty slot = slots.GetArrayElementAtIndex(0);
        slot.FindPropertyRelative("icon").objectReferenceValue = icon;
        slot.FindPropertyRelative("nameText").objectReferenceValue = nameText;
        slot.FindPropertyRelative("countText").objectReferenceValue = countText;
        hudSettings.ApplyModifiedPropertiesWithoutUndo();

        // Unity skips OnEnable in Edit Mode, so call it the way Unity would.
        TestMessages.Send(hud, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        TestMessages.Send(hud, "OnDisable");
        Object.DestroyImmediate(hudObject);
        Object.DestroyImmediate(inventoryObject);
        Object.DestroyImmediate(icons);
        Object.DestroyImmediate(carrotPicture.texture);
        Object.DestroyImmediate(carrotPicture);
    }

    private T Make<T>(string name) where T : Component
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(hudObject.transform, false);
        return child.AddComponent<T>();
    }

    [Test]
    public void EmptySlot_ShowsNothing()
    {
        Assert.IsFalse(icon.enabled);
        Assert.AreEqual("", nameText.text);
        Assert.AreEqual("", countText.text);
    }

    [Test]
    public void ItemWithAPicture_ShowsThePictureAndCountButNoName()
    {
        inventory.Add(ResourceType.Carrots, 4);

        Assert.IsTrue(icon.enabled);
        Assert.AreSame(carrotPicture, icon.sprite);
        Assert.AreEqual("", nameText.text);
        Assert.AreEqual("4", countText.text);
    }

    [Test]
    public void ItemWithoutAPicture_ShowsItsName()
    {
        inventory.Add(ResourceType.Stone, 2);

        Assert.IsFalse(icon.enabled);
        Assert.AreEqual(ResourceTypes.DisplayName(ResourceType.Stone), nameText.text);
        Assert.AreEqual("2", countText.text);
    }

    [Test]
    public void SpendingEverything_RemovesThePicture()
    {
        inventory.Add(ResourceType.Carrots, 3);
        inventory.TrySpend(ResourceType.Carrots, 3);

        Assert.IsFalse(icon.enabled);
        Assert.IsNull(icon.sprite);
        Assert.AreEqual("", countText.text);
    }
}
