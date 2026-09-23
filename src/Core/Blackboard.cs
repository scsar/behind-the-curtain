using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public readonly struct Reservation
    {
        public Reservation(string key, AgentId owner, long expiresAtTick)
        { Key = key; Owner = owner; ExpiresAtTick = expiresAtTick; }
        public string Key { get; }
        public AgentId Owner { get; }
        public long ExpiresAtTick { get; }
    }

    public sealed class Blackboard
    {
        private readonly Dictionary<string, object> facts = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, Reservation> reservations = new Dictionary<string, Reservation>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> intentions = new Dictionary<string, double>(StringComparer.Ordinal);
        private long? lastTick;
        internal Func<string, AgentId, bool>? ReservationAllowed { get; set; }

        /// <summary>Store immutable values/snapshots. Facts are supplied by the caller, not synchronized from World.</summary>
        public AiFailure SetFact<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key)) return AiFailure.InvalidKey;
            if (value == null) return AiFailure.InvalidValue;
            facts[key] = value;
            return AiFailure.None;
        }

        public AiResult<T> GetFact<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return AiResult<T>.Fail(AiFailure.InvalidKey);
            if (!facts.TryGetValue(key, out var value)) return AiResult<T>.Fail(AiFailure.FactNotFound);
            return value is T typed ? AiResult<T>.Ok(typed) : AiResult<T>.Fail(AiFailure.InvalidValue);
        }

        /// <summary>T14 will populate these between encounters. Missing intentions have multiplier 1.</summary>
        public AiFailure SetIntention(string actionId, double multiplier)
        {
            if (string.IsNullOrWhiteSpace(actionId)) return AiFailure.InvalidKey;
            if (!AiNumber.Weight(multiplier)) return AiFailure.InvalidWeight;
            intentions[actionId] = multiplier;
            return AiFailure.None;
        }

        public AiResult<double> GetIntention(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId)) return AiResult<double>.Fail(AiFailure.InvalidKey);
            return AiResult<double>.Ok(intentions.TryGetValue(actionId, out var value) ? value : 1);
        }

        internal bool HasIntention(string key) => intentions.ContainsKey(key);
        internal double RoleIntention(TargetRole role, string action) =>
            GetIntention(HasIntention(role + ":" + action) ? role + ":" + action : action).Value;

        public AiResult<Reservation> Reserve(string key, AgentId owner, long expiresAtTick, long tick)
        {
            if (string.IsNullOrWhiteSpace(key)) return AiResult<Reservation>.Fail(AiFailure.InvalidKey);
            if (!owner.IsValid) return AiResult<Reservation>.Fail(AiFailure.InvalidId);
            if (ReservationAllowed != null && !ReservationAllowed(key, owner))
                return AiResult<Reservation>.Fail(AiFailure.InvalidState);
            if (expiresAtTick < tick || (lastTick.HasValue && tick < lastTick.Value))
                return AiResult<Reservation>.Fail(AiFailure.InvalidTick);
            Advance(tick);
            if (reservations.TryGetValue(key, out var existing) && !existing.Owner.Equals(owner))
                return AiResult<Reservation>.Fail(AiFailure.ReservationConflict);
            var reservation = new Reservation(key, owner, expiresAtTick);
            reservations[key] = reservation;
            return AiResult<Reservation>.Ok(reservation);
        }

        public AiResult<Reservation> GetReservation(string key, long tick)
        {
            if (string.IsNullOrWhiteSpace(key)) return AiResult<Reservation>.Fail(AiFailure.InvalidKey);
            if (lastTick.HasValue && tick < lastTick.Value) return AiResult<Reservation>.Fail(AiFailure.InvalidTick);
            Advance(tick);
            return reservations.TryGetValue(key, out var value) ? AiResult<Reservation>.Ok(value)
                : AiResult<Reservation>.Fail(AiFailure.ReservationNotFound);
        }

        private void Advance(long tick)
        {
            lastTick = tick;
            var expired = new List<string>();
            foreach (var pair in reservations)
                if (pair.Value.ExpiresAtTick < tick) expired.Add(pair.Key);
            foreach (var key in expired) reservations.Remove(key);
        }

        internal void RemoveAgentReservations(AgentId id, bool removeTarget)
        {
            var removed = new List<string>();
            foreach (var pair in reservations)
                if (pair.Value.Owner.Equals(id) || (removeTarget && pair.Key == "intercept:" + id.Value))
                    removed.Add(pair.Key);
            foreach (var key in removed) reservations.Remove(key);
        }
    }
}
