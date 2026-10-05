using System.Collections.Generic;
using BehindTheCurtain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BehindTheCurtain.Prototype
{
    public sealed partial class PrototypeJointAttack
    {
        private Expedition expedition;
        private CombatObserver observer;
        private Tactic tactic;
        private readonly TacticSettings tacticSettings = new TacticSettings();
        private int encounterNumber = 1;
        private int completedCycles;
        private bool intermission;
        private long intermissionUntil;
        private Vector3 basePartyCenter;
        private Vector3[] baseOffsets;
        private Material[] puppetMaterials;
        private long[] puppetFlashUntil;
        private double feedbackTickFraction;
        private long feedbackTick;
        private readonly List<AttackLabel> attackLabels = new List<AttackLabel>();

        private sealed class AttackLabel
        {
            public Transform Target;
            public string Text;
            public long Start;
        }

        private bool InitializeExpedition()
        {
            if (settings.EncounterCycles < 1 || settings.IntermissionTicks < 1 ||
                settings.PuppetHitFlashTicks < 1 || settings.AttackLabelTicks < 1 ||
                !(settings.MeleeAttackRadius > 0f) || float.IsInfinity(settings.MeleeAttackRadius) ||
                !(settings.RangedAttackConeDegrees >= 0f && settings.RangedAttackConeDegrees <= 180f) ||
                !(settings.SpreadSpacingMultiplier > 0f) || float.IsInfinity(settings.SpreadSpacingMultiplier) ||
                !(settings.ShieldAdvanceDistance >= 0f) || float.IsInfinity(settings.ShieldAdvanceDistance) ||
                !(settings.GuardHealerOffset > 0f) || float.IsInfinity(settings.GuardHealerOffset)) return false;
            return RuleTacticSelector.TryCreate(tacticSettings, out var selector) &&
                Expedition.TryCreate(new ExpeditionSettings(settings.ExpeditionDecay), selector, out expedition) &&
                PrepareEncounter();
        }

        private bool PrepareEncounter()
        {
            return expedition.TryStartEncounter(out tactic) &&
                CombatObserver.TryCreate(new ObserverSettings(settings.MinimumAreaHitCount,
                    settings.SustainedRangedStreak), out observer) &&
                tactic.TryCreatePlan(out var layout) &&
                JointAttackPlan.TryCreate(tactic, settings.TelegraphTicks, settings.CounterWindowTicks,
                    layout.ConnectionPoints, new FairnessRules(maxRequiredPointsPerAttack: settings.MaxRequiredPoints),
                    out plan);
        }

        private bool FinishEncounter(long tick)
        {
            CancelAction(null);
            if (!expedition.EndEncounterWithIndividuals(observer.EndEncounterWithIndividuals()) || !PrepareEncounter())
            {
                lastReason = "Encounter transition rejected";
                enabled = false;
                return false;
            }
            encounterNumber++;
            if (!InitializeBrain()) return false;
            intermission = true;
            intermissionUntil = tick + settings.IntermissionTicks;
            nextAttackTick = intermissionUntil;
            return true;
        }

        private void InitializePlayerAttacks()
        {
            basePartyCenter = partyCenter;
            baseOffsets = new Vector3[puppets.Length];
            puppetMaterials = new Material[puppets.Length];
            puppetFlashUntil = new long[puppets.Length];
            for (var i = 0; i < puppets.Length; i++)
            {
                baseOffsets[i] = restPositions[i] - partyCenter;
                puppetMaterials[i] = puppets[i].GetComponent<Renderer>().sharedMaterial;
                SetMaterialColor(puppetMaterials[i], settings.PuppetColor);
            }
        }

        private Color PuppetColor(int index) => expedition.EncounterWorld.Get(PuppetAgentId(index)).Value.Role
            == TargetRole.Healer ? settings.HealerColor : settings.PuppetColor;

        private void ApplyFormation(bool useAssignedRoles)
        {
            // Always rebuild from the original offsets, never from rotated/advanced positions.
            partyCenter = basePartyCenter;
            var towardPlayer = Vector3.ProjectOnPlane(player.position - partyCenter, Vector3.up).normalized;
            if (towardPlayer.sqrMagnitude == 0f) towardPlayer = Vector3.back;
            var facing = Quaternion.LookRotation(towardPlayer);
            var formationRotation = facing * Quaternion.Inverse(Quaternion.LookRotation(Vector3.back));
            if (tactic.Id == TacticId.ShieldAdvance)
                partyCenter += towardPlayer * settings.ShieldAdvanceDistance;
            var healerIndex = -1;
            var rearGuardIndex = -1;
            var guardCount = 0;
            if (useAssignedRoles && tactic.Id == TacticId.GuardHealer)
            {
                for (var i = 0; i < puppets.Length; i++)
                {
                    var agent = expedition.EncounterWorld.Get(PuppetAgentId(i)).Value;
                    if (agent.Role == TargetRole.Healer && healerIndex < 0) healerIndex = i;
                    if (agent.Role != TargetRole.Guard) continue;
                    guardCount++;
                    if (rearGuardIndex < 0 || agent.Id.Value > PuppetAgentId(rearGuardIndex).Value)
                        rearGuardIndex = i;
                }
            }
            for (var i = 0; i < puppets.Length; i++)
            {
                var offset = baseOffsets[i];
                if (tactic.Id == TacticId.SpreadFormation) offset *= settings.SpreadSpacingMultiplier;
                if (healerIndex >= 0 && guardCount >= 2 && i == rearGuardIndex)
                    offset = baseOffsets[healerIndex] + Vector3.back * settings.GuardHealerOffset;
                restPositions[i] = partyCenter + formationRotation * offset;
                restRotations[i] = facing;
            }
        }

        private void UpdatePlayerAttacks()
        {
            if (!enabled || observer == null || puppetMaterials == null) return;
            feedbackTickFraction += (double)Time.deltaTime * PrototypeSettings.FeedbackTicksPerSecond;
            var elapsedTicks = (long)feedbackTickFraction;
            feedbackTick += elapsedTicks;
            feedbackTickFraction -= elapsedTicks;
            if (!intermission && Application.isFocused)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) PerformPlayerAttack(PlayerAttackKind.AreaMelee, "광역");
                if (keyboard != null && keyboard.fKey.wasPressedThisFrame) PerformPlayerAttack(PlayerAttackKind.Ranged, "원거리");
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) PerformPlayerAttack(PlayerAttackKind.Melee, "근접");
            }
            for (var i = 0; i < puppets.Length; i++)
                SetMaterialColor(puppetMaterials[i], feedbackTick < puppetFlashUntil[i] ? Color.white :
                    PuppetColor(i));
            attackLabels.RemoveAll(label => feedbackTick - label.Start >= settings.AttackLabelTicks);
        }

        private void PerformPlayerAttack(PlayerAttackKind kind, string label)
        {
            var targets = new AttackTarget[puppets.Length];
            var aim = controls != null ? controls.AimDirection : player.forward;
            for (var i = 0; i < targets.Length; i++)
            {
                var offset = Vector3.ProjectOnPlane(puppets[i].position - player.position, Vector3.up);
                targets[i] = new AttackTarget(offset.magnitude, Vector3.Angle(aim, offset),
                    expedition.EncounterWorld.Get(PuppetAgentId(i)).Value.Role);
            }
            // Core's legacy resolver records anonymously. Use a scratch observer, then
            // attach identity once to the encounter observer without duplicating hit rules.
            var observerSettings = new ObserverSettings(settings.MinimumAreaHitCount, settings.SustainedRangedStreak);
            CombatObserver.TryCreate(observerSettings, out var scratch);
            if (!PlayerAttack.TryResolve(kind, targets, settings.MeleeAttackRadius, settings.RangedAttackConeDegrees,
                clock.Tick, scratch, out var hits))
            {
                attackLabels.Add(new AttackLabel { Target = player, Text = "빗나감", Start = feedbackTick });
                return;
            }
            var hitTargets = new AttackTarget[hits.Count];
            for (var h = 0; h < hits.Count; h++) hitTargets[h] = targets[hits[h]];
            // Ask Core for the nearest primary among the already resolved hits.
            CombatObserver.TryCreate(observerSettings, out var primaryScratch);
            if (!PlayerAttack.TryResolve(PlayerAttackKind.Melee, hitTargets, double.MaxValue, 180,
                clock.Tick, primaryScratch, out var primary)) return;
            if (!observer.Observe(new AttackEvent(kind == PlayerAttackKind.AreaMelee ? hits.Count : 0,
                hitTargets[primary[0]].Role, kind == PlayerAttackKind.Ranged ? AttackRange.Ranged : AttackRange.Melee,
                clock.Tick, PlayerAgentId, true))) return;
            foreach (var i in hits)
            {
                puppetFlashUntil[i] = feedbackTick + settings.PuppetHitFlashTicks;
                attackLabels.Add(new AttackLabel { Target = puppets[i], Text = label, Start = feedbackTick });
            }
        }

        private string ConnectionSummary()
        {
            if (plan == null) return "";
            var text = "";
            for (var i = 0; i < plan.ConnectionPoints.Count; i++)
            {
                var point = plan.ConnectionPoints[i];
                text += $"Puppet {i + 1}: {point.Kind} {point.Position} " +
                    (severedViews.Contains(point) ? "끊김" : WindowOpen ? "노출" : "닫힘") + "\n";
            }
            return text;
        }

        private void DrawExpedition()
        {
            if (observer == null) return;
            var profile = observer.CurrentProfile;
            GUI.Box(new Rect(8f, 8f, 540f, 166f), "");
            GUI.Label(new Rect(12f, 12f, 530f, 24f),
                $"교전 {encounterNumber} | 사이클 {completedCycles}/{settings.EncounterCycles} | F1: 개발 오버레이");
            if (debugOverlay)
            {
                GUI.Label(new Rect(12f, 150f, 530f, 24f), $"대응: {tactic.Id}");
                DrawTendency(42f, "광역 의존", profile.AreaDependency, tacticSettings.AreaDependencyThreshold);
                DrawTendency(70f, "힐러 집중", profile.HealerFocus, tacticSettings.HealerFocusThreshold);
                DrawTendency(98f, "원거리 지속", profile.SustainedRangedRatio, tacticSettings.SustainedRangedThreshold);
            }
            GUI.Label(new Rect(12f, 130f, 530f, 36f), "Space: 광역 근접 | F: 원거리 단일 | 좌클릭: 근접 단일");
            if (debugOverlay && intermission)
            {
                var memory = expedition.Memory.Value;
                GUI.Box(new Rect(Screen.width / 2f - settings.CastingSummaryWidth / 2f,
                    Screen.height / 2f - settings.CastingSummaryHeight / 2f,
                    settings.CastingSummaryWidth, settings.CastingSummaryHeight),
                    $"연출가가 각본을 고친다: {tactic.Id}\n{castingSummary}\n다음 교전 원정 기억\n" +
                    $"광역 의존 {memory.AreaDependency:P0} | 힐러 집중 {memory.HealerFocus:P0} | 원거리 지속 {memory.SustainedRangedRatio:P0}");
            }
            if (Camera.main == null) return;
            foreach (var label in attackLabels)
            {
                var progress = (float)(feedbackTick - label.Start) / settings.AttackLabelTicks;
                var screen = Camera.main.WorldToScreenPoint(label.Target.position +
                    Vector3.up * (settings.AttackLabelHeight + progress * settings.AttackLabelRise));
                if (screen.z > 0f) GUI.Label(new Rect(screen.x, Screen.height - screen.y, 110f, 24f), label.Text);
            }
        }

        private static void DrawTendency(float y, string name, double value, double threshold)
        {
            GUI.Label(new Rect(12f, y, 150f, 24f), $"{name}: {value:P0}");
            var previous = GUI.color;
            GUI.color = Color.gray;
            GUI.DrawTexture(new Rect(170f, y + 3f, 300f, 18f), Texture2D.whiteTexture);
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(170f, y + 3f, 300f * (float)value, 18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(170f + 300f * (float)threshold, y, 2f, 24f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
