using System.Collections.Generic;
using BehindTheCurtain.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BehindTheCurtain.Prototype
{
    /// <summary>Input and presentation adapter. Attack transitions and counter results belong to Core.</summary>
    [DefaultExecutionOrder(100)] // Consume Bootstrap's aim after its LateUpdate.
    public sealed partial class PrototypeJointAttack : MonoBehaviour
    {
        private readonly PrototypeClock clock = new PrototypeClock();
        private readonly List<Material> materials = new List<Material>();
        private readonly HashSet<ConnectionPoint> severedViews = new HashSet<ConnectionPoint>();
        private PrototypeSettings settings;
        private Transform player;
        private Transform[] puppets;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private LineRenderer[] threads;
        private Transform[] markers;
        private JointAttackPlan plan;
        private JointAttack attack;
        private long nextAttackTick;
        private long resultStartTick = -1;
        private long rangeMessageUntil;
        private string lastReason = "None";
        private string rangeMessage;

        private InterferenceMethod? pendingMethod;
        private int aimTargetIndex = -1;
        private long actionStartTick;
        private long shootReadyTick;
        private long cancelMessageUntil;
        private string cancelMessage;
        private Vector3 actionPosition;
        private LineRenderer aimLine;
        private LineRenderer disruptCircle;
        private PrototypeBootstrap controls;
        private Transform shootPreview;
        private Material shootPreviewMaterial;

        public void Initialize(PrototypeSettings configuration, Transform playerTransform,
            IReadOnlyList<Transform> puppetTransforms)
        {
            settings = configuration;
            player = playerTransform;
            controls = GetComponent<PrototypeBootstrap>();
            if (!InitializeExpedition())
            {
                lastReason = "Invalid expedition settings or plan";
                enabled = false;
                return;
            }
            var points = plan.ConnectionPoints;
            if (puppetTransforms.Count != 4 || settings.AttackWaitTicks < 1 ||
                settings.ResultTicks < settings.CollapseTicks || settings.CollapseTicks < 1 ||
                settings.ShakeTicks < 1 || settings.FlashTicks < 1 || settings.ThreadBlinkTicks < 1 ||
                settings.ShootAimTicks < 1 || settings.ShootCooldownTicks < 0 ||
                settings.DisruptChannelTicks < 1 || settings.CancelMessageTicks < 1 ||
                settings.DisruptCircleSegments < 3 || settings.ShootMessageTicks < 1 ||
                !(settings.ShootConeDegrees >= 0f && settings.ShootConeDegrees <= 180f) ||
                !(settings.ShootOriginHeight >= 0f) || float.IsInfinity(settings.ShootOriginHeight) ||
                !(settings.ShootOcclusionMargin >= 0f) || float.IsInfinity(settings.ShootOcclusionMargin) ||
                !(settings.ConnectionSurfaceOffset >= 0f) || float.IsInfinity(settings.ConnectionSurfaceOffset) ||
                !(settings.ShootPreviewScale > 1f) || !ValidPressureSettings())
            {
                lastReason = "Invalid prototype presentation settings";
                enabled = false;
                return;
            }

            puppets = new Transform[puppetTransforms.Count];
            restPositions = new Vector3[puppets.Length];
            restRotations = new Quaternion[puppets.Length];
            threads = new LineRenderer[puppets.Length];
            markers = new Transform[points.Count];
            for (var i = 0; i < puppets.Length; i++)
            {
                puppets[i] = puppetTransforms[i];
                restPositions[i] = puppets[i].position;
                restRotations[i] = puppets[i].rotation;
                var threadObject = new GameObject($"Control Thread {i + 1}");
                threadObject.transform.SetParent(transform, false);
                var line = threadObject.AddComponent<LineRenderer>();
                line.sharedMaterial = CreateUnlitMaterial(settings.ThreadStartColor);
                line.useWorldSpace = true;
                line.positionCount = 3;
                line.widthMultiplier = settings.ThreadWidth;
                line.enabled = false;
                threads[i] = line;
                if (i >= markers.Length)
                    continue;
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Connection {points[i].AllowedMethods}";
                sphere.transform.SetParent(transform, false);
                sphere.transform.localScale = Vector3.one * settings.ConnectionDiameter;
                var collider = sphere.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                sphere.GetComponent<Renderer>().sharedMaterial = CreateUnlitMaterial(MethodColor(points[i].AllowedMethods));
                sphere.SetActive(false);
                markers[i] = sphere.transform;
            }
            aimLine = CreateCounterLine("Shoot Aim", settings.ShootColor, 2, false);
            var preview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            preview.name = "Shoot Target Preview";
            preview.transform.SetParent(transform, false);
            preview.transform.localScale = Vector3.one * settings.ConnectionDiameter * settings.ShootPreviewScale;
            var previewCollider = preview.GetComponent<Collider>();
            previewCollider.enabled = false;
            Destroy(previewCollider);
            shootPreviewMaterial = CreateUnlitMaterial(settings.ShootPreviewColor);
            ConfigureTransparentPreview(shootPreviewMaterial);
            var previewRenderer = preview.GetComponent<Renderer>();
            previewRenderer.sharedMaterial = shootPreviewMaterial;
            previewRenderer.shadowCastingMode = ShadowCastingMode.Off;
            previewRenderer.receiveShadows = false;
            shootPreview = preview.transform;
            preview.SetActive(false);
            disruptCircle = CreateCounterLine("Disrupt Range", settings.DisruptColor,
                settings.DisruptCircleSegments, true);
            InitializePressure();
            InitializePlayerAttacks();
            if (!InitializeBrain()) return;
            nextAttackTick = clock.Tick + settings.AttackWaitTicks;
        }

        private LineRenderer CreateCounterLine(string name, Color color, int count, bool loop)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = CreateUnlitMaterial(color);
            line.useWorldSpace = true;
            line.positionCount = count;
            line.loop = loop;
            line.widthMultiplier = settings.CounterLineWidth;
            line.enabled = false;
            return line;
        }

        private bool WindowOpen => attack != null && attack.State == JointAttackState.CounterWindowOpen
            && attack.CounterWindow.Contains(clock.Tick);

        private static bool HasMovementInput()
        {
            var keyboard = Keyboard.current;
            return Application.isFocused && keyboard != null &&
                (keyboard.wKey.isPressed || keyboard.aKey.isPressed ||
                 keyboard.sKey.isPressed || keyboard.dKey.isPressed);
        }

        private void CancelAction(string reason)
        {
            pendingMethod = null;
            aimTargetIndex = -1;
            if (reason == null) return;
            cancelMessage = $"Cancelled: {reason}";
            cancelMessageUntil = clock.Tick + settings.CancelMessageTicks;
        }

        private Color MethodColor(InterferenceMethod method)
        {
            switch (method)
            {
                case InterferenceMethod.Cut: return settings.CutColor;
                case InterferenceMethod.Shoot: return settings.ShootColor;
                default: return settings.DisruptColor;
            }
        }

        private Material CreateUnlitMaterial(Color color)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var pipelineName = pipeline == null ? "" : pipeline.GetType().Name;
            var shaderName = pipelineName.Contains("HDRender") ? "HDRP/Unlit"
                : pipelineName.Contains("Universal") ? "Universal Render Pipeline/Unlit" : "Unlit/Color";
            var shader = Shader.Find(shaderName);
            var material = shader != null ? new Material(shader) : new Material(pipeline.defaultMaterial);
            SetMaterialColor(material, color);
            materials.Add(material);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private void UpdateCounter()
        {
            if (plan == null || puppets == null)
                return;
            var keyboard = Keyboard.current;
            if (pendingMethod.HasValue && Application.isFocused && keyboard != null &&
                ((keyboard.digit1Key.wasPressedThisFrame && pendingMethod != InterferenceMethod.Cut) ||
                 (keyboard.digit2Key.wasPressedThisFrame && pendingMethod != InterferenceMethod.Shoot) ||
                 (keyboard.digit3Key.wasPressedThisFrame && pendingMethod != InterferenceMethod.Disrupt)))
                CancelAction(null);
            // Check before advancing: movement on the completion frame must cancel too.
            if (pendingMethod.HasValue && (HasMovementInput() || player.position != actionPosition))
                CancelAction("Movement");
            clock.Advance(Time.deltaTime, OnTick);
            if (!enabled)
                return;
            // Frame input samples the same interpolated bodies that are shown on screen.
            UpdatePresentation();
            Physics.SyncTransforms();
            if (Application.isFocused && keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) TryCounter(InterferenceMethod.Cut);
                if (keyboard.digit2Key.wasPressedThisFrame) TryCounter(InterferenceMethod.Shoot);
                if (keyboard.digit3Key.wasPressedThisFrame) TryCounter(InterferenceMethod.Disrupt);
            }
            ObserveResult();
            UpdatePresentation();
            Physics.SyncTransforms();
            UpdateHitPresentation();
        }

        private void OnTick(long tick)
        {
            if (!enabled)
                return;
            if (resultStartTick >= 0 && tick - resultStartTick >= settings.ResultTicks)
            {
                attack = null;
                resultStartTick = -1;
                severedViews.Clear();
                nextAttackTick = tick + settings.AttackWaitTicks;
                completedCycles++;
                if (completedCycles >= settings.EncounterCycles && !FinishEncounter(tick)) return;
            }
            if (intermission && tick >= intermissionUntil)
            {
                intermission = false;
                completedCycles = 0;
            }
            if (!intermission && attack == null && tick >= nextAttackTick)
            {
                if (!plan.TryCreateAttack(tick, out attack))
                {
                    lastReason = "Attack schedule rejected";
                    enabled = false;
                    return;
                }
                StartPressureCycle();
            }
            if (attack != null)
                attack.Advance(tick);
            if (!intermission && !AdvanceBrain(tick)) return;
            AdvancePressure(tick);
            AdvanceCounterAction();
            ObserveResult();
        }

        private void AdvanceCounterAction()
        {
            if (!pendingMethod.HasValue) return;
            if (!WindowOpen)
            {
                CancelAction("Window closed");
                return;
            }
            var method = pendingMethod.Value;
            if (method == InterferenceMethod.Shoot)
            {
                if (!IsShootCandidate(aimTargetIndex) || ShootAngle(aimTargetIndex) > settings.ShootConeDegrees)
                {
                    RejectShoot("조준 벗어남");
                    return;
                }
                if (IsShootOccluded(aimTargetIndex))
                {
                    RejectShoot("가려짐");
                    return;
                }
            }
            var duration = method == InterferenceMethod.Shoot ? settings.ShootAimTicks : settings.DisruptChannelTicks;
            if (clock.Tick - actionStartTick < duration) return;
            var targetIndex = aimTargetIndex;
            CancelAction(null);
            if (method == InterferenceMethod.Shoot)
            {
                ResolveCounter(method, attack.ConnectionPoints[targetIndex]);
                shootReadyTick = clock.Tick + settings.ShootCooldownTicks;
                return;
            }
            var found = false;
            for (var i = 0; i < attack.ConnectionPoints.Count; i++)
            {
                var point = attack.ConnectionPoints[i];
                if (severedViews.Contains(point) || (point.AllowedMethods & method) == 0 ||
                    Vector3.Distance(player.position, ConnectionPosition(i)) > settings.DisruptRange)
                    continue;
                found = true;
                ResolveCounter(method, point);
            }
            if (!found) CancelAction("No target");
        }

        private void ObserveResult()
        {
            if (attack == null || resultStartTick >= 0)
                return;
            switch (attack.State)
            {
                case JointAttackState.Broken:
                case JointAttackState.Weakened:
                case JointAttackState.Executed:
                    resultStartTick = clock.Tick;
                    break;
            }
        }

        private Vector3 ConnectionPosition(int index)
        {
            var direction = Vector3.forward;
            switch (plan.ConnectionPoints[index].Position)
            {
                case ConnectionPointPosition.Left: direction = Vector3.left; break;
                case ConnectionPointPosition.Right: direction = Vector3.right; break;
                case ConnectionPointPosition.Rear: direction = Vector3.back; break;
            }
            // Primitive cubes are centred at half their height; local faces are at +/- 0.5.
            var puppet = puppets[index];
            return puppet.TransformPoint(direction * 0.5f)
                + puppet.rotation * direction * settings.ConnectionSurfaceOffset;
        }

        private Vector3 ShootOrigin => player.position
            + Vector3.up * (settings.ShootOriginHeight - settings.PlayerHeight / 2f);

        private float ShootRayLength(float distance)
            => Mathf.Max(0f, distance - settings.ShootOcclusionMargin);

        private bool IsShootCandidate(int index)
            => WindowOpen && index >= 0 && index < attack.ConnectionPoints.Count &&
                !severedViews.Contains(attack.ConnectionPoints[index]) &&
                (attack.ConnectionPoints[index].AllowedMethods & InterferenceMethod.Shoot) != 0;

        private float ShootAngle(int index)
        {
            var direction = Vector3.ProjectOnPlane(ConnectionPosition(index) - player.position, Vector3.up);
            var aim = Vector3.ProjectOnPlane(controls != null ? controls.AimDirection : player.forward, Vector3.up);
            return direction.sqrMagnitude > 0f && aim.sqrMagnitude > 0f
                ? Vector3.Angle(aim, direction) : float.PositiveInfinity;
        }

        private int SelectShootTarget()
        {
            if (!WindowOpen) return -1;
            var selected = -1;
            var smallestAngle = float.PositiveInfinity;
            for (var i = 0; i < attack.ConnectionPoints.Count; i++)
            {
                if (!IsShootCandidate(i)) continue;
                var angle = ShootAngle(i);
                if (angle <= settings.ShootConeDegrees && angle < smallestAngle)
                {
                    selected = i;
                    smallestAngle = angle;
                }
            }
            return selected;
        }

        private bool IsPuppetBody(Collider collider, int targetIndex)
        {
            if (collider.transform.IsChildOf(player) || collider.transform.IsChildOf(markers[targetIndex]))
                return false;
            foreach (var puppet in puppets)
                if (collider.transform.IsChildOf(puppet)) return true;
            return false;
        }

        private bool IsShootOccluded(int index)
        {
            var origin = ShootOrigin;
            var offset = ConnectionPosition(index) - origin;
            var distance = offset.magnitude;
            var rayLength = ShootRayLength(distance);
            if (rayLength <= 0f) return false;
            var ray = new Ray(origin, offset / distance);
            if (!Physics.Raycast(ray, out var first, rayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (IsPuppetBody(first.collider, index)) return true;
            // Ignore self, connection markers and non-body geometry without hiding bodies behind them.
            foreach (var hit in Physics.RaycastAll(ray, rayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (IsPuppetBody(hit.collider, index)) return true;
            return false;
        }

        private void RejectShoot(string reason)
        {
            CancelAction(null);
            cancelMessage = reason;
            cancelMessageUntil = clock.Tick + settings.ShootMessageTicks;
        }

        private static void ConfigureTransparentPreview(Material material)
        {
            // Explicit blend state for HDRP, URP and the built-in pipeline.
            if (material.shader.name == "Unlit/Color")
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) material.shader = shader;
            }
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SurfaceType", 1f);
            material.SetFloat("_BlendMode", 0f);
            // HDRP premultiplies alpha in the shader, including its Alpha blend mode.
            material.SetInt("_SrcBlend", (int)(material.shader.name == "HDRP/Unlit"
                ? BlendMode.One : BlendMode.SrcAlpha));
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_AlphaSrcBlend", (int)BlendMode.One);
            material.SetInt("_AlphaDstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_TransparentZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void TryCounter(InterferenceMethod method)
        {
            if (pendingMethod == method) return;
            CancelAction(null);
            if (method == InterferenceMethod.Shoot && clock.Tick < shootReadyTick)
            {
                lastReason = "Shoot: cooldown";
                return;
            }
            if (method != InterferenceMethod.Cut && !WindowOpen)
            {
                CancelAction("Window closed");
                return;
            }
            ConnectionPoint target = null;
            var targetIndex = -1;
            var closestDistance = float.PositiveInfinity;
            if (method == InterferenceMethod.Shoot)
            {
                targetIndex = SelectShootTarget();
                if (targetIndex < 0)
                {
                    RejectShoot("조준 대상 없음");
                    return;
                }
                if (IsShootOccluded(targetIndex))
                {
                    RejectShoot("가려짐");
                    return;
                }
                target = attack.ConnectionPoints[targetIndex];
                closestDistance = Vector3.Distance(player.position, ConnectionPosition(targetIndex));
            }
            else if (attack != null && attack.State == JointAttackState.CounterWindowOpen)
            {
                for (var i = 0; i < attack.ConnectionPoints.Count; i++)
                {
                    var point = attack.ConnectionPoints[i];
                    if (severedViews.Contains(point) || (point.AllowedMethods & method) == 0)
                        continue;
                    var distance = Vector3.Distance(player.position, ConnectionPosition(i));
                    if (distance < closestDistance)
                    {
                        target = point;
                        targetIndex = i;
                        closestDistance = distance;
                    }
                }
            }
            var range = method == InterferenceMethod.Cut ? settings.CutRange
                : method == InterferenceMethod.Disrupt ? settings.DisruptRange : float.PositiveInfinity;
            if (method != InterferenceMethod.Cut)
            {
                if (target == null || closestDistance > range)
                {
                    CancelAction("No target");
                    return;
                }
                if (HasMovementInput())
                {
                    CancelAction("Movement");
                    return;
                }
                pendingMethod = method;
                aimTargetIndex = targetIndex;
                actionStartTick = clock.Tick;
                actionPosition = player.position;
                rangeMessage = null;
                return;
            }
            if (target != null && closestDistance > range)
            {
                rangeMessage = $"{method}: out of range ({closestDistance:F1}m / {range:F1}m)";
                rangeMessageUntil = clock.Tick + settings.RangeMessageTicks;
                return;
            }
            ResolveCounter(method, target);
        }

        private void ResolveCounter(InterferenceMethod method, ConnectionPoint target)
        {
            var result = CounterResolver.Resolve(attack, clock.Tick, method, target);
            lastReason = $"{method}: {(result.Success ? "Success" : result.FailureReason.ToString())}";
            rangeMessage = null;
            if (result.Success)
                severedViews.Add(target); // Presentation cache, updated only from Core success.
        }

        private void UpdatePresentation()
        {
            var telegraphing = attack != null && attack.State == JointAttackState.Telegraphing;
            var windowOpen = attack != null && attack.State == JointAttackState.CounterWindowOpen;
            var tension = telegraphing ? Mathf.Clamp01((float)(clock.Tick - attack.TelegraphStartTick)
                / Mathf.Max(1f, plan.TelegraphTicks)) : 1f;
            var threadColor = Color.Lerp(settings.ThreadStartColor, settings.ThreadTautColor, tension);
            if (windowOpen)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin((float)(clock.Tick % settings.ThreadBlinkTicks)
                    / settings.ThreadBlinkTicks * Mathf.PI * 2f);
                threadColor = Color.Lerp(settings.ThreadTautColor, settings.ThreadBlinkColor, pulse);
            }
            for (var i = 0; i < puppets.Length; i++)
            {
                puppets[i].position = Vector3.Lerp(previousPositions[i], restPositions[i], clock.Interpolation);
                puppets[i].rotation = Quaternion.Slerp(previousRotations[i], restRotations[i], clock.Interpolation);
                if (resultStartTick >= 0)
                {
                    var elapsed = clock.Tick - resultStartTick;
                    if (attack.State == JointAttackState.Broken)
                    {
                        var progress = Mathf.Clamp01((float)elapsed / settings.CollapseTicks);
                        puppets[i].rotation = restRotations[i] * Quaternion.Euler(0f, 0f, settings.CollapseAngle * progress);
                        puppets[i].position -= Vector3.up * (settings.CollapseDrop * progress);
                    }
                    else if (attack.State == JointAttackState.Weakened && elapsed < settings.ShakeTicks)
                    {
                        var progress = (float)elapsed / settings.ShakeTicks;
                        var angle = Mathf.Sin(progress * Mathf.PI * 2f * settings.ShakeCycles)
                            * settings.ShakeAngle * (1f - progress);
                        puppets[i].rotation = restRotations[i] * Quaternion.Euler(0f, 0f, angle);
                    }
                }
                var severed = i < markers.Length && severedViews.Contains(plan.ConnectionPoints[i]);
                var line = threads[i];
                line.enabled = (telegraphing || windowOpen) && !severed;
                if (line.enabled)
                {
                    var bottom = i < markers.Length ? ConnectionPosition(i)
                        : puppets[i].position + Vector3.up * (settings.PuppetSize / 2f);
                    var top = i < markers.Length ? bottom + Vector3.up * settings.ThreadHeight
                        : puppets[i].position + Vector3.up * settings.ThreadHeight;
                    line.SetPosition(0, bottom);
                    line.SetPosition(1, (bottom + top) / 2f + Vector3.right * (settings.ThreadSag * (1f - tension)));
                    line.SetPosition(2, top);
                    SetMaterialColor(line.sharedMaterial, threadColor);
                }
                if (i < markers.Length)
                {
                    markers[i].position = ConnectionPosition(i);
                    markers[i].gameObject.SetActive(windowOpen && !severed);
                }
            }
        }

        private void LateUpdate()
        {
            if (plan == null || puppets == null) return;
            if (Application.isFocused && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                debugOverlay = !debugOverlay;
            Physics.SyncTransforms();
            UpdateCounter();
            UpdateBrainLines();
            UpdatePlayerAttacks();
            if (aimLine == null) return;
            var previewIndex = SelectShootTarget();
            shootPreview.gameObject.SetActive(previewIndex >= 0);
            if (previewIndex >= 0)
            {
                shootPreview.position = ConnectionPosition(previewIndex);
                SetMaterialColor(shootPreviewMaterial, IsShootOccluded(previewIndex)
                    ? settings.ShootOccludedPreviewColor : settings.ShootPreviewColor);
            }
            // Also catch movement performed by another component later in Update.
            if (pendingMethod.HasValue && player.position != actionPosition)
                CancelAction("Movement");
            aimLine.enabled = pendingMethod == InterferenceMethod.Shoot;
            if (aimLine.enabled)
            {
                var origin = ShootOrigin;
                var offset = ConnectionPosition(aimTargetIndex) - origin;
                aimLine.SetPosition(0, origin);
                aimLine.SetPosition(1, origin + offset.normalized * ShootRayLength(offset.magnitude));
            }
            disruptCircle.enabled = pendingMethod == InterferenceMethod.Disrupt;
            if (disruptCircle.enabled)
            {
                for (var i = 0; i < settings.DisruptCircleSegments; i++)
                {
                    var angle = i * Mathf.PI * 2f / settings.DisruptCircleSegments;
                    disruptCircle.SetPosition(i, new Vector3(player.position.x + Mathf.Cos(angle) * settings.DisruptRange,
                        settings.DisruptCircleHeight, player.position.z + Mathf.Sin(angle) * settings.DisruptRange));
                }
            }
        }

        private void DrawCounterProgress()
        {
            if (player == null || Camera.main == null) return;
            var screen = Camera.main.WorldToScreenPoint(player.position + Vector3.up *
                (settings.PlayerHeight / 2f + settings.ProgressHeight));
            if (screen.z <= 0f) return;
            var rect = new Rect(screen.x - settings.ProgressWidth / 2f, Screen.height - screen.y,
                settings.ProgressWidth, settings.ProgressBarHeight);
            if (pendingMethod.HasValue)
            {
                var previous = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                var duration = pendingMethod == InterferenceMethod.Shoot ? settings.ShootAimTicks : settings.DisruptChannelTicks;
                rect.width *= Mathf.Clamp01((float)(clock.Tick - actionStartTick) / duration);
                GUI.color = MethodColor(pendingMethod.Value);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = previous;
            }
            if (clock.Tick < cancelMessageUntil)
                GUI.Label(new Rect(rect.x, rect.y - 24f, 240f, 24f), cancelMessage);
        }

        private void OnGUI()
        {
            if (settings == null)
                return;
            DrawResultOverlay();
            DrawCounterProgress();
            DrawExpedition();
            DrawBrainOverlay();
            var remaining = attack != null && attack.State == JointAttackState.CounterWindowOpen
                ? System.Math.Max(0, attack.CounterWindow.End - clock.Tick + 1) : 0;
            var required = plan != null ? plan.ConnectionPoints.Count : 0;
            var broken = attack != null ? (int)System.Math.Round((1d - attack.Severity) * required) : 0;
            GUI.Box(new Rect(8f, 178f, 540f, 220f), "");
            GUI.Label(new Rect(12f, 182f, 530f, 210f),
                $"Stage: {(attack == null ? "Waiting" : attack.State.ToString())}\n"
                + $"Window ticks: {remaining} | Connections: {broken}/{required}\n"
                + $"Last result: {lastReason}\n1: Cut ({settings.CutRange:g}m)   2: Shoot   3: Disrupt ({settings.DisruptRange:g}m)\n"
                + $"Shoot cooldown: {System.Math.Max(0, shootReadyTick - clock.Tick) / (float)PrototypeSettings.TicksPerSecond:F1}s | 이번 사이클 피격: {cycleHitCount}\n"
                + (clock.Tick < rangeMessageUntil ? rangeMessage : "")
                + "\n" + ConnectionSummary());
        }

        private void DrawResultOverlay()
        {
            if (resultStartTick < 0 || attack == null)
                return;
            var progress = Mathf.Clamp01((float)(clock.Tick - resultStartTick) / settings.FlashTicks);
            var previous = GUI.color;
            if (attack.State == JointAttackState.Executed)
            {
                var color = settings.ExecutedScreenColor;
                color.a *= 1f - progress;
                GUI.color = color;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            else if (attack.State == JointAttackState.Weakened)
            {
                var color = settings.WeakenedScreenColor;
                color.a *= 1f - progress;
                GUI.color = color;
                var edge = Mathf.Min(Screen.width, Screen.height) * settings.ScreenEdgeFraction;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, edge), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, Screen.height - edge, Screen.width, edge), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, edge, edge, Screen.height - 2f * edge), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width - edge, edge, edge, Screen.height - 2f * edge), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (slamMesh != null) Destroy(slamMesh);
            foreach (var material in materials)
                Destroy(material);
        }
    }
}
