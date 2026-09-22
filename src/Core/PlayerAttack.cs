using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public enum PlayerAttackKind { AreaMelee, Ranged, Melee }

    /// <summary>Spatial measurements supplied by the host, in metres and degrees.</summary>
    public readonly struct AttackTarget
    {
        public AttackTarget(double distance, double aimAngle, TargetRole role)
        { Distance = distance; AimAngle = aimAngle; Role = role; }
        public double Distance { get; }
        public double AimAngle { get; }
        public TargetRole Role { get; }
    }

    public static class PlayerAttack
    {
        /// <summary>Nearest primary target; ties retain input order. A miss is not observed.</summary>
        public static bool TryResolve(PlayerAttackKind kind, IReadOnlyList<AttackTarget> targets,
            double meleeRadius, double rangedConeDegrees, long tick, CombatObserver observer,
            out IReadOnlyList<int> hits)
        {
            hits = Array.Empty<int>();
            if (targets == null || observer == null || !(meleeRadius > 0) || double.IsInfinity(meleeRadius)
                || !(rangedConeDegrees >= 0 && rangedConeDegrees <= 180)
                || (kind != PlayerAttackKind.AreaMelee && kind != PlayerAttackKind.Ranged && kind != PlayerAttackKind.Melee))
                return false;
            var selected = new List<int>();
            var primary = -1;
            var nearest = double.PositiveInfinity;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!(target.Distance >= 0) || double.IsInfinity(target.Distance)
                    || !(target.AimAngle >= 0 && target.AimAngle <= 180)
                    || (target.Role != TargetRole.Guard && target.Role != TargetRole.Attacker && target.Role != TargetRole.Healer))
                    return false;
                if (kind == PlayerAttackKind.Ranged ? target.AimAngle > rangedConeDegrees : target.Distance > meleeRadius)
                    continue;
                if (kind == PlayerAttackKind.AreaMelee) selected.Add(i);
                if (target.Distance < nearest) { primary = i; nearest = target.Distance; }
            }
            if (primary < 0) return false;
            if (kind != PlayerAttackKind.AreaMelee) selected.Add(primary);
            if (!observer.Observe(new AttackEvent(kind == PlayerAttackKind.AreaMelee ? selected.Count : 0,
                targets[primary].Role, kind == PlayerAttackKind.Ranged ? AttackRange.Ranged : AttackRange.Melee, tick)))
                return false;
            hits = selected.AsReadOnly();
            return true;
        }
    }
}
