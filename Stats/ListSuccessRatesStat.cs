using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.Enums;
using Celeste.Mod.ConsistencyTracker.Models;

namespace Celeste.Mod.ConsistencyTracker.Stats;

public class ListSuccessRatesStat : Stat {
    public static string ListSuccessRates = "{list:successRates}";

    public static List<string> IDs = new() { ListSuccessRates, };

    public ListSuccessRatesStat() : base(IDs) { }

    public override string FormatStat(
        PathInfo chapterPath,
        ChapterStats chapterStats,
        string format
    ) {
        if (chapterPath == null) {
            format = StatManager.MissingPathFormat(format, ListSuccessRates);
            return format;
        }

        var rooms = new List<string>();
        foreach (var rInfo in chapterPath.Checkpoints.SelectMany(cpInfo => cpInfo.Rooms)) {
            switch (StatManager.ListOutputFormat) {
                case ListFormat.Plain:
                    rooms.Add(
                        StatManager.FormatPercentage(
                            chapterStats
                                .GetRoom(rInfo.DebugRoomName)
                                .AverageSuccessOverN(StatManager.AttemptCount)
                        )
                    );
                    break;
                case ListFormat.Json:
                    rooms.Add(
                        chapterStats
                            .GetRoom(rInfo.DebugRoomName)
                            .AverageSuccessOverN(StatManager.AttemptCount)
                            .ToString()
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        var output = string.Join(", ", rooms);
        format = StatManager.ListOutputFormat switch {
            ListFormat.Plain => format.Replace(ListSuccessRates, $"{output}"),
            ListFormat.Json => format.Replace(ListSuccessRates, $"[{output}]"),
            _ => format
        };

        return format;
    }

    public override string FormatSummary(PathInfo chapterPath, ChapterStats chapterStats) {
        return null;
    }

    public override List<KeyValuePair<string, string>> GetPlaceholderExplanations() {
        return new List<KeyValuePair<string, string>>() {
            //new KeyValuePair<string, string>(PathSuccessRatesJson, "Outputs the current path as JSON array"),
        };
    }

    public override List<StatFormat> GetStatExamples() {
        return new List<StatFormat>() {
            //new StatFormat("path-json", $"{PathSuccessRatesJson}"),
        };
    }
}