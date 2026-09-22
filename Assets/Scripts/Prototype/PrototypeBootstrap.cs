using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BehindTheCurtain.Prototype
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private PrototypeSettings settings = new PrototypeSettings();

        private readonly List<Material> materials = new List<Material>();
        private Transform player;
        private readonly List<Transform> puppets = new List<Transform>();
        private Camera followCamera;
        private enum ControlMode { CursorAim, LockedTurn }

        private ControlMode mode = ControlMode.CursorAim;
        private float yaw, pitch, distance;
        private float targetYaw, targetPitch, targetDistance;
        private float yawVelocity, pitchVelocity, distanceVelocity;
        private bool cursorSuspended;
        private bool orbiting;
        private bool hasFocus = true;

        public Vector3 AimDirection => player != null ? player.forward : Vector3.forward;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Prototype")
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<PrototypeBootstrap>(true) != null)
                    return;
            }

            new GameObject(nameof(PrototypeBootstrap)).AddComponent<PrototypeBootstrap>();
        }

        private void Awake()
        {
            if (gameObject.scene.name != "Prototype")
            {
                enabled = false;
                return;
            }

            var floor = CreatePrimitive(PrimitiveType.Plane, "Floor", Vector3.zero,
                settings.FloorColor);
            // Unity's primitive Plane is 10 by 10 metres.
            floor.localScale = new Vector3(settings.FloorSize / 10f, 1f, settings.FloorSize / 10f);

            player = CreatePrimitive(PrimitiveType.Capsule, "Player",
                Vector3.up * (settings.PlayerHeight / 2f), settings.PlayerColor);
            player.localScale = new Vector3(settings.PlayerRadius * 2f,
                settings.PlayerHeight / 2f, settings.PlayerRadius * 2f);

            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 2; column++)
                {
                    var position = new Vector3((column - 0.5f) * settings.PuppetSpacing,
                        settings.PuppetSize / 2f,
                        settings.PuppetPartyDistance + (row - 0.5f) * settings.PuppetSpacing);
                    var puppet = CreatePrimitive(PrimitiveType.Cube,
                        $"Puppet {row * 2 + column + 1}", position, settings.PuppetColor, true);
                    puppet.localScale = Vector3.one * settings.PuppetSize;
                    puppets.Add(puppet);
                }
            }

            // Reuse scene defaults so the supplied Prototype scene has one camera and light.
            followCamera = FindSceneComponent<Camera>();
            if (followCamera == null)
                followCamera = CreateChild("Main Camera").AddComponent<Camera>();
            followCamera.tag = "MainCamera";
            followCamera.orthographic = false;
            followCamera.fieldOfView = settings.CameraFieldOfView;
            yaw = targetYaw = settings.CameraYaw;
            pitch = targetPitch = Mathf.Clamp(settings.CameraPitch, settings.MinPitch, settings.MaxPitch);
            distance = targetDistance = Mathf.Clamp(settings.CameraDistance, settings.MinDistance, settings.MaxDistance);
            followCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            followCamera.transform.position = CameraPosition();
            SetCursorLocked(false);

            var light = FindSceneComponent<Light>();
            if (light == null)
                light = CreateChild("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(settings.LightEulerAngles);
            gameObject.AddComponent<PrototypeJointAttack>().Initialize(settings, player, puppets);
        }

        private T FindSceneComponent<T>() where T : Component
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>();
                if (component != null)
                    return component;
            }
            return null;
        }

        private GameObject CreateChild(string objectName)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            return child;
        }

        private Transform CreatePrimitive(PrimitiveType type, string objectName, Vector3 position, Color color,
            bool keepCollider = false)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(transform, false);
            primitive.transform.position = position;
            var collider = primitive.GetComponent<Collider>();
            if (!keepCollider)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            var renderer = primitive.GetComponent<Renderer>();
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var template = pipeline != null ? pipeline.defaultMaterial : renderer.sharedMaterial;
            var material = new Material(template);
            // Support the active pipeline (HDRP here), URP, and the built-in pipeline.
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            renderer.sharedMaterial = material;
            materials.Add(material);
            return primitive.transform;
        }

        private void Update()
        {
            if (player == null || followCamera == null)
                return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (!hasFocus)
                return;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                return;
            }

            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                mode = mode == ControlMode.CursorAim ? ControlMode.LockedTurn : ControlMode.CursorAim;
                cursorSuspended = false;
            }

            if (mouse != null && cursorSuspended &&
                (mode == ControlMode.LockedTurn ? mouse.leftButton.wasPressedThisFrame : mouse.middleButton.wasPressedThisFrame))
                cursorSuspended = false;

            orbiting = mouse != null && !cursorSuspended &&
                (mode == ControlMode.LockedTurn || mouse.middleButton.isPressed);
            var wasLocked = Cursor.lockState == CursorLockMode.Locked;
            SetCursorLocked(orbiting);
            if (orbiting && wasLocked)
            {
                var delta = mouse.delta.ReadValue();
                targetYaw = Mathf.Repeat(targetYaw + delta.x * settings.RotateSensitivity, 360f);
                targetPitch = Mathf.Clamp(targetPitch - delta.y * settings.RotateSensitivity,
                    settings.MinPitch, settings.MaxPitch);
            }

            if (mouse != null && !cursorSuspended)
            {
                var scrollSteps = mouse.scroll.ReadValue().y;
                // Native Windows wheel units differ from the uniform Input System range.
                if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                    (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer))
                    scrollSteps /= 120f;
                targetDistance = Mathf.Clamp(targetDistance - scrollSteps * settings.ZoomStep,
                    settings.MinDistance, settings.MaxDistance);
            }

            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, settings.CameraSmoothTime);
            pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, settings.CameraSmoothTime);
            distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVelocity, settings.CameraSmoothTime);
            followCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            if (keyboard == null)
                return;
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);
            var forward = Vector3.ProjectOnPlane(followCamera.transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(followCamera.transform.right, Vector3.up).normalized;
            var position = player.position + (forward * input.y + right * input.x)
                * (settings.MoveSpeed * Time.deltaTime);
            var limit = Mathf.Max(0f, settings.FloorSize / 2f - settings.PlayerRadius);
            position.x = Mathf.Clamp(position.x, -limit, limit);
            position.z = Mathf.Clamp(position.z, -limit, limit);
            player.position = position;
        }

        private Vector3 CameraPosition()
        {
            return player.position - followCamera.transform.forward * distance;
        }

        private void LateUpdate()
        {
            if (player == null || followCamera == null)
                return;

            // Smooth the orbit parameters, keeping its centre on the player.
            followCamera.transform.position = CameraPosition();
            if (mode == ControlMode.LockedTurn)
            {
                player.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            else if (hasFocus && !orbiting && Mouse.current != null)
            {
                var ray = followCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                var floor = new Plane(Vector3.up, Vector3.zero);
                if (floor.Raycast(ray, out var hitDistance))
                {
                    var direction = Vector3.ProjectOnPlane(ray.GetPoint(hitDistance) - player.position, Vector3.up);
                    if (direction.sqrMagnitude > 0f)
                        player.rotation = Quaternion.RotateTowards(player.rotation,
                            Quaternion.LookRotation(direction), settings.TurnSpeed * Time.deltaTime);
                }
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void ReleaseCursor()
        {
            cursorSuspended = true;
            orbiting = false;
            SetCursorLocked(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused)
                ReleaseCursor();
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12f, 142f, 300f, 24f), $"Mode: {mode} (Tab)");
        }

        private void OnDestroy()
        {
            foreach (var material in materials)
                Destroy(material);
        }
    }
}
