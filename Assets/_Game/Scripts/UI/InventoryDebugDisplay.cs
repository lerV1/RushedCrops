using System;
using System.Text;
using UnityEngine;

// Draws the inventory counts in the top-left corner during Play mode.
// It uses OnGUI (Unity's immediate-mode debug UI), so it needs no Canvas or prefab setup.
// A proper HUD can replace it later by reading the same Inventory.
public class InventoryDebugDisplay : MonoBehaviour
{
    [Tooltip("The scene's shared Inventory.")]
    [SerializeField] private Inventory inventory;
    [Tooltip("Optional. Shows the debug key list and the result of the last key press.")]
    [SerializeField] private InventoryDebugControls controls;

    [Header("Layout")]
    [SerializeField] private Vector2 position = new Vector2(10f, 10f);
    [SerializeField, Min(100f)] private float width = 340f;
    [SerializeField, Min(8)] private int fontSize = 18;

    private static readonly ResourceType[] AllTypes = (ResourceType[])Enum.GetValues(typeof(ResourceType));

    private readonly StringBuilder text = new StringBuilder();
    private GUIStyle style;

    private void OnGUI()
    {
        if (inventory == null)
        {
            return;
        }

        // GUI styles can only be created inside OnGUI, so make it on first use.
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = fontSize,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8),
            };
        }

        text.Clear();
        text.AppendLine("<b>Inventory</b>");
        foreach (ResourceType type in AllTypes)
        {
            text.AppendLine($"{type}: {inventory.GetCount(type)}");
        }

        if (controls != null)
        {
            text.AppendLine();
            text.AppendLine(controls.HelpText);
            text.AppendLine();
            text.Append(controls.LastResult);
        }

        // Grow the box to fit however many lines there are.
        var content = new GUIContent(text.ToString());
        float height = style.CalcHeight(content, width);
        GUI.Box(new Rect(position.x, position.y, width, height), content, style);
    }
}
