using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public enum JointAttackState
    {
        Telegraphing,
        CounterWindowOpen,
        Executed,
        Broken,
        Weakened
    }

    /// <summary>A deterministic attack advanced explicitly by its owner.</summary>
    public sealed class JointAttack
    {
        private readonly HashSet<ConnectionPoint> brokenConnections = new HashSet<ConnectionPoint>();

        private JointAttack(long telegraphStartTick, TickWindow counterWindow, long executionTick,
            ConnectionPoint[] connectionPoints)
        {
            TelegraphStartTick = telegraphStartTick;
            CounterWindow = counterWindow;
            ExecutionTick = executionTick;
            CurrentTick = telegraphStartTick;
            State = JointAttackState.Telegraphing;
            ConnectionPoints = Array.AsReadOnly(connectionPoints);
        }

        public long TelegraphStartTick { get; }
        public TickWindow CounterWindow { get; }
        public long ExecutionTick { get; }
        public long CurrentTick { get; private set; }
        public JointAttackState State { get; private set; }
        public IReadOnlyList<ConnectionPoint> ConnectionPoints { get; }
        /// <summary>Remaining connection fraction, including before execution. Broken attacks have zero severity.</summary>
        public double Severity => (double)(ConnectionPoints.Count - brokenConnections.Count) / ConnectionPoints.Count;

        /// <summary>
        /// Requires telegraphStartTick &lt;= counterWindow.Start and counterWindow.End &lt; executionTick.
        /// Negative ticks are allowed. Creation starts in Telegraphing; call Advance to open the window.
        /// All exposed connections are required and must be distinct instances with valid allowed methods.
        /// Requires at least one connection; copies the collection.
        /// </summary>
        public static bool TryCreate(long telegraphStartTick, TickWindow counterWindow,
            long executionTick, IReadOnlyList<ConnectionPoint>? connectionPoints, out JointAttack? attack)
        {
            attack = null;
            if (telegraphStartTick > counterWindow.Start || counterWindow.End >= executionTick)
                return false;

            if (connectionPoints == null || connectionPoints.Count == 0)
                return false;

            var snapshot = new ConnectionPoint[connectionPoints.Count];
            var seen = new HashSet<ConnectionPoint>();
            for (var i = 0; i < snapshot.Length; i++)
            {
                var point = connectionPoints[i];
                if (point == null || !point.IsValid || !seen.Add(point))
                    return false;
                snapshot[i] = point;
            }

            attack = new JointAttack(telegraphStartTick, counterWindow, executionTick, snapshot);
            return true;
        }

        /// <summary>
        /// Returns false without mutation for backwards time or a terminal state.
        /// Equal ticks and forward jumps are accepted. After the window closes, the attack
        /// remains CounterWindowOpen until execution, but Break is no longer allowed.
        /// </summary>
        public bool Advance(long tick)
        {
            if (IsTerminal || tick < CurrentTick)
                return false;

            CurrentTick = tick;
            if (State == JointAttackState.Telegraphing && tick >= CounterWindow.Start)
                State = JointAttackState.CounterWindowOpen;
            if (State == JointAttackState.CounterWindowOpen && tick >= ExecutionTick)
                State = brokenConnections.Count == 0 ? JointAttackState.Executed : JointAttackState.Weakened;

            return true;
        }

        /// <summary>Legacy single-connection operation. Multi-connection attacks require an explicit target.</summary>
        public bool Break()
            => ConnectionPoints.Count == 1 && Break(ConnectionPoints[0]);

        internal bool IsConnectionBroken(ConnectionPoint? target)
            => target != null && brokenConnections.Contains(target);

        /// <summary>Severs one exposed connection at the last accepted Advance tick.</summary>
        public bool Break(ConnectionPoint? target)
        {
            if (State != JointAttackState.CounterWindowOpen || !CounterWindow.Contains(CurrentTick))
                return false;

            if (target == null || !((IList<ConnectionPoint>)ConnectionPoints).Contains(target)
                || !brokenConnections.Add(target))
                return false;

            if (brokenConnections.Count == ConnectionPoints.Count)
                State = JointAttackState.Broken;
            return true;
        }

        private bool IsTerminal => State == JointAttackState.Executed || State == JointAttackState.Broken
            || State == JointAttackState.Weakened;
    }
}
