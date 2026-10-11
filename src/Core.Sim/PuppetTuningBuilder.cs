using System.Globalization;
using System.Reflection;

namespace BehindTheCurtain.Core.Sim;

public static class PuppetTuningBuilder
{
    private static ConstructorInfo Constructor(Type type) => type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
    private static bool Numeric(Type type) => type == typeof(int) || type == typeof(float) || type == typeof(double);
    private static bool Setting(Type type) => type.Assembly == typeof(PuppetSettings).Assembly && type.IsClass && type.Name.EndsWith("Settings", StringComparison.Ordinal);
    private static bool Scene(Type type, string? name) => type == typeof(PuppetSettings) && name is "arenaHalfSize" or "ticksPerSecond";
    public static Dictionary<string, double> Defaults()
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        void Collect(Type type)
        {
            foreach (var p in Constructor(type).GetParameters())
                if (Setting(p.ParameterType)) Collect(p.ParameterType);
                else if (Numeric(p.ParameterType) && !Scene(type, p.Name)) values[type.Name + "." + p.Name] = Convert.ToDouble(p.DefaultValue, CultureInfo.InvariantCulture);
        }
        Collect(typeof(PuppetSettings));
        return values;
    }
    public static bool TryBuild(IReadOnlyDictionary<string, string> overrides, PuppetScenarioSettings scenario,
        out PuppetSettings? settings, out string differences, out string reason)
    {
        settings = null; differences = ""; reason = "";
        var values = Defaults();
        foreach (var entry in overrides)
        {
            if (!values.ContainsKey(entry.Key)) { reason = "Unknown tuning: " + entry.Key; return false; }
            if (!double.TryParse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            { reason = "Invalid number: " + entry.Key + "=" + entry.Value; return false; }
            values[entry.Key] = value;
        }
        object Construct(Type type)
        {
            return Constructor(type).Invoke(Constructor(type).GetParameters().Select(p =>
            {
                if (Setting(p.ParameterType)) return Construct(p.ParameterType);
                if (!Numeric(p.ParameterType)) return p.HasDefaultValue ? p.DefaultValue : null;
                string key = type.Name + "." + p.Name;
                double value = Scene(type, p.Name) ? (p.Name == "arenaHalfSize" ? scenario.ArenaHalfSize : new PuppetSettings().TicksPerSecond) : values[key];
                object converted;
                if (p.ParameterType == typeof(int)) converted = checked((int)Math.Round(value));
                else if (p.ParameterType == typeof(float)) converted = (float)value;
                else converted = value;
                if (values.ContainsKey(key)) values[key] = Convert.ToDouble(converted, CultureInfo.InvariantCulture);
                return converted;
            }).ToArray());
        }
        try { settings = (PuppetSettings)Construct(typeof(PuppetSettings)); }
        catch (Exception e) when (e is OverflowException or TargetInvocationException or ArgumentException)
        { reason = "Cannot construct settings: " + e.GetType().Name; return false; }
        var defaults = Defaults();
        differences = string.Join(",", values.Where(e => Math.Abs(e.Value - defaults[e.Key]) > 1e-6 * Math.Max(1, Math.Abs(defaults[e.Key])))
            .Select(e => e.Key + "=" + e.Value.ToString("0.####", CultureInfo.InvariantCulture)));
        var validation = PuppetBrain.Create(new World(), settings, new DeterministicRandom(1));
        if (!validation.Success) { reason = "PuppetBrain.Create: " + validation.Failure; return false; }
        return true;
    }
}
