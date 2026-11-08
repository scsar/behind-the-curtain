using BehindTheCurtain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BehindTheCurtain.Prototype
{
    public sealed partial class PrototypeJointAttack
    {
        private sealed class MarionetteView
        {
            public Transform Root, Body, Torso, Head, RightArm, LeftArm, RightLeg, LeftLeg, Hand, LeftHand;
            public LineRenderer[] Strings;
            public long FallenTick = -1;
            public Vector3[] FallenStringBottom, FallenStringTop;
            public long AttackStart = -1, ImpactTick = -1;
        }
        private MarionetteView[] marionettes;
        private bool showMarionettes = true;

        private Transform VisualPivot(Transform parent, string name, Vector3 position)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            return pivot;
        }

        private Transform VisualPart(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private void InitializeMarionettes()
        {
            marionettes = new MarionetteView[puppets.Length];
            var dark = CreateUnlitMaterial(settings.MarionetteDarkColor);
            for (var i = 0; i < puppets.Length; i++)
            {
                var v = new MarionetteView();
                marionettes[i] = v;
                // Root scale already supplies PuppetSize. Its origin is half a size above the floor.
                v.Root = VisualPivot(puppets[i], "Marionette", Vector3.zero);
                v.Body = VisualPivot(v.Root, "Body pivot", Vector3.zero);
                var height = settings.MarionetteTorsoHeight - 0.5f;
                v.Torso = VisualPart(v.Body, "Torso", PrimitiveType.Cube, Vector3.up * height,
                    settings.MarionetteTorsoSize, puppetMaterials[i]);
                var headHeight = height + settings.MarionetteTorsoSize.y / 2f + settings.MarionetteHeadDiameter / 2f;
                v.Head = VisualPivot(v.Body, "Head pivot", Vector3.up * headHeight);
                VisualPart(v.Head, "Head", PrimitiveType.Sphere, Vector3.zero,
                    Vector3.one * settings.MarionetteHeadDiameter, puppetMaterials[i]);
                VisualPart(v.Head, "Blindfold", PrimitiveType.Cube,
                    new Vector3(0f, 0f, settings.MarionetteHeadDiameter / 2f), settings.MarionetteFaceSize, dark);
                v.RightArm = VisualPivot(v.Body, "Right shoulder", new Vector3(settings.MarionetteShoulderX, height + settings.MarionetteTorsoSize.y / 2f, 0f));
                v.LeftArm = VisualPivot(v.Body, "Left shoulder", new Vector3(-settings.MarionetteShoulderX, v.RightArm.localPosition.y, 0f));
                v.RightLeg = VisualPivot(v.Body, "Right hip", new Vector3(settings.MarionetteHipX, height - settings.MarionetteTorsoSize.y / 2f, 0f));
                v.LeftLeg = VisualPivot(v.Body, "Left hip", new Vector3(-settings.MarionetteHipX, v.RightLeg.localPosition.y, 0f));
                foreach (var limb in new[] { v.RightArm, v.LeftArm, v.RightLeg, v.LeftLeg })
                {
                    var length = limb == v.RightArm || limb == v.LeftArm ? settings.MarionetteArmLength : settings.MarionetteLegLength;
                    VisualPart(limb, "Limb", PrimitiveType.Cylinder, Vector3.down * length / 2f,
                        new Vector3(settings.MarionetteLimbWidth, length / 2f, settings.MarionetteLimbWidth), puppetMaterials[i]);
                }
                v.Hand = VisualPivot(v.RightArm, "Right hand", Vector3.down * settings.MarionetteArmLength);
                v.LeftHand = VisualPivot(v.LeftArm, "Left hand", Vector3.down * settings.MarionetteArmLength);
                v.Strings = new LineRenderer[3];
                for (var j = 0; j < 3; j++)
                {
                    var line = VisualPivot(v.Root, "Suspension string", Vector3.zero).gameObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = dark;
                    line.positionCount = 3;
                    line.useWorldSpace = true;
                    line.widthMultiplier = settings.MarionetteStringWidth * settings.PuppetSize;
                    v.Strings[j] = line;
                }
            }
        }

        private void MountMarionetteEquipment(int i)
        {
            var v = marionettes[i];
            // Torso geometry is scaled: use a unit-scale socket on the visual root for equipment.
            var back = v.Body.Find("Back socket") ?? VisualPivot(v.Body, "Back socket", v.Torso.localPosition);
            puppets[i].GetComponent<PrototypeEquipmentView>().Mount(v.Hand, v.LeftArm, back, settings, showMarionettes);
            puppets[i].GetComponent<MeshRenderer>().enabled = !showMarionettes;
            v.Root.gameObject.SetActive(showMarionettes);
        }

        private void UpdateFallenMarionette(MarionetteView v, PuppetStatus status)
        {
            if (v.FallenTick < 0)
            {
                v.FallenTick = clock.Tick;
                v.FallenStringBottom = new Vector3[v.Strings.Length];
                v.FallenStringTop = new Vector3[v.Strings.Length];
                for (var j = 0; j < v.Strings.Length; j++)
                {
                    v.FallenStringBottom[j] = v.Strings[j].GetPosition(0);
                    v.FallenStringTop[j] = v.Strings[j].GetPosition(2);
                }
                v.Root.localPosition = Vector3.down * settings.FallenDrop;
                v.Root.localRotation = Quaternion.Euler(0f, 0f,
                    status == PuppetStatus.Broken ? settings.BrokenBodyAngle : 0f);
                v.Body.localRotation = Quaternion.Euler(status == PuppetStatus.Broken ? 0f : settings.ReleasedBodyAngle, 0f, 0f);
                v.Head.localRotation = Quaternion.identity;
                v.RightArm.localRotation = Quaternion.Euler(0f, 0f, settings.LimpArmAngle);
                v.LeftArm.localRotation = Quaternion.Euler(0f, 0f, -settings.LimpArmAngle);
                v.RightLeg.localRotation = Quaternion.Euler(settings.LimpLegAngle, 0f, 0f);
                v.LeftLeg.localRotation = Quaternion.Euler(-settings.LimpLegAngle, 0f, 0f);
            }
            var duration = Mathf.Max(1f, settings.ReclaimRetractSeconds * PrototypeSettings.TicksPerSecond);
            var progress = Mathf.Clamp01((float)(clock.Tick + clock.Interpolation - v.FallenTick) / duration);
            for (var j = 0; j < v.Strings.Length; j++)
            {
                var line = v.Strings[j];
                line.enabled = status == PuppetStatus.Broken && progress < 1f;
                if (!line.enabled) continue;
                var top = v.FallenStringTop[j];
                var bottom = Vector3.Lerp(v.FallenStringBottom[j], top, progress);
                line.SetPosition(0, bottom);
                line.SetPosition(1, (bottom + top) / 2f);
                line.SetPosition(2, top);
            }
        }

        private void UpdateMarionettes()
        {
            if (marionettes == null || brain == null) return;
            if (Application.isFocused && Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                showMarionettes = !showMarionettes;
                for (var i = 0; i < puppets.Length; i++) MountMarionetteEquipment(i);
            }
            var blend = settings.PoseBlendSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / settings.PoseBlendSeconds);
            for (var i = 0; i < puppets.Length; i++)
            {
                var v = marionettes[i];
                var agent = expedition.EncounterWorld.Get(PuppetAgentId(i)).Value;
                var state = expedition.EncounterWorld.GetPuppetState(agent.Id).Value;
                if (!state.IsParticipating)
                {
                    UpdateFallenMarionette(v, state.Status);
                    continue;
                }
                var decision = brain.GetDecision(agent.Id);
                HarassPhase? phase = decision.Success ? decision.Value.HarassPhase : null;
                var movement = restPositions[i] - previousPositions[i];
                var local = Quaternion.Inverse(restRotations[i]) * movement;
                var speed = movement.magnitude * PrototypeSettings.TicksPerSecond;
                var amount = Mathf.Clamp01(speed / Mathf.Max(settings.WalkReferenceSpeed, Mathf.Epsilon));
                var time = Time.time;
                var gait = Mathf.Sin(time * settings.WalkFrequency + i * settings.MarionettePhaseOffset) * settings.WalkLegAngle * amount;
                var body = new Vector3(local.normalized.z * settings.MoveLeanAngle * amount, 0f, -local.normalized.x * settings.MoveLeanAngle * amount);
                if (state.Posture < expedition.EncounterWorld.StateSettings.PostureStaggerThreshold)
                    body.x += settings.StaggerLeanAngle;
                var offset = Vector3.zero;
                if (speed == 0f) offset.y = Mathf.Sin(time * settings.IdleBobFrequency + i * settings.MarionettePhaseOffset) * settings.IdleBobHeight;
                var right = Vector3.zero;
                var left = Vector3.zero;
                var rightLeg = new Vector3(gait, 0f, 0f);
                var leftLeg = new Vector3(-gait, 0f, 0f);
                if (phase == HarassPhase.Waiting)
                {
                    offset.y -= settings.WaitingDrop;
                    right.x = settings.AimArmAngle;
                    rightLeg.x += settings.WaitingLegAngle;
                    leftLeg.x += settings.WaitingLegAngle;
                }
                else if (phase == HarassPhase.Strike)
                {
                    offset.z += settings.LungeDistance;
                    right.x = settings.AimArmAngle;
                }
                else if (phase == HarassPhase.Recover)
                {
                    body.x += settings.RecoverLeanAngle;
                    right.x = settings.RecoverArmAngle;
                }
                else if (phase == HarassPhase.Withdraw) body.x += settings.WithdrawLeanAngle;
                else if (agent.CurrentAction == ActionIds.Intercept)
                {
                    if (puppets[i].GetComponent<PrototypeEquipmentView>().HasShield) left.x = settings.AimArmAngle;
                    else { right.z = settings.InterceptArmAngle; left.z = -settings.InterceptArmAngle; }
                    rightLeg.z = settings.InterceptLegAngle;
                    leftLeg.z = -settings.InterceptLegAngle;
                }
                else if (agent.CurrentAction == ActionIds.Attack) right.x = settings.AimArmAngle;
                // A recorded slam owns the weapon until its swing completes, even if Core selects another action.
                var slamActive = v.AttackStart >= 0 && clock.Tick < v.ImpactTick + Mathf.Max(1, settings.SwingTicks);
                if (slamActive)
                {
                    var tick = clock.Tick + clock.Interpolation;
                    right.x = clock.Tick < v.ImpactTick
                        ? Mathf.Lerp(settings.AimArmAngle, settings.AttackRaisedAngle,
                            Mathf.Clamp01((float)(tick - v.AttackStart) / Mathf.Max(1, settings.SlamWarningTicks)))
                        : Mathf.Lerp(settings.AttackRaisedAngle, settings.AttackSwingAngle,
                            Mathf.Clamp01((float)(tick - v.ImpactTick) / Mathf.Max(1, settings.SwingTicks)));
                }
                var temperament = expedition.EncounterWorld.GetTemperament(agent.Id).Value;
                var boldness = (float)temperament[TemperamentAxisIds.Boldness];
                if (boldness > 0f)
                {
                    body.x += boldness * settings.BoldLeanAngle;
                    offset.z += boldness * settings.BoldChestForward;
                }
                else
                {
                    offset.y += boldness * settings.CautiousCrouch;
                    body.x += boldness * settings.CautiousLeanAngle;
                    right.x += boldness * settings.CautiousArmAngle;
                }
                // Target yaw is measured from the puppet root's front, never its leaning body.
                var headYaw = 0f;
                if (decision.Success && decision.Value.Target.HasValue)
                {
                    var target = expedition.EncounterWorld.Get(decision.Value.Target.Value);
                    if (target.Success)
                    {
                        var direction = new Vector3((float)target.Value.Position.X, puppets[i].position.y,
                            (float)target.Value.Position.Z) - puppets[i].position;
                        var facing = Quaternion.Inverse(puppets[i].rotation) * direction;
                        if (facing.sqrMagnitude > Mathf.Epsilon)
                            headYaw = Mathf.Clamp(Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg,
                                -Mathf.Abs(settings.HeadTurnLimit), Mathf.Abs(settings.HeadTurnLimit));
                    }
                }
                v.Head.localRotation = Quaternion.Slerp(v.Head.localRotation, Quaternion.Euler(0f, headYaw, 0f), blend);
                v.Root.localPosition = Vector3.Lerp(v.Root.localPosition, offset, blend);
                v.Body.localRotation = Quaternion.Slerp(v.Body.localRotation, Quaternion.Euler(body), blend);
                v.RightArm.localRotation = Quaternion.Slerp(v.RightArm.localRotation, Quaternion.Euler(right), slamActive ? 1f : blend);
                v.LeftArm.localRotation = Quaternion.Slerp(v.LeftArm.localRotation, Quaternion.Euler(left), blend);
                v.RightLeg.localRotation = Quaternion.Slerp(v.RightLeg.localRotation, Quaternion.Euler(rightLeg), blend);
                v.LeftLeg.localRotation = Quaternion.Slerp(v.LeftLeg.localRotation, Quaternion.Euler(leftLeg), blend);
                var sources = new[] { v.Head, v.Hand, v.LeftHand };
                for (var j = 0; j < sources.Length; j++)
                {
                    var bottom = sources[j].position;
                    var top = bottom + Vector3.up * settings.StringHeight * settings.PuppetSize;
                    v.Strings[j].SetPosition(0, bottom);
                    v.Strings[j].SetPosition(1, (bottom + top) / 2f);
                    v.Strings[j].SetPosition(2, top);
                }
            }
        }
    }
}
