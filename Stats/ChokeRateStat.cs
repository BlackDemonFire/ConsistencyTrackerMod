using System.Collections.Generic;
using Celeste.Mod.ConsistencyTracker.Models;

namespace Celeste.Mod.ConsistencyTracker.Stats {
    /*
     Stats to implement:
     {room:chokeRate}
     {room:chokeRateSession}
     {checkpoint:chokeRate}
     {checkpoint:chokeRateSession}

     */

    public class ChokeRateStat : Stat {
        public static string RoomChokeRate = "{room:chokeRate}";
        public static string RoomChokeRateSession = "{room:chokeRateSession}";
        public static string CheckpointChokeRate = "{checkpoint:chokeRate}";
        public static string CheckpointChokeRateSession = "{checkpoint:chokeRateSession}";
        public static List<string> IDs = new List<string>()
        {
            RoomChokeRate,
            RoomChokeRateSession,
            CheckpointChokeRate,
            CheckpointChokeRateSession
        };

        public ChokeRateStat() : base(IDs) { }

        public override string FormatStat(
            PathInfo chapterPath,
            ChapterStats chapterStats,
            string format
        ) {
            if (chapterPath == null) {
                format = StatManager.MissingPathFormat(format, RoomChokeRate);
                format = StatManager.MissingPathFormat(format, RoomChokeRateSession);
                format = StatManager.MissingPathFormat(format, CheckpointChokeRate);
                format = StatManager.MissingPathFormat(format, CheckpointChokeRateSession);
                return format;
            }

            CheckpointInfo currentCpInfo = null;

            //======== Room ========
            var goldenDeathsInRoom = new[] { 0, 0 };
            var goldenDeathsAfterRoom = new[] { 0, 0 };

            var pastRoom = false;

            foreach (var cpInfo in chapterPath.Checkpoints) {
                foreach (var rInfo in cpInfo.Rooms) {
                    var rStats = chapterStats.GetRoom(rInfo.DebugRoomName);

                    if (pastRoom) {
                        goldenDeathsAfterRoom[0] += rStats.GoldenBerryDeaths;
                        goldenDeathsAfterRoom[1] += rStats.GoldenBerryDeathsSession;
                    }

                    if (rInfo.DebugRoomName != chapterStats.CurrentRoom.DebugRoomName)
                        continue;
                    currentCpInfo = cpInfo;
                    pastRoom = true;
                    goldenDeathsInRoom[0] = rStats.GoldenBerryDeaths;
                    goldenDeathsInRoom[1] = rStats.GoldenBerryDeathsSession;
                }
            }

            float crRoom,
                crRoomSession;

            //Calculate
            if (goldenDeathsInRoom[0] + goldenDeathsAfterRoom[0] == 0)
                crRoom = float.NaN;
            else
                crRoom =
                    (float)goldenDeathsInRoom[0]
                    / (goldenDeathsInRoom[0] + goldenDeathsAfterRoom[0]);

            if (goldenDeathsInRoom[1] + goldenDeathsAfterRoom[1] == 0)
                crRoomSession = float.NaN;
            else
                crRoomSession =
                    (float)goldenDeathsInRoom[1]
                    / (goldenDeathsInRoom[1] + goldenDeathsAfterRoom[1]);

            //Format
            if (float.IsNaN(crRoom)) { //pastRoom is false when player is not on path
                format = format.Replace(RoomChokeRate, "-%");
            } else if (pastRoom == false) {
                format = StatManager.NotOnPathFormatPercent(format, RoomChokeRate);
            } else {
                format = format.Replace(RoomChokeRate, $"{StatManager.FormatPercentage(crRoom)}");
            }

            if (float.IsNaN(crRoomSession)) {
                format = format.Replace(RoomChokeRateSession, "-%");
            } else if (pastRoom == false) {
                format = StatManager.NotOnPathFormatPercent(format, RoomChokeRateSession);
            } else {
                format = format.Replace(RoomChokeRateSession, $"{StatManager.FormatPercentage(crRoomSession)}");
            }

            //======== Checkpoint ========

            if (currentCpInfo != null) { //Check if player is on path
                var goldenDeathsInCP = new[] { 0, 0 };
                var goldenDeathsAfterCP = new[] { 0, 0 };

                var pastCP = false;

                foreach (var cpInfo in chapterPath.Checkpoints) {
                    if (pastCP) {
                        goldenDeathsAfterCP[0] += cpInfo.Stats.GoldenBerryDeaths;
                        goldenDeathsAfterCP[1] += cpInfo.Stats.GoldenBerryDeathsSession;
                    }

                    if (cpInfo != currentCpInfo)
                        continue;
                    pastCP = true;
                    goldenDeathsInCP[0] = cpInfo.Stats.GoldenBerryDeaths;
                    goldenDeathsInCP[1] = cpInfo.Stats.GoldenBerryDeathsSession;
                }

                float crCheckpoint,
                    crCheckpointSession;

                //Calculate
                if (goldenDeathsInCP[0] + goldenDeathsAfterCP[0] == 0)
                    crCheckpoint = float.NaN;
                else
                    crCheckpoint =
                        (float)goldenDeathsInCP[0] / (goldenDeathsInCP[0] + goldenDeathsAfterCP[0]);

                if (goldenDeathsInCP[1] + goldenDeathsAfterCP[1] == 0)
                    crCheckpointSession = float.NaN;
                else
                    crCheckpointSession =
                        (float)goldenDeathsInCP[1] / (goldenDeathsInCP[1] + goldenDeathsAfterCP[1]);

                //Format
                format = format.Replace(CheckpointChokeRate, float.IsNaN(crCheckpoint) ? "-%" : $"{StatManager.FormatPercentage(crCheckpoint)}");

                format = format.Replace(CheckpointChokeRateSession, float.IsNaN(crCheckpointSession) ? "-%" : $"{StatManager.FormatPercentage(crCheckpointSession)}");
            } else {
                //Player is not on path
                format = format.Replace(CheckpointChokeRate, "-%");
                format = format.Replace(CheckpointChokeRateSession, "-%");
            }

            return format;
        }

        public override string FormatSummary(PathInfo chapterPath, ChapterStats chapterStats) {
            return null;
        }

        //choke-rate;Room Choke Rate: {room:chokeRate} (CP: {checkpoint:chokeRate})
        public override List<KeyValuePair<string, string>> GetPlaceholderExplanations() {
            return new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair < string, string >(RoomChokeRate, "Choke Rate of the current room (how many runs died to this room / how many runs passed this room)"),
                new KeyValuePair < string, string >(RoomChokeRateSession, "Choke Rate of the current room in the current session"),
                new KeyValuePair < string, string >(CheckpointChokeRate, "Choke Rate of the current checkpoint"),
                new KeyValuePair < string, string >(CheckpointChokeRateSession, "Choke Rate of the current checkpoint in the current session"),
            };
        }

        public override List<StatFormat> GetStatExamples() {
            return new List<StatFormat>()
            {
                new StatFormat("choke-rate", $"Room Choke Rate: {RoomChokeRate} (CP: {CheckpointChokeRate})")
            };
        }
    }
}
