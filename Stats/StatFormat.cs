namespace Celeste.Mod.ConsistencyTracker.Stats
{
    public class StatFormat
    {
        public string Name { get; set; }
        public string Format { get; set; }

        public StatFormat(string pName, string pFormat)
        {
            Name = pName;
            Format = pFormat;
        }
    }
}
