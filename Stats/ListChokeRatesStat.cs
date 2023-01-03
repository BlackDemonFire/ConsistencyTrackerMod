using System;
using System.Collections.Generic;
using Celeste.Mod.ConsistencyTracker.Enums;
using Celeste.Mod.ConsistencyTracker.Models;

namespace Celeste.Mod.ConsistencyTracker.Stats;

public class ListChokeRatesStat : Stat {
    public static string ListChokeRates = "{list:chokeRates}";
    public static string ListChokeRatesSession = "{list:chokeRatesSession}";

    public static List<string> IDs = new() { ListChokeRates, ListChokeRatesSession };

    public ListChokeRatesStat() : base(IDs) { }

    public override string FormatStat(
        PathInfo chapterPath,
        ChapterStats chapterStats,
        string format
    ) {
        if (chapterPath == null) {
            format = StatManager.MissingPathFormat(format, ListChokeRates);
            format = StatManager.MissingPathFormat(format, ListChokeRatesSession);
            return format;
        }

        List<string> chokeRates = new();
        List<string> chokeRatesSession = new();

        //For every room
        foreach (var cpInfo in chapterPath.Checkpoints) {
            foreach (var rInfo in cpInfo.Rooms) {
                //We go through all other rooms to calc choke rate
                var pastRoom = false;
                var goldenDeathsInRoom = new[] { 0, 0 };
                var goldenDeathsAfterRoom = new[] { 0, 0 };

                foreach (var cpInfoTemp in chapterPath.Checkpoints) {
                    foreach (var rInfoTemp in cpInfoTemp.Rooms) {
                        var rStats = chapterStats.GetRoom(rInfoTemp.DebugRoomName);

                        if (pastRoom) {
                            goldenDeathsAfterRoom[0] += rStats.GoldenBerryDeaths;
                            goldenDeathsAfterRoom[1] += rStats.GoldenBerryDeathsSession;
                        }

                        if (rInfoTemp.DebugRoomName != rInfo.DebugRoomName)
                            continue;
                        pastRoom = true;
                        goldenDeathsInRoom[0] = rStats.GoldenBerryDeaths;
                        goldenDeathsInRoom[1] = rStats.GoldenBerryDeathsSession;
                    }
                }

                float crRoom,
                    crRoomSession;

                //Calculate
                if (goldenDeathsInRoom[0] + goldenDeathsAfterRoom[0] == 0)
                    crRoom = 0;
                else
                    crRoom =
                        (float)goldenDeathsInRoom[0]
                        / (goldenDeathsInRoom[0] + goldenDeathsAfterRoom[0]);

                if (goldenDeathsInRoom[1] + goldenDeathsAfterRoom[1] == 0)
                    crRoomSession = 0;
                else
                    crRoomSession =
                        (float)goldenDeathsInRoom[1]
                        / (goldenDeathsInRoom[1] + goldenDeathsAfterRoom[1]);

                //Format
                switch (StatManager.ListOutputFormat) {
                    case ListFormat.Plain:
                        chokeRates.Add(StatManager.FormatPercentage(crRoom));
                        chokeRatesSession.Add(StatManager.FormatPercentage(crRoomSession));
                        break;
                    case ListFormat.Json:
                        chokeRates.Add(crRoom.ToString());
                        chokeRatesSession.Add(crRoomSession.ToString());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        var output = string.Join(", ", chokeRates);
        var outputSession = string.Join(", ", chokeRatesSession);

        switch (StatManager.ListOutputFormat) {
            case ListFormat.Plain:
                format = format.Replace(ListChokeRates, $"{output}");
                format = format.Replace(ListChokeRatesSession, $"{outputSession}");
                break;
            case ListFormat.Json:
                format = format.Replace(ListChokeRates, $"[{output}]");
                format = format.Replace(ListChokeRatesSession, $"[{outputSession}]");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

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