using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.Models;

namespace Celeste.Mod.ConsistencyTracker.Stats;

/*
 
 {path:names-json}

 'ST-1', 'ST-2', 'ST-3', 'CR-1', 'CR-2'
     */

public class ListCheckpointDeathsStat : Stat {

    public static string ListCheckpointDeaths = "{list:checkpointDeaths}";

    public static List<string> IDs = new() { ListCheckpointDeaths };

    public ListCheckpointDeathsStat() : base(IDs) { }

    public override string FormatStat(PathInfo chapterPath, ChapterStats chapterStats, string format) {
        if (chapterPath == null) {
            format = StatManager.MissingPathFormat(format, ListCheckpointDeaths);
            return format;
        }



        var checkpointDeaths = chapterPath.Checkpoints.Select(cpInfo => cpInfo.Rooms.Sum(rInfo => chapterStats.GetRoom(rInfo.DebugRoomName).DeathsInCurrentRun)).ToList();

        format = format.Replace(ListCheckpointDeaths, string.Join("/", checkpointDeaths));

        return format;
    }

    public override string FormatSummary(PathInfo chapterPath, ChapterStats chapterStats) {
        return null;
    }


    public override List<KeyValuePair<string, string>> GetPlaceholderExplanations() {
        return new List<KeyValuePair<string, string>>() {
            new(ListCheckpointDeaths, "Lists death count for each checkpoint in the current run (for low deaths, like '0/0/2/3/0/1')"),
        };
    }
    public override List<StatFormat> GetStatExamples() {
        return new List<StatFormat>() {
            new("list-checkpoint-deaths", $"Current run: {ListCheckpointDeaths}"),
        };
    }
}