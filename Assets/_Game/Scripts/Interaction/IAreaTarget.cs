using System.Collections.Generic;
using UnityEngine;

// Ground the player can use by aiming at it, like the crop field. It has no collider, so PlayerInteractor
// asks each area whether the ground point it hit lies inside.
public interface IAreaTarget
{
    // True if the point is inside this area. The area remembers it as the aimed point.
    bool TryAim(Vector3 point);
}

// Every area in the scene. Areas add themselves in OnEnable and remove themselves in OnDisable.
public static class AreaTargets
{
    public static readonly List<IAreaTarget> All = new List<IAreaTarget>();
}
