using System.Collections;
using NUnit.Framework;
using RushedCrops.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RushedCrops.Tests
{
    /// <summary>
    /// Loads the sample scene and drives the player with fake keyboard and mouse input.
    /// </summary>
    public class FirstPersonPlayerTests
    {
        private const string SceneName = "SampleScene";

        private InputSettings.BackgroundBehavior originalBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode originalEditorBehavior;
        private Keyboard keyboard;
        private Mouse mouse;
        private PlayerLook look;
        private Rigidbody body;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // By default keyboard and mouse input only reach a focused Game view, and batch mode
            // (CI) has no Game view at all. Let the fake devices through for the length of the test.
            InputSettings settings = InputSystem.settings;
            originalBackgroundBehavior = settings.backgroundBehavior;
            originalEditorBehavior = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            yield return SceneManager.LoadSceneAsync(SceneName);

            look = Object.FindAnyObjectByType<PlayerLook>();
            Assert.That(look, Is.Not.Null, $"{SceneName} has no PlayerLook");
            body = look.GetComponent<Rigidbody>();

            // Let the player drop onto the ground before measuring anything.
            yield return new WaitForSeconds(0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);

            InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorBehavior;
        }

        [Test]
        public void Player_HasCameraAtEyeHeight()
        {
            Camera camera = look.GetComponentInChildren<Camera>();

            Assert.That(camera, Is.Not.Null, "The player prefab should carry its own camera");
            Assert.That(camera.transform.position.y, Is.GreaterThan(body.position.y),
                "Camera should sit above the body's center");
        }

        [UnityTest]
        public IEnumerator MouseMovement_TurnsCamera()
        {
            float yawBefore = look.Yaw;
            float pitchBefore = look.Pitch;

            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(300f, 200f) });
            yield return null;
            yield return null;

            Assert.That(Mathf.DeltaAngle(yawBefore, look.Yaw), Is.GreaterThan(1f), "Moving the mouse right should turn right");
            Assert.That(look.Pitch, Is.LessThan(pitchBefore), "Moving the mouse up should look up");
        }

        [UnityTest]
        public IEnumerator PressingW_WalksWhereCameraFaces()
        {
            // Turn about 90 degrees to the right so "forward" is world +X.
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(900f, 0f) });
            yield return null;
            yield return null;

            Vector3 facing = Quaternion.Euler(0f, look.Yaw, 0f) * Vector3.forward;
            Vector3 start = body.position;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            Vector3 moved = Vector3.ProjectOnPlane(body.position - start, Vector3.up);
            Assert.That(moved.magnitude, Is.GreaterThan(1f), "Player should have walked forward");
            Assert.That(Vector3.Angle(moved, facing), Is.LessThan(10f), "Player should walk the way the camera faces");
        }

        [UnityTest]
        public IEnumerator Player_StaysOnTheGround()
        {
            float startY = body.position.y;

            yield return new WaitForSeconds(0.5f);

            Assert.That(body.position.y, Is.EqualTo(startY).Within(0.05f), "Player should be resting on the ground");
        }
    }
}
