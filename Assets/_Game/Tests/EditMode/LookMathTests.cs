using NUnit.Framework;
using RushedCrops.Player;
using UnityEngine;

namespace RushedCrops.Tests
{
    public class LookMathTests
    {
        private const float MinPitch = -85f;
        private const float MaxPitch = 85f;

        [Test]
        public void MouseUp_LooksUp()
        {
            float yaw = 0f, pitch = 0f;

            LookMath.ApplyLook(ref yaw, ref pitch, new Vector2(0f, 10f), MinPitch, MaxPitch, invertY: false);

            Assert.That(pitch, Is.EqualTo(-10f));
        }

        [Test]
        public void InvertY_MouseUpLooksDown()
        {
            float yaw = 0f, pitch = 0f;

            LookMath.ApplyLook(ref yaw, ref pitch, new Vector2(0f, 10f), MinPitch, MaxPitch, invertY: true);

            Assert.That(pitch, Is.EqualTo(10f));
        }

        [Test]
        public void Pitch_StopsAtLimits()
        {
            float yaw = 0f, pitch = 0f;

            LookMath.ApplyLook(ref yaw, ref pitch, new Vector2(0f, 500f), MinPitch, MaxPitch, invertY: false);
            Assert.That(pitch, Is.EqualTo(MinPitch));

            LookMath.ApplyLook(ref yaw, ref pitch, new Vector2(0f, -500f), MinPitch, MaxPitch, invertY: false);
            Assert.That(pitch, Is.EqualTo(MaxPitch));
        }

        [Test]
        public void Yaw_WrapsAround()
        {
            float yaw = 350f, pitch = 0f;

            LookMath.ApplyLook(ref yaw, ref pitch, new Vector2(20f, 0f), MinPitch, MaxPitch, invertY: false);
            Assert.That(yaw, Is.EqualTo(10f).Within(0.001f));

            LookMath.ApplyLook(ref yaw, ref pitch, new Vector2(-30f, 0f), MinPitch, MaxPitch, invertY: false);
            Assert.That(yaw, Is.EqualTo(340f).Within(0.001f));
        }

        [Test]
        public void MouseInput_IgnoresFrameTime()
        {
            Vector2 scaled = LookMath.ScaleLookInput(new Vector2(10f, 5f), fromMouse: true,
                mouseSensitivity: 0.1f, stickDegreesPerSecond: 180f, deltaTime: 0.5f);

            Assert.That(scaled.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(scaled.y, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void StickInput_ScalesWithFrameTime()
        {
            Vector2 scaled = LookMath.ScaleLookInput(new Vector2(1f, 0f), fromMouse: false,
                mouseSensitivity: 0.1f, stickDegreesPerSecond: 180f, deltaTime: 0.5f);

            Assert.That(scaled.x, Is.EqualTo(90f).Within(0.0001f));
        }
    }
}
