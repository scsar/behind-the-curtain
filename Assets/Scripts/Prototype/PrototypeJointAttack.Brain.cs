using System.Collections.Generic;
using BehindTheCurtain.Core;
using UnityEngine;

namespace BehindTheCurtain.Prototype
{
    public sealed partial class PrototypeJointAttack
    {
        private static readonly AgentId PlayerAgentId = new AgentId(1);
        private PuppetBrain brain;
        private Vector3[] previousPositions;
        private Quaternion[] previousRotations;
        private long[] attackReadyTicks;
        private LineRenderer[] brainLines;
        private bool debugOverlay;

        private static AgentId PuppetAgentId(int index) => new AgentId(10 + index);
        private static Vec2 Planar(Vector3 value) => new Vec2(value.x, value.z);
        private static TargetRole PuppetRole(int index) => index < 2 ? TargetRole.Guard
            : index == 3 ? TargetRole.Healer : TargetRole.Attacker;

        private bool BrainFailure(AiFailure failure)
        {
            lastReason = $"PuppetBrain rejected: {failure}";
            Debug.LogError(lastReason, this);
            enabled = false;
            return false;
        }

        private bool InitializeBrain()
        {
            ApplyFormation();
            var world = expedition.EncounterWorld;
            var added = world.Add(new AgentState(PlayerAgentId, Side.Players, true, TargetRole.Attacker,
                Planar(player.position), Planar(player.forward), settings.PlayerRadius));
            if (!added.Success) return BrainFailure(added.Failure);
            previousPositions = (Vector3[])restPositions.Clone();
            previousRotations = (Quaternion[])restRotations.Clone();
            attackReadyTicks = new long[puppets.Length];
            for (var i = 0; i < puppets.Length; i++)
            {
                added = world.Add(new AgentState(PuppetAgentId(i), Side.Puppets, false, PuppetRole(i),
                    Planar(restPositions[i]), Planar(restRotations[i] * Vector3.forward), settings.PuppetSize / 2f));
                if (!added.Success) return BrainFailure(added.Failure);
                attackReadyTicks[i] = clock.Tick;
                puppets[i].position = restPositions[i];
                puppets[i].rotation = restRotations[i];
            }
            var tuning = new PuppetSettings(settings.PuppetInterceptDistance, settings.PuppetSpreadDistance,
                settings.PuppetAttackRange, settings.PuppetMoveSpeed,
                settings.FloorSize / 2f - settings.PuppetSize / 2f, PrototypeSettings.TicksPerSecond,
                settings.PuppetDistanceScale, settings.PuppetApproachWeight, settings.PuppetInterceptWeight,
                settings.PuppetSpreadWeight, settings.PuppetHoldWeight, settings.PuppetAttackWeight,
                settings.PuppetInterceptPositionFloor, settings.PuppetPersistenceBonus);
            var created = PuppetBrain.Create(world, tuning, new DeterministicRandom(unchecked((ulong)settings.Seed)));
            if (!created.Success) return BrainFailure(created.Failure);
            brain = created.Value;
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
            for (var i = 0; i < puppets.Length; i++)
            {
                var agent = expedition.EncounterWorld.Get(PuppetAgentId(i)).Value;
                var decision = brain.GetDecision(agent.Id);
                var text = $"{agent.Id.Value}: {agent.CurrentAction ?? "pending"} | target {agent.Target?.Value.ToString() ?? "-"}";
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
                var screen = Camera.main.WorldToScreenPoint(puppets[i].position + Vector3.up * settings.BrainLabelHeight);
                if (screen.z > 0f)
                    GUI.Box(new Rect(screen.x - settings.BrainLabelWidth / 2f, Screen.height - screen.y,
                        settings.BrainLabelWidth, settings.BrainLabelPanelHeight), text);
            }
        }
    }
}
