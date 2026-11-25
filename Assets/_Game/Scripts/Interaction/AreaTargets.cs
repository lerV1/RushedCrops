using System.Collections.Generic;
using UnityEngine;

// Every area in the scene. Areas add themselves in OnEnable and remove themselves in OnDisable.
public static class AreaTargets
{
    public static readonly List<FarmField> All = new List<FarmField>();
}
