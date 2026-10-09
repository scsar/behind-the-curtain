using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BehindTheCurtain.Core;

namespace BehindTheCurtain.Prototype
{
    // Constructor metadata is the sole source of numeric names and defaults.
    internal static class PrototypePuppetTuning
    {
        private static ConstructorInfo Constructor(Type type) => type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length).First();
        private static bool Numeric(Type type) => type == typeof(int) || type == typeof(double) || type == typeof(float);
        private static bool Setting(Type type) => type.Assembly == typeof(PuppetSettings).Assembly
            && type.IsClass && type.Name.EndsWith("Settings", StringComparison.Ordinal);
        private static bool SceneParameter(Type type, string name) => type == typeof(PuppetSettings)
            && (name == "arenaHalfSize" || name == "ticksPerSecond");
        internal static Dictionary<string, double> Defaults()
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            Collect(typeof(PuppetSettings), values);
            return values;
        }
        private static void Collect(Type type, Dictionary<string, double> values)
        {
            foreach (var parameter in Constructor(type).GetParameters())
                if (Numeric(parameter.ParameterType) && !SceneParameter(type, parameter.Name))
                    values[type.Name + "." + parameter.Name] = Convert.ToDouble(parameter.DefaultValue, CultureInfo.InvariantCulture);
                else if (Setting(parameter.ParameterType)) Collect(parameter.ParameterType, values);
        }
        internal static PuppetSettings Build(PrototypeSettings settings, bool defaults,
            Action<string> warn, out Dictionary<string, double> applied)
        {
            var values = Defaults();
            if (!defaults && settings.PuppetTuning != null)
                foreach (var entry in settings.PuppetTuning)
                {
                    if (entry == null || entry.Name == null || !values.ContainsKey(entry.Name))
                    { warn("알 수 없는 인형 수치: " + (entry?.Name ?? "<null>")); continue; }
                    values[entry.Name] = entry.Value;
                }
            var result = (PuppetSettings)Construct(typeof(PuppetSettings), settings, values);
            applied = values;
            return result;
        }
        private static object Construct(Type type, PrototypeSettings settings, Dictionary<string, double> values)
        {
            var constructor = Constructor(type);
            var arguments = constructor.GetParameters().Select(parameter =>
            {
                if (Setting(parameter.ParameterType)) return Construct(parameter.ParameterType, settings, values);
                if (!Numeric(parameter.ParameterType)) return parameter.HasDefaultValue ? parameter.DefaultValue : null;
                double value;
                var key = type.Name + "." + parameter.Name;
                if (SceneParameter(type, parameter.Name))
                    value = parameter.Name == "ticksPerSecond" ? PrototypeSettings.TicksPerSecond
                        : settings.FloorSize / 2f - settings.PuppetSize / 2f;
                else value = values[key];
                object converted;
                if (parameter.ParameterType == typeof(int)) converted = checked((int)Math.Round(value));
                else if (parameter.ParameterType == typeof(float)) converted = (float)value;
                else converted = value;
                if (values.ContainsKey(key)) values[key] = Convert.ToDouble(converted, CultureInfo.InvariantCulture);
                return converted;
            }).ToArray();
            return constructor.Invoke(arguments);
        }
        internal static bool Different(double value, double baseline) =>
            Math.Abs(value - baseline) > 1e-6 * Math.Max(1, Math.Abs(baseline));
        internal static string[] Differences(Dictionary<string, double> applied)
        {
            var defaults = Defaults();
            return applied.Where(entry => Different(entry.Value, defaults[entry.Key]))
                .Select(entry => entry.Key + "=" + entry.Value.ToString("0.####", CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
