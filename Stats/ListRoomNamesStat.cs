using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.Enums;
using Celeste.Mod.ConsistencyTracker.Models;

namespace Celeste.Mod.ConsistencyTracker.Stats {
    /*
     
     {path:names-json}

     'ST-1', 'ST-2', 'ST-3', 'CR-1', 'CR-2'
         */

    public class ListRoomNamesStat : Stat {
        public static string ListRoomNames = "{list:roomNames}";

        public static List<string> IDs = new() { ListRoomNames, };

        public ListRoomNamesStat() : base(IDs) { }

        public override string FormatStat(
            PathInfo chapterPath,
            ChapterStats chapterStats,
            string format
        ) {
            if (chapterPath == null) {
                format = StatManager.MissingPathFormat(format, ListRoomNames);
                return format;
            }

            var rooms = (from cpInfo in chapterPath.Checkpoints from rInfo in cpInfo.Rooms select $"{StatManager.GetFormattedRoomName(rInfo)}").ToList();

            switch (StatManager.ListOutputFormat) {
                case ListFormat.Plain: {
                    var output = string.Join(", ", rooms);
                    format = format.Replace(ListRoomNames, $"{output}");
                    break;
                }
                case ListFormat.Json: {
                    var output = string.Join("', '", rooms);
                    format = format.Replace(ListRoomNames, $"['{output}']");
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return format;
        }

        public override string FormatSummary(PathInfo chapterPath, ChapterStats chapterStats) {
            return null;
        }

        public override List<KeyValuePair<string, string>> GetPlaceholderExplanations() {
            return new List<KeyValuePair<string, string>>()
            {
                new(ListRoomNames, "Outputs the current path as list"),
                new(ListSuccessRatesStat.ListSuccessRates, "Outputs the success rate for all rooms on the current path as list"),
                new(ListChokeRatesStat.ListChokeRates, "Outputs the choke rates for all rooms on the current path as list"),
                new(StreakStat.ListRoomStreaks, "Outputs the current streaks for all rooms on the current path as list"),
            };
        }
        public override List<StatFormat> GetStatExamples() {
            return new List<StatFormat>() {
                new("list-room-names", $"Names: {ListRoomNames}\\nSuccess Rates: {ListSuccessRatesStat.ListSuccessRates}\\nChoke Rates: {ListChokeRatesStat.ListChokeRates}\\n"),
            };
        }
    }
}
