using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BehindTheCurtain.Core
{
    public enum PuppetKind { Puppet, Humanized }
    public enum PuppetStatus { Active, Groggy, Released, Broken, Downed }
    public enum PuppetValueId { Health, Posture, Tension }
    public enum PuppetInputKind { Initialize, ApplyDamage, ReduceTension, RecoverPosture, TuneTension }
    public enum PuppetStateEventKind { Input, ValueChanged, StatusChanged, StringReclaimed }

    /// <summary>Provisional, arbitrarily chosen T16a-1 balance values.</summary>
    public sealed class PuppetStateSettings
    {
        public PuppetStateSettings(int groggyTicks = 60, double reconnectTension = 0.5,
            double postureStaggerThreshold = 30, double postureSlowScale = 0.5,
            double postureRecoverPerSecond = 10, double defaultHealth = 100,
            double maxPosture = 100, double defaultTension = 1)
        {
            GroggyTicks = groggyTicks; ReconnectTension = reconnectTension;
            PostureStaggerThreshold = postureStaggerThreshold; PostureSlowScale = postureSlowScale;
            PostureRecoverPerSecond = postureRecoverPerSecond; DefaultHealth = defaultHealth;
            MaxPosture = maxPosture; DefaultTension = defaultTension;
        }
        public int GroggyTicks { get; }
        public double ReconnectTension { get; }
        public double PostureStaggerThreshold { get; }
        public double PostureSlowScale { get; }
        public double PostureRecoverPerSecond { get; }
        public double DefaultHealth { get; }
        public double MaxPosture { get; }
        public double DefaultTension { get; }
        internal bool IsValid => GroggyTicks > 0 && AiNumber.Weight(ReconnectTension)
            && ReconnectTension > 0 && ReconnectTension <= 1 && AiNumber.Weight(PostureStaggerThreshold)
            && AiNumber.Weight(PostureSlowScale) && PostureSlowScale <= 1
            && AiNumber.Weight(PostureRecoverPerSecond) && AiNumber.Weight(DefaultHealth)
            && DefaultHealth > 0 && AiNumber.Weight(MaxPosture) && MaxPosture > 0
            && PostureStaggerThreshold <= MaxPosture && AiNumber.Weight(DefaultTension)
            && DefaultTension > 0 && DefaultTension <= 1;
    }

    /// <summary>
    /// Missing numeric entries use settings defaults. Copied on construction.
    /// Tick anchors explicitly supplied values and initial zero-value transitions.
    /// Health takes precedence over tension when both start at zero.
    /// </summary>
    public sealed class PuppetInitialState
    {
        public PuppetInitialState(PuppetKind kind = PuppetKind.Puppet,
            IReadOnlyDictionary<PuppetValueId, double>? values = null, long tick = 0)
        {
            Kind = kind; Tick = tick;
            var copy = new Dictionary<PuppetValueId, double>();
            if (values != null) foreach (var pair in values) copy[pair.Key] = pair.Value;
            Values = new ReadOnlyDictionary<PuppetValueId, double>(copy);
        }
        public PuppetKind Kind { get; }
        public long Tick { get; }
        public IReadOnlyDictionary<PuppetValueId, double> Values { get; }
        internal bool IsValid
        {
            get
            {
                if (Kind != PuppetKind.Puppet && Kind != PuppetKind.Humanized) return false;
                foreach (var pair in Values)
                    if (!AiNumber.Weight(pair.Value) || (pair.Key == PuppetValueId.Tension && pair.Value > 1)) return false;
                return true;
            }
        }
    }

    /// <summary>Immutable state snapshot; numeric axes can be extended without changing selection.</summary>
    public sealed class PuppetState
    {
        internal PuppetState(AgentId id, PuppetKind kind, PuppetStatus status, Dictionary<PuppetValueId, double> values)
        { Id = id; Kind = kind; Status = status; Values = new ReadOnlyDictionary<PuppetValueId, double>(values); }
        public AgentId Id { get; }
        public PuppetKind Kind { get; }
        public PuppetStatus Status { get; }
        public IReadOnlyDictionary<PuppetValueId, double> Values { get; }
        public double this[PuppetValueId id] => Values.TryGetValue(id, out var value) ? value : 0;
        public double Health => this[PuppetValueId.Health];
        public double Posture => this[PuppetValueId.Posture];
        public double Tension => this[PuppetValueId.Tension];
        public bool IsParticipating => Status == PuppetStatus.Active || Status == PuppetStatus.Groggy;
    }

    /// <summary>Accepted input, shared by its ordered events, including delayed reconnection.</summary>
    public sealed class PuppetStateInput
    {
        internal PuppetStateInput(long sequence, long tick, AgentId id, PuppetInputKind kind, double health, double posture, double tension)
        { Sequence = sequence; Tick = tick; AgentId = id; Kind = kind; Health = health; Posture = posture; Tension = tension; }
        public long Sequence { get; }
        public long Tick { get; }
        public AgentId AgentId { get; }
        public PuppetInputKind Kind { get; }
        public double Health { get; }
        public double Posture { get; }
        public double Tension { get; }
    }

    public sealed class PuppetStateEvent
    {
        internal PuppetStateEvent(long tick, PuppetStateEventKind kind, PuppetState before, PuppetState after,
            PuppetStateInput cause, PuppetValueId? valueId = null)
        { Tick = tick; Kind = kind; Before = before; After = after; Cause = cause; ValueId = valueId; }
        public long Tick { get; }
        public AgentId AgentId => After.Id;
        public PuppetStateEventKind Kind { get; }
        public PuppetValueId? ValueId { get; }
        public PuppetState Before { get; }
        public PuppetState After { get; }
        public PuppetStateInput Cause { get; }
        public double ReclaimedAmount { get; internal set; }
        public double BudgetAfter { get; internal set; }
    }
}
