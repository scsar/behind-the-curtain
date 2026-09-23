using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public sealed partial class World
    {
        private readonly Dictionary<AgentId, PuppetState> puppetStates = new Dictionary<AgentId, PuppetState>();
        private readonly Dictionary<AgentId, long> recoveryTicks = new Dictionary<AgentId, long>();
        private readonly Dictionary<AgentId, PuppetStateInput> groggyCauses = new Dictionary<AgentId, PuppetStateInput>();
        private readonly List<PuppetStateEvent> stateEvents = new List<PuppetStateEvent>();
        private long inputSequence;
        private long? stateTick;
        private int? stateTicksPerSecond;
        internal long ParticipationVersion { get; private set; }

        public World() : this(new PuppetStateSettings()) { }
        public World(PuppetStateSettings stateSettings)
        {
            StateSettings = stateSettings;
            puppets.ReservationAllowed = ReservationAllowed;
            players.ReservationAllowed = ReservationAllowed;
        }
        public PuppetStateSettings StateSettings { get; }
        internal void SetStateTickRate(int ticksPerSecond) { stateTicksPerSecond = ticksPerSecond; }
        public IReadOnlyList<PuppetStateEvent> StateEvents => stateEvents.AsReadOnly();
        public AiResult<PuppetState> GetPuppetState(AgentId id) => puppetStates.TryGetValue(id, out var state)
            ? AiResult<PuppetState>.Ok(state) : AiResult<PuppetState>.Fail(id.IsValid ? AiFailure.AgentNotFound : AiFailure.InvalidId);
        public IReadOnlyList<PuppetState> GetPuppetStates()
        {
            var result = new List<PuppetState>(puppetStates.Values);
            result.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            return result.AsReadOnly();
        }
        public bool IsParticipating(AgentId id) => agents.ContainsKey(id)
            && (!puppetStates.TryGetValue(id, out var state) || state.IsParticipating);
        public bool CanAct(AgentId id) => IsParticipating(id)
            && (!puppetStates.TryGetValue(id, out var state) || state.Status == PuppetStatus.Active);
        public IReadOnlyList<AgentState> GetParticipatingAgents()
        {
            var result = new List<AgentState>();
            foreach (var agent in GetAgents()) if (IsParticipating(agent.Id)) result.Add(agent);
            return result.AsReadOnly();
        }

        private bool ReservationAllowed(string key, AgentId owner)
        {
            if (puppetStates.TryGetValue(owner, out var state) && state.Status != PuppetStatus.Active) return false;
            foreach (var target in puppetStates.Values)
                if (!target.IsParticipating && key == "intercept:" + target.Id.Value) return false;
            return true;
        }

        private void AddPuppetState(AgentId id, PuppetInitialState initial)
        {
            var values = new Dictionary<PuppetValueId, double> {
                [PuppetValueId.Health] = StateSettings.DefaultHealth,
                [PuppetValueId.Posture] = StateSettings.MaxPosture,
                [PuppetValueId.Tension] = StateSettings.DefaultTension };
            foreach (var pair in initial.Values) values[pair.Key] = pair.Value;
            puppetStates[id] = new PuppetState(id, initial.Kind, PuppetStatus.Active, values);
            var cause = Input(id, initial.Tick, PuppetInputKind.Initialize, values[PuppetValueId.Health],
                values[PuppetValueId.Posture], values[PuppetValueId.Tension]);
            // Default registration does not establish a clock: legacy brains may start at any signed tick.
            if (initial.Values.Count > 0) recoveryTicks[id] = initial.Tick;
            ResolveStatus(id, initial.Tick, cause);
        }

        /// <summary>
        /// Create the encounter brain before combat inputs, then submit before Brain.Tick(tick).
        /// Its PuppetSettings.TicksPerSecond supplies the recovery clock. Elapsed recovery is applied
        /// before damage; Brain.Tick at the same tick does not recover again.
        /// Accepted inputs must be chronological; same-tick inputs are ordered. Rejections are atomic.
        /// </summary>
        public AiResult<PuppetState> ApplyDamage(AgentId id, double health, double posture, long tick)
        {
            var failure = ValidateInput(id, tick, health, posture, false);
            if (failure != AiFailure.None) return AiResult<PuppetState>.Fail(failure);
            if (stateTicksPerSecond.HasValue)
            {
                failure = AdvancePuppetStates(tick, stateTicksPerSecond.Value);
                if (failure != AiFailure.None) return AiResult<PuppetState>.Fail(failure);
            }
            stateTick = tick;
            var cause = Input(id, tick, PuppetInputKind.ApplyDamage, health, posture, 0);
            SetValue(id, PuppetValueId.Health, Math.Max(0, puppetStates[id].Health - health), tick, cause);
            SetValue(id, PuppetValueId.Posture, Math.Max(0, puppetStates[id].Posture - posture), tick, cause);
            recoveryTicks[id] = tick;
            ResolveStatus(id, tick, cause);
            return GetPuppetState(id);
        }

        public AiResult<PuppetState> ReduceTension(AgentId id, double amount, long tick)
        {
            var failure = ValidateInput(id, tick, amount, 0, true);
            if (failure != AiFailure.None) return AiResult<PuppetState>.Fail(failure);
            if (stateTicksPerSecond.HasValue)
            {
                failure = AdvancePuppetStates(tick, stateTicksPerSecond.Value);
                if (failure != AiFailure.None) return AiResult<PuppetState>.Fail(failure);
            }
            stateTick = tick;
            var cause = Input(id, tick, PuppetInputKind.ReduceTension, 0, 0, amount);
            SetValue(id, PuppetValueId.Tension, Math.Max(0, puppetStates[id].Tension - amount), tick, cause);
            ResolveStatus(id, tick, cause);
            return GetPuppetState(id);
        }

        private AiFailure ValidateInput(AgentId id, long tick, double a, double b, bool tension)
        {
            if (!AiNumber.Weight(a) || !AiNumber.Weight(b)) return AiFailure.InvalidValue;
            var found = GetPuppetState(id);
            if (!found.Success) return found.Failure;
            if (!found.Value.IsParticipating || (tension && found.Value.Status == PuppetStatus.Groggy)) return AiFailure.InvalidState;
            if ((stateTick.HasValue && tick < stateTick.Value)
                || (recoveryTicks.TryGetValue(id, out var recovery) && tick < recovery)) return AiFailure.InvalidTick;
            return AiFailure.None;
        }

        private PuppetStateInput Input(AgentId id, long tick, PuppetInputKind kind, double health, double posture, double tension)
        {
            var cause = new PuppetStateInput(inputSequence++, tick, id, kind, health, posture, tension);
            var state = puppetStates[id];
            stateEvents.Add(new PuppetStateEvent(tick, PuppetStateEventKind.Input, state, state, cause));
            return cause;
        }

        private void SetValue(AgentId id, PuppetValueId key, double value, long tick, PuppetStateInput cause)
        {
            var before = puppetStates[id];
            if (before[key] == value) return;
            var values = new Dictionary<PuppetValueId, double>();
            foreach (var pair in before.Values) values[pair.Key] = pair.Value;
            values[key] = value;
            var after = new PuppetState(id, before.Kind, before.Status, values);
            puppetStates[id] = after;
            stateEvents.Add(new PuppetStateEvent(tick, PuppetStateEventKind.ValueChanged, before, after, cause, key));
        }

        private void SetStatus(AgentId id, PuppetStatus status, long tick, PuppetStateInput cause)
        {
            var before = puppetStates[id];
            if (before.Status == status) return;
            var values = new Dictionary<PuppetValueId, double>();
            foreach (var pair in before.Values) values[pair.Key] = pair.Value;
            var after = new PuppetState(id, before.Kind, status, values);
            puppetStates[id] = after;
            stateEvents.Add(new PuppetStateEvent(tick, PuppetStateEventKind.StatusChanged, before, after, cause));
            ParticipationVersion++;
            if (status == PuppetStatus.Groggy) groggyCauses[id] = cause;
            else groggyCauses.Remove(id);
            if (status != PuppetStatus.Active)
            {
                UpdateAction(id, null);
                puppets.RemoveAgentReservations(id, !after.IsParticipating);
                players.RemoveAgentReservations(id, !after.IsParticipating);
                if (!after.IsParticipating)
                    foreach (var agent in GetAgents())
                        if (agent.Target.HasValue && agent.Target.Value.Equals(id)) UpdateAction(agent.Id, null);
            }
        }

        private void ResolveStatus(AgentId id, long tick, PuppetStateInput cause)
        {
            var state = puppetStates[id];
            if (state.Health <= 0)
                SetStatus(id, state.Kind == PuppetKind.Puppet ? PuppetStatus.Broken : PuppetStatus.Downed, tick, cause);
            else if (state.Tension <= 0 && state.Status == PuppetStatus.Active)
                SetStatus(id, state.Kind == PuppetKind.Puppet ? PuppetStatus.Released : PuppetStatus.Groggy, tick, cause);
        }

        internal AiFailure AdvancePuppetStates(long tick, int ticksPerSecond)
        {
            if (stateTick.HasValue && tick < stateTick.Value) return AiFailure.InvalidTick;
            foreach (var state in GetPuppetStates())
                if (recoveryTicks.TryGetValue(state.Id, out var previous) && tick < previous) return AiFailure.InvalidTick;
            stateTick = tick;
            foreach (var state in GetPuppetStates())
            {
                if (!state.IsParticipating) continue;
                var id = state.Id;
                if (recoveryTicks.TryGetValue(id, out var previous) && tick > previous && state.Posture < StateSettings.MaxPosture)
                {
                    double recovered = StateSettings.PostureRecoverPerSecond / ticksPerSecond * unchecked((ulong)tick - (ulong)previous);
                    double next = Math.Min(StateSettings.MaxPosture, state.Posture + recovered);
                    if (next != state.Posture)
                    {
                        var cause = Input(id, tick, PuppetInputKind.RecoverPosture, 0, next - state.Posture, 0);
                        SetValue(id, PuppetValueId.Posture, next, tick, cause);
                    }
                }
                recoveryTicks[id] = tick;
                if (groggyCauses.TryGetValue(id, out var groggy) && tick >= groggy.Tick
                    && unchecked((ulong)tick - (ulong)groggy.Tick) >= (ulong)StateSettings.GroggyTicks)
                {
                    SetValue(id, PuppetValueId.Tension, StateSettings.ReconnectTension, tick, groggy);
                    SetStatus(id, PuppetStatus.Active, tick, groggy);
                }
            }
            return AiFailure.None;
        }

        internal void TuneTension(AgentId id, double amount, long tick)
        {
            var cause = Input(id, tick, PuppetInputKind.TuneTension, 0, 0, amount);
            SetValue(id, PuppetValueId.Tension, Math.Min(1, puppetStates[id].Tension + amount), tick, cause);
        }
        internal void RecordReclaim(PuppetStateEvent transition, double amount, double budget)
        {
            stateEvents.Add(new PuppetStateEvent(transition.Tick, PuppetStateEventKind.StringReclaimed,
                transition.After, transition.After, transition.Cause) { ReclaimedAmount = amount, BudgetAfter = budget });
        }
        internal double MovementScale(AgentId id) => puppetStates.TryGetValue(id, out var state)
            && state.Posture < StateSettings.PostureStaggerThreshold ? StateSettings.PostureSlowScale : 1;
    }
}
