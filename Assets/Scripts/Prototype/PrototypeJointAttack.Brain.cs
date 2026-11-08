using System.Collections.Generic;
using BehindTheCurtain.Core;
using UnityEngine;

namespace BehindTheCurtain.Prototype
{
    public sealed partial class PrototypeJointAttack
    {
        private static readonly AgentId PlayerAgentId = new AgentId(1);
        private PuppetBrain brain;
        private PuppetSettings brainTuning;
        private string castingSummary;
        private Vector3[] previousPositions;
        private Quaternion[] previousRotations;
        private long[] attackReadyTicks;
        private LineRenderer[] brainLines;
        private bool debugOverlay;

        private static AgentId PuppetAgentId(int index) => new AgentId(10 + index);
        private static Vec2 Planar(Vector3 value) => new Vec2(value.x, value.z);
        private static TargetRole LegacyPuppetRole(int index) => index < 2 ? TargetRole.Guard
            : index == 3 ? TargetRole.Healer : TargetRole.Attacker;

        private bool BrainFailure(AiFailure failure, string context = "PuppetBrain")
        {
            lastReason = $"{context} rejected: {failure}";
            Debug.LogError(lastReason, this);
            enabled = false;
            return false;
        }

        private bool InitializeBrain()
        {
            if (settings.Lineups == null || settings.Lineups.Length == 0)
                return BrainFailure(AiFailure.InvalidSettings, "Lineups");
            var lineup = settings.Lineups[(encounterNumber - 1) % settings.Lineups.Length];
            if (lineup == null || lineup.Puppets == null || lineup.Puppets.Length != puppets.Length)
                return BrainFailure(AiFailure.InvalidSettings, "Lineup (requires four slots)");
            ApplyFormation(false);
            var world = expedition.EncounterWorld;
            var added = world.Add(new AgentState(PlayerAgentId, Side.Players, true, TargetRole.Attacker,
                Planar(player.position), Planar(player.forward), settings.PlayerRadius));
            if (!added.Success) return BrainFailure(added.Failure);
            attackReadyTicks = new long[puppets.Length];
            for (var i = 0; i < puppets.Length; i++)
            {
                var selectedLoadout = Loadout.Create(lineup.Puppets[i]?.EquipmentIds);
                if (!selectedLoadout.Success)
                    return BrainFailure(selectedLoadout.Failure, $"Lineup {lineup.Name}, Puppet {PuppetAgentId(i).Value} Loadout");
                added = world.AddWithLoadout(new AgentState(PuppetAgentId(i), Side.Puppets, false,
                    settings.UseDirectorCasting ? TargetRole.Attacker : LegacyPuppetRole(i),
                    Planar(restPositions[i]), Planar(restRotations[i] * Vector3.forward), settings.PuppetSize / 2f), selectedLoadout.Value);
                if (!added.Success) return BrainFailure(added.Failure);
                attackReadyTicks[i] = clock.Tick;
                var loadout = world.GetLoadout(PuppetAgentId(i));
                if (!loadout.Success) return BrainFailure(loadout.Failure);
                var equipmentView = puppets[i].GetComponent<PrototypeEquipmentView>();
                if (equipmentView == null) equipmentView = puppets[i].gameObject.AddComponent<PrototypeEquipmentView>();
                equipmentView.SetEquipment(loadout.Value.EquipmentIds);
                marionettes[i].AttackStart = -1;
                MountMarionetteEquipment(i);
                puppets[i].position = restPositions[i];
                puppets[i].rotation = restRotations[i];
            }
            if (settings.UseDirectorCasting)
            {
                var casting = DirectorCasting.Assign(world, new DirectorCastingSettings(settings.DirectorGuardCount));
                if (!casting.Success) return BrainFailure(casting.Failure, "DirectorCasting");
            }
            ApplyFormation(true);
            castingSummary = $"교전 {encounterNumber} 라인업 {lineup.Name} | " +
                (settings.UseDirectorCasting ? "연출가 배분" : "자리 배분");
            for (var i = 0; i < puppets.Length; i++)
            {
                var positioned = world.UpdatePosition(PuppetAgentId(i), Planar(restPositions[i]),
                    Planar(restRotations[i] * Vector3.forward));
                if (!positioned.Success) return BrainFailure(positioned.Failure);
                puppets[i].SetPositionAndRotation(restPositions[i], restRotations[i]);
                SetMaterialColor(puppetMaterials[i], PuppetColor(i));
                castingSummary += $" | {PuppetAgentId(i).Value}:{positioned.Value.Role}";
            }
            previousPositions = (Vector3[])restPositions.Clone();
            previousRotations = (Quaternion[])restRotations.Clone();
            if (!ApplyPuppetTuning(true)) return false;
            if (brainLines == null)
            {
                brainLines = new LineRenderer[puppets.Length];
                for (var i = 0; i < puppets.Length; i++)
                    brainLines[i] = CreateCounterLine($"Puppet {10 + i} Goal", settings.BrainGoalColor, 2, false);
            }
            Physics.SyncTransforms();
            return true;
        }

        private bool AdvanceBrain(long tick)
        {
            var world = expedition.EncounterWorld;
            var updated = world.UpdatePosition(PlayerAgentId, Planar(player.position), Planar(player.forward));
            if (!updated.Success) return BrainFailure(updated.Failure);
            // Temporary: keep T15 running through joint telegraphs, counter windows and results.
            // Joint-attack-specific behavior remains an open question in docs/ai-design.md.
            var failure = brain.Tick(tick);
            if (failure != AiFailure.None) return BrainFailure(failure);
            for (var i = 0; i < puppets.Length; i++)
            {
                var agent = world.Get(PuppetAgentId(i)).Value;
                previousPositions[i] = restPositions[i];
                previousRotations[i] = restRotations[i];
                restPositions[i] = new Vector3((float)agent.Position.X, restPositions[i].y, (float)agent.Position.Z);
                restRotations[i] = Quaternion.LookRotation(new Vector3((float)agent.Direction.X, 0f, (float)agent.Direction.Z));
                if (agent.CurrentAction == ActionIds.Attack
                    && tick >= attackReadyTicks[i] && agent.Target.HasValue)
                {
                    var target = world.Get(agent.Target.Value);
                    if (target.Success)
                    {
                        CreateSlam(tick, target.Value.Position);
                        marionettes[i].AttackStart = tick;
                        marionettes[i].ImpactTick = tick + settings.SlamWarningTicks;
                        attackReadyTicks[i] = tick + settings.AttackCooldownTicks;
                    }
                }
                // Catch-up tick raycasts must use the current World pose.
                puppets[i].position = restPositions[i];
                puppets[i].rotation = restRotations[i];
            }
            Physics.SyncTransforms();
            return true;
        }

        private void UpdateBrainLines()
        {
            if (brainLines == null) return;
            for (var i = 0; i < brainLines.Length; i++)
            {
                var decision = brain.GetDecision(PuppetAgentId(i));
                var line = brainLines[i];
                line.enabled = debugOverlay && decision.Success;
                if (!line.enabled) continue;
                var goal = decision.Value.Goal;
                line.SetPosition(0, puppets[i].position);
                line.SetPosition(1, new Vector3((float)goal.X, settings.SlamHeight, (float)goal.Z));
            }
        }

        private void DrawBrainOverlay()
        {
            if (!debugOverlay || brain == null || Camera.main == null) return;
            GUI.Box(new Rect(8f, 0f, Screen.width - 16f, 26f),
                $"판단 {brainTuning.ReevaluationIntervalTicks} ticks | 이동 {brainTuning.MoveSpeed:0.####} | 견제 {brainTuning.HarassIntervalTicks} / 회복 {brainTuning.HarassRecoverTicks} ticks | 측면 반경 {brainTuning.FlankRadius:0.####} | 기본값과 다른 항목 {PrototypePuppetTuning.Differences(appliedPuppetTuning).Length}");
            for (var i = 0; i < puppets.Length; i++)
            {
                var agent = expedition.EncounterWorld.Get(PuppetAgentId(i)).Value;
                var decision = brain.GetDecision(agent.Id);
                var text = $"{agent.Id.Value}: {agent.CurrentAction ?? "pending"} | target {agent.Target?.Value.ToString() ?? "-"}";
                if (decision.Success && decision.Value.HarassPhase.HasValue)
                    text = text.Replace(agent.CurrentAction ?? "pending", (agent.CurrentAction ?? "pending") + "/" + decision.Value.HarassPhase.Value);
                var contract = RoleContract.ForRole(agent.Role);
                var kind = contract.Classify(agent.CurrentAction);
                var classification = kind == ContractActionKind.Primary ? "우선"
                    : kind == ContractActionKind.Alternative ? "대체" : "계약 밖";
                var loadout = expedition.EncounterWorld.GetLoadout(agent.Id).Value;
                var state = expedition.EncounterWorld.GetPuppetState(agent.Id).Value;
                text += $"\n배역 {agent.Role} | 목표 {contract.Goal}\n계약: {classification}";
                text += $"\n능력: {(loadout.Capabilities.Count == 0 ? "-" : string.Join(", ", loadout.Capabilities))} | 장력 {state.Tension:F2}";
                text += $"\n공격 거리 {loadout.GetAttackRange(brainTuning):F2} | 이동 배율 {loadout.MoveSpeedScale:F2}";
                if (decision.Success)
                {
                    var scores = new List<ActionScore>(decision.Value.Selection.Scores);
                    scores.Sort((a, b) => {
                        var order = b.Score.CompareTo(a.Score);
                        return order != 0 ? order : string.CompareOrdinal(a.ActionId, b.ActionId);
                    });
                    for (var rank = 0; rank < System.Math.Min(3, scores.Count); rank++)
                        text += $"\n{scores[rank].ActionId}: {scores[rank].Score:F3}";
                }
                var top = puppets[i].position.y;
                foreach (var renderer in puppets[i].GetComponentsInChildren<Renderer>())
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy && !(renderer is LineRenderer)) top = Mathf.Max(top, renderer.bounds.max.y);
                var anchor = puppets[i].position;
                anchor.y = top + settings.BrainLabelMargin;
                var screen = Camera.main.WorldToScreenPoint(anchor);
                if (screen.z > 0f)
                    GUI.Box(new Rect(screen.x - settings.BrainLabelWidth / 2f,
                        Mathf.Max(0f, Screen.height - screen.y - settings.BrainLabelPanelHeight),
                        settings.BrainLabelWidth, settings.BrainLabelPanelHeight), text);
            }
        }
    }
}
