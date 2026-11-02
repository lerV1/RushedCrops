using System.Collections.Generic;
using UnityEngine;

// Part of the ground the player can use by aiming at it, like the crop field.
// An area has no collider of its own, so the player can stand inside it and still aim at it:
// when the look ray lands on the ground, PlayerInteractor asks every registered area whether
// that point lies inside it. The area then works like any other target (IInteractable,
// IToolTarget, IHighlightable) for the point it was given.
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
