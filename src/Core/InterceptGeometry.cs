using System;

namespace BehindTheCurtain.Core
{
    /// <summary>Shared intercept segment geometry for rules and independent simulation measurements.</summary>
    public static class InterceptGeometry
    {
        public static Vec2 ClosestPoint(Vec2 position, Vec2 healer, Vec2 threat, PuppetSettings settings)
        {
            var direction = PuppetActions.Direction(healer, threat, new Vec2(0, 1));
            var start = new Vec2(healer.X + direction.X * settings.InterceptDistance,
                healer.Z + direction.Z * settings.InterceptDistance);
            if (PuppetActions.Distance(healer, threat) < settings.InterceptDistance + settings.InterceptStandOff)
                return start;
            var end = new Vec2(threat.X - direction.X * settings.InterceptStandOff,
                threat.Z - direction.Z * settings.InterceptStandOff);
            return Project(position, start, end);
        }

        internal static Vec2 Project(Vec2 position, Vec2 start, Vec2 end)
        {
            double x = end.X - start.X, z = end.Z - start.Z;
            double squared = x * x + z * z;
            double t = squared == 0 ? 0 : Math.Max(0, Math.Min(1,
                ((position.X - start.X) * x + (position.Z - start.Z) * z) / squared));
            return new Vec2(start.X + t * x, start.Z + t * z);
        }
    }
}
