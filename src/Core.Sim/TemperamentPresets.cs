namespace BehindTheCurtain.Core.Sim;

public static class TemperamentPresets
{
    public static readonly string[] Names = { "none", "bold", "cautious", "persistent", "whimsical", "mixed" };
    public static Temperament ForAgent(string name, int id)
    {
        if (name == "mixed") name = id switch { 10 => "bold", 11 => "cautious", 12 => "whimsical", 13 => "persistent", _ => "none" };
        return new Temperament(new Dictionary<string, double>
        {
            [TemperamentAxisIds.Boldness] = name == "bold" ? 1 : name == "cautious" ? -1 : 0,
            [TemperamentAxisIds.Persistence] = name == "persistent" ? 1 : name == "whimsical" ? -1 : 0
        });
    }
}
