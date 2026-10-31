using UnityEngine;

namespace RushedCrops.Player
{
    /// <summary>Movement math with no Unity object dependencies, so edit mode tests can cover it.</summary>
    public static class MoveMath
    {
        /// <summary>
        /// Turns move input (WASD or left stick) into a flat world-space direction relative to
        /// <paramref name="yawDegrees"/>. The result is at most length 1, so diagonals aren't faster.
        /// </summary>
        public static Vector3 GetMoveDirection(Vector2 input, float yawDegrees)
        {
            Vector3 local = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
            return Quaternion.Euler(0f, yawDegrees, 0f) * local;
        }
    }
}
