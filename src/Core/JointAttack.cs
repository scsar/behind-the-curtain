using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public enum JointAttackState
    {
        Telegraphing,
        CounterWindowOpen,
        Executed,
        Broken
    }

    /// <summary>A deterministic attack advanced explicitly by its owner.</summary>
    public sealed class JointAttack
    {
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

        /// <summary>
        /// Requires telegraphStartTick &lt;= counterWindow.Start and counterWindow.End &lt; executionTick.
        /// Negative ticks are allowed. Creation starts in Telegraphing; call Advance to open the window.
        /// Requires at least one exposed connection with valid allowed methods; copies the collection.
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
            for (var i = 0; i < snapshot.Length; i++)
            {
                var point = connectionPoints[i];
                if (point == null || !point.IsValid)
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
                State = JointAttackState.Executed;

            return true;
        }

        /// <summary>Attempts to break the attack at the last accepted Advance tick.</summary>
        public bool Break()
        {
            if (State != JointAttackState.CounterWindowOpen || !CounterWindow.Contains(CurrentTick))
                return false;

            State = JointAttackState.Broken;
            return true;
        }

        private bool IsTerminal => State == JointAttackState.Executed || State == JointAttackState.Broken;
    }
}
