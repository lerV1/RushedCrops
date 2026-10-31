using NUnit.Framework;
using RushedCrops.Player;
using UnityEngine;

namespace RushedCrops.Tests
{
    public class MoveMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Forward_FacingNorth_MovesAlongWorldForward()
        {
            Vector3 direction = MoveMath.GetMoveDirection(Vector2.up, 0f);

            AssertVector(direction, Vector3.forward);
        }

        [Test]
        public void Forward_FacingEast_MovesAlongWorldRight()
        {
            Vector3 direction = MoveMath.GetMoveDirection(Vector2.up, 90f);

            AssertVector(direction, Vector3.right);
        }

        [Test]
        public void StrafeRight_FacingEast_MovesAlongWorldBack()
        {
            Vector3 direction = MoveMath.GetMoveDirection(Vector2.right, 90f);

            AssertVector(direction, Vector3.back);
        }

        [Test]
        public void Diagonal_IsNotFasterThanStraight()
        {
            Vector3 direction = MoveMath.GetMoveDirection(new Vector2(1f, 1f), 30f);

            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Direction_StaysFlat()
        {
            Vector3 direction = MoveMath.GetMoveDirection(new Vector2(0.3f, -0.8f), 217f);

            Assert.That(direction.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void NoInput_NoMovement()
        {
            Vector3 direction = MoveMath.GetMoveDirection(Vector2.zero, 123f);

            AssertVector(direction, Vector3.zero);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance),
                $"Expected {expected} but got {actual}");
        }
    }
}
