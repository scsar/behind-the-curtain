using System;
using System.Collections.Generic;
using System.Globalization;
using BehindTheCurtain.Core;
using UnityEngine;

namespace BehindTheCurtain.Prototype
{
    public sealed partial class PrototypeJointAttack
    {
        private bool temperamentsEnabled = true;

        private static string FormatTemperament(double value) =>
            value.ToString("+0.0;−0.0;0.0", CultureInfo.InvariantCulture);

        private Temperament ConfiguredTemperament(int index)
        {
            if (!temperamentsEnabled || settings.TemperamentMode == TemperamentMode.Off)
                return new Temperament();
            double boldness = 0, persistence = 0;
            if (settings.TemperamentMode == TemperamentMode.Random)
            {
                // Fixed tuple mixing, independent of brain randomness and iteration order.
                var seed = unchecked((ulong)(uint)settings.Seed
                    ^ ((ulong)(uint)encounterNumber * 0x9E3779B97F4A7C15UL)
                    ^ ((ulong)(uint)PuppetAgentId(index).Value * 0xBF58476D1CE4E5B9UL));
                var random = new DeterministicRandom(seed);
                boldness = Math.Round(random.NextDouble() * 2 - 1, 1, MidpointRounding.AwayFromZero);
                persistence = Math.Round(random.NextDouble() * 2 - 1, 1, MidpointRounding.AwayFromZero);
            }
            else if (settings.TemperamentMode == TemperamentMode.Manual
                && settings.ManualTemperaments != null && index < settings.ManualTemperaments.Length)
            {
                boldness = Mathf.Clamp(settings.ManualTemperaments[index].x, -1f, 1f);
                persistence = Mathf.Clamp(settings.ManualTemperaments[index].y, -1f, 1f);
            }
            return new Temperament(new Dictionary<string, double> {
                { TemperamentAxisIds.Boldness, boldness },
                { TemperamentAxisIds.Persistence, persistence }
            });
        }

        private void ApplyTemperaments()
        {
            var world = expedition.EncounterWorld;
            for (var i = 0; i < puppets.Length; i++)
            {
                var id = PuppetAgentId(i);
                var applied = world.SetTemperament(id, ConfiguredTemperament(i));
                if (applied.Success) continue;
                WarnPuppetTuning($"결 적용 거부: Puppet {id.Value}: {applied.Failure}; 0으로 복귀");
                var fallback = world.SetTemperament(id, new Temperament());
                if (!fallback.Success)
                    WarnPuppetTuning($"결 0 적용 거부: Puppet {id.Value}: {fallback.Failure}");
            }
        }
    }
}
