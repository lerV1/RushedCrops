using UnityEngine;

namespace RushedCrops.Player
{
    /// <summary>
    /// First-person look math with no Unity object dependencies, so edit mode tests can cover it.
    /// Angles are in degrees. Pitch follows Unity's convention: negative looks up, positive looks down.
    /// </summary>
    public static class LookMath
    {
        /// <summary>
        /// Turns raw look input into degrees for this frame.
        /// Mouse delta is already "per frame", so only the stick is multiplied by deltaTime.
        /// </summary>
        public static Vector2 ScaleLookInput(Vector2 rawInput, bool fromMouse, float mouseSensitivity,
            float stickDegreesPerSecond, float deltaTime)
        {
            return fromMouse
                ? rawInput * mouseSensitivity
                : rawInput * (stickDegreesPerSecond * deltaTime);
        }

        /// <summary>
        /// Adds this frame's look delta to yaw and pitch. Yaw wraps to [0, 360) and pitch is clamped
        /// so the camera can't flip over the top or bottom.
        /// </summary>
        public static void ApplyLook(ref float yaw, ref float pitch, Vector2 lookDelta,
            float minPitch, float maxPitch, bool invertY)
        {
            yaw = Mathf.Repeat(yaw + lookDelta.x, 360f);

            // Moving the mouse up gives a positive y, which should look up (lower pitch).
            float pitchChange = invertY ? lookDelta.y : -lookDelta.y;
            pitch = Mathf.Clamp(pitch + pitchChange, minPitch, maxPitch);
        }
    }
}
