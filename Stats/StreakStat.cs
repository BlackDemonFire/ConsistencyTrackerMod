using System;
using Celeste.Mod.ConsistencyTracker.Enums;
using Celeste.Mod.ConsistencyTracker.Models;
using System.Collections.Generic;

namespace Celeste.Mod.ConsistencyTracker.Stats {

    /*
     Stats to implement:
     {room:successRate} - Success Rate of the current room
     {checkpoint:successRate} - Average Success Rate of current checkpoint
     {chapter:successRate} - Average Success Rate of the entire chapter

         */

    public class StreakStat : Stat {
        public static string RoomCurrentStreak = "{room:currentStreak}";
        public static string CheckpointCurrentStreak = "{checkpoint:currentStreak}";
        public static string ListRoomStreaks = "{list:roomStreaks}";

        public static List<string> IDs = new List<string>() { RoomCurrentStreak, CheckpointCurrentStreak, ListRoomStreaks };

        public StreakStat() : base(IDs) {
        }

        public override string FormatStat(PathInfo chapterPath, ChapterStats chapterStats, string format) {
            if (chapterPath == null) {
                //Player doesn't have path
                format = StatManager.MissingPathFormat(format, RoomCurrentStreak);
                format = StatManager.MissingPathFormat(format, CheckpointCurrentStreak);
                return format;
            } else if (chapterPath.CurrentRoom == null) {
                //or is not on the path
                format = StatManager.NotOnPathFormat(format, RoomCurrentStreak);
                format = StatManager.NotOnPathFormat(format, CheckpointCurrentStreak);
                return format;
            }

            var streak = chapterStats.GetRoom(chapterPath.CurrentRoom.DebugRoomName).SuccessStreak;

            var roomStreaks = new List<int>();
            //Checkpoint
            var cpLowestStreak = 100;
            foreach (var cpInfo in chapterPath.Checkpoints) {
                var cpLowestCheck = 100;
                var isInCp = false;

                foreach (var rInfo in cpInfo.Rooms) {
                    var rStats = chapterStats.GetRoom(rInfo.DebugRoomName);
                    var rStreak = rStats.SuccessStreak;

                    roomStreaks.Add(rStreak);

                    if (rStreak < cpLowestStreak) {
                        cpLowestCheck = rStreak;
                    }

                    if (rInfo.DebugRoomName == chapterPath.CurrentRoom.DebugRoomName) {
                        isInCp = true;
                    }
                }

                if (isInCp) {
                    cpLowestStreak = cpLowestCheck;
                }
            }


            format = format.Replace(RoomCurrentStreak, $"{streak}");
            format = format.Replace(CheckpointCurrentStreak, $"{cpLowestStreak}");


            switch (StatManager.ListOutputFormat) {
                case ListFormat.Plain: {
                    var output = string.Join(", ", roomStreaks);
                    format = format.Replace(ListRoomStreaks, $"{output}");
                    break;
                }
                case ListFormat.Json: {
                    var output = string.Join(", ", roomStreaks);
                    format = format.Replace(ListRoomStreaks, $"[{output}]");
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


        //success-rate;Room SR: {room:successRate} | CP: {checkpoint:successRate} | Total: {chapter:successRate}
        public override List<KeyValuePair<string, string>> GetPlaceholderExplanations() {
            return new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(RoomCurrentStreak, "Current streak of beating the current room deathless"),
                new KeyValuePair<string, string>(CheckpointCurrentStreak, "Current streak of beating the current checkpoint deathless"),
            };
        }

        public override List<StatFormat> GetStatExamples() {
            return new List<StatFormat>()
            {
                new StatFormat("current-streak",
                    $"Current Room Streak: {RoomCurrentStreak}, Checkpoint: {CheckpointCurrentStreak}")
            };
        }
    }
}