namespace Celeste.Mod.ConsistencyTracker.Models;

public class AggregateStats {
    public int CountSuccesses { get; set; } = 0;
    public int CountAttempts { get; set; } = 0;
    public int CountFailures => CountAttempts - CountSuccesses;

    public float SuccessRate {
        get {
            if (CountAttempts == 0)
                return 0;

            return (float)CountSuccesses / CountAttempts;
        }
    }

    public int GoldenBerryDeaths { get; set; } = 0;
    public int GoldenBerryDeathsSession { get; set; } = 0;

    public float GoldenChance { get; set; } = 1;
}