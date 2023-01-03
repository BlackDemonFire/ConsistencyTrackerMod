using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Celeste.Mod.ConsistencyTracker.Models;

[Serializable]
public class ChapterStats {
    public static readonly int MaxAttemptCount = 100;

    public string CampaignName { get; set; }
    public string ChapterName { get; set; }
    public string ChapterSID { get; set; }
    public string ChapterSIDDialogSanitized { get; set; }
    public string ChapterDebugName { get; set; }
    public RoomStats CurrentRoom { get; set; }
    public Dictionary<string, RoomStats> Rooms { get; set; } = new();
    public static Dictionary<string, List<RoomStats>> LastGoldenRuns { get; set; } = new(); //Latest runs will always be at the end of the list
    public List<RoomStats> CurrentChapterLastGoldenRuns {
        get {
            if (!LastGoldenRuns.ContainsKey(ChapterDebugName)) {
                LastGoldenRuns.Add(ChapterDebugName, new List<RoomStats>());
            }
            return LastGoldenRuns[ChapterDebugName];
        }
        private set => _ = value;
    }

    public ModState ModState { get; set; } = new ModState();

    /// <summary>Adds the attempt to the specified room.</summary>
    /// <param name="debugRoomName">debug name of the room.</param>
    /// <param name="success">if the attempt was successful.</param>
    public void AddAttempt(string debugRoomName, bool success) {
        var targetRoom = GetRoom(debugRoomName);
        targetRoom.AddAttempt(success);
    }

    /// <summary>Adds the attempt to the current room</summary>
    /// <param name="success">if the attempt was successful.</param>
    public void AddAttempt(bool success) {
        CurrentRoom.AddAttempt(success);
    }

    public void AddGoldenBerryDeath() {
        CurrentChapterLastGoldenRuns.Add(CurrentRoom);
        CurrentRoom.GoldenBerryDeaths++;
        CurrentRoom.GoldenBerryDeathsSession++;
    }

    public void SetCurrentRoom(string debugRoomName) {
        var targetRoom = GetRoom(debugRoomName);
        CurrentRoom = targetRoom;
    }

    public RoomStats GetRoom(string debugRoomName) {
        RoomStats targetRoom;
        if (Rooms.ContainsKey(debugRoomName)) {
            targetRoom = Rooms[debugRoomName];
        } else {
            targetRoom = new RoomStats() { DebugRoomName = debugRoomName };
            Rooms[debugRoomName] = targetRoom;
        }
        return targetRoom;
    }

    public void ResetCurrentSession() {
        foreach (var room in Rooms.Keys.Select(name => Rooms[name])) {
            room.GoldenBerryDeathsSession = 0;
        }
    }

    public string ToChapterStatsString() {
        List<string> lines = new() { $"{CurrentRoom}" };
        lines.AddRange(Rooms.Keys.Select(key => $"{Rooms[key]}"));

        return string.Join("\n", lines) + "\n";
    }

    public static ChapterStats ParseString(string content) {
        var lines = content
            .Split(new[] { "\n" }, StringSplitOptions.None)
            .ToList();
        ChapterStats stats = new();

        foreach (var room in lines.TakeWhile(line => line.Trim() != "").Select(RoomStats.ParseString)) {
            if (stats.Rooms.Count == 0) { //First element is always the current room
                stats.CurrentRoom = room;
            }

            if (!stats.Rooms.ContainsKey(room.DebugRoomName)) //Skip duplicate entries, as the current room is always the first line and also found later on
                stats.Rooms.Add(room.DebugRoomName, room);
        }

        return stats;
    }

    private Dictionary<string, int> _unvisitedRoomsToRoomNumber = new();

    public void OutputSummary(string outPath, PathInfo info, int attemptCount) {
        StringBuilder sb = new();
        sb.AppendLine($"Tracker summary for chapter '{ChapterDebugName}'");
        sb.AppendLine($"");
        sb.AppendLine($"--- Golden Berry Deaths ---"); //Room->Checkpoint->Chapter + 1

        var chapterDeaths = (from cpInfo in info.Checkpoints from rInfo in cpInfo.Rooms where Rooms.ContainsKey(rInfo.DebugRoomName) select Rooms[rInfo.DebugRoomName] into rStats select rStats.GoldenBerryDeaths).Sum();

        foreach (var cpInfo in info.Checkpoints) {
            var checkpointDeaths = 0;

            foreach (var rInfo in cpInfo.Rooms) {
                if (!Rooms.ContainsKey(rInfo.DebugRoomName))
                    continue; //Skip rooms the player has not yet visited.

                var rStats = Rooms[rInfo.DebugRoomName];
                checkpointDeaths += rStats.GoldenBerryDeaths;
            }

            var percentStr = (checkpointDeaths / (double)chapterDeaths).ToString("P2", CultureInfo.InvariantCulture);
            sb.AppendLine($"{cpInfo.Name}: {checkpointDeaths} ({percentStr})");

            var roomNumber = 0;
            foreach (var rInfo in cpInfo.Rooms) {
                roomNumber++;

                if (!Rooms.ContainsKey(rInfo.DebugRoomName)) {
                    _unvisitedRoomsToRoomNumber.Add(rInfo.DebugRoomName, roomNumber);
                    sb.AppendLine($"\t{cpInfo.Abbreviation}-{roomNumber}: 0");
                } else {
                    var rStats = Rooms[rInfo.DebugRoomName];
                    rStats.RoomNumber = roomNumber;
                    sb.AppendLine(
                        $"\t{cpInfo.Abbreviation}-{roomNumber}: {rStats.GoldenBerryDeaths}"
                    );
                }
            }
        }
        sb.AppendLine($"Total Golden Berry Deaths: {chapterDeaths}");

        sb.AppendLine($"");
        sb.AppendLine($"");

        sb.AppendLine($"--- Consistency Stats ---");
        sb.AppendLine($"- Success Rate"); //Room->Checkpoint->Chapter

        double chapterSuccessRateSum = 0f;
        var chapterRoomCount = 0;
        var chapterSuccesses = 0;
        var chapterAttempts = 0;
        double chapterGoldenChance = 1;

        foreach (var cpInfo in info.Checkpoints) {
            double checkpointSuccessRateSum = 0f;
            var checkpointRoomCount = 0;
            var checkpointSuccesses = 0;
            var checkpointAttempts = 0;

            foreach (var rInfo in cpInfo.Rooms) {
                if (!Rooms.ContainsKey(rInfo.DebugRoomName))
                    continue; //Skip rooms the player has not yet visited.

                var rStats = Rooms[rInfo.DebugRoomName];
                var rRate = rStats.AverageSuccessOverN(attemptCount);

                checkpointSuccessRateSum += rRate;
                checkpointRoomCount++;

                chapterSuccessRateSum += rRate;
                chapterRoomCount++;

                var rSuccesses = rStats.SuccessesOverN(attemptCount);
                var rAttempts = rStats.AttemptsOverN(attemptCount);

                checkpointSuccesses += rSuccesses;
                checkpointAttempts += rAttempts;

                chapterSuccesses += rSuccesses;
                chapterAttempts += rAttempts;

                cpInfo.GoldenChance *= rRate;
                chapterGoldenChance *= rRate;
            }

            var cpPercentStr = (checkpointSuccessRateSum / checkpointRoomCount).ToString(
                "P2",
                CultureInfo.InvariantCulture
            );
            sb.AppendLine(
                $"{cpInfo.Name}: {cpPercentStr} ({checkpointSuccesses} successes / {checkpointAttempts} attempts)"
            );

            foreach (var rInfo in cpInfo.Rooms) {
                if (!Rooms.ContainsKey(rInfo.DebugRoomName)) {
                    var rPercentStr = 0.ToString("P2", CultureInfo.InvariantCulture);
                    sb.AppendLine(
                        $"\t{cpInfo.Abbreviation}-{_unvisitedRoomsToRoomNumber[rInfo.DebugRoomName]}: {rPercentStr} (0 / 0)"
                    );
                } else {
                    var rStats = Rooms[rInfo.DebugRoomName];
                    var rPercentStr = rStats
                        .AverageSuccessOverN(attemptCount)
                        .ToString("P2", CultureInfo.InvariantCulture);
                    sb.AppendLine($"\t{cpInfo.Abbreviation}-{rStats.RoomNumber}: {rPercentStr} ({rStats.SuccessesOverN(attemptCount)} / {rStats.AttemptsOverN(attemptCount)})");
                }
            }
        }
        var cPercentStr = (chapterSuccessRateSum / chapterRoomCount).ToString("P2", CultureInfo.InvariantCulture);
        sb.AppendLine($"Total Success Rate: {cPercentStr} ({chapterSuccesses} successes / {chapterAttempts} attempts)");

        sb.AppendLine($"");

        sb.AppendLine($"- Choke Rate"); //Choke Rate
        var cpChokeRates = new Dictionary<CheckpointInfo, int>();
        var roomChokeRates = new Dictionary<RoomInfo, int>();

        foreach (var cpInfo in info.Checkpoints) {
            cpChokeRates.Add(cpInfo, 0);

            foreach (var rInfo in cpInfo.Rooms) {
                roomChokeRates.Add(rInfo, 0);

                if (!Rooms.ContainsKey(rInfo.DebugRoomName))
                    continue; //Skip rooms the player has not yet visited.
                roomChokeRates[rInfo] = Rooms[rInfo.DebugRoomName].GoldenBerryDeaths;
                cpChokeRates[cpInfo] += Rooms[rInfo.DebugRoomName].GoldenBerryDeaths;
            }
        }

        sb.AppendLine($"");
        sb.AppendLine($"Room Name,Choke Rate,Golden Runs to Room,Room Deaths");
        var goldenAchieved = true;

        foreach (var cpInfo in info.Checkpoints) {
            foreach (var rInfo in cpInfo.Rooms) { //For every room
                var goldenRunsToRoom = 0;
                var foundRoom = false;

                foreach (var cpInfoTemp in info.Checkpoints) { //Iterate all remaining rooms and sum up their golden deaths
                    foreach (var rInfoTemp in cpInfoTemp.Rooms) {
                        if (rInfoTemp == rInfo)
                            foundRoom = true;
                        if (foundRoom) {
                            goldenRunsToRoom += roomChokeRates[rInfoTemp];
                        }
                    }
                }

                if (goldenAchieved)
                    goldenRunsToRoom++;

                var roomNumber = Rooms.ContainsKey(rInfo.DebugRoomName) ? Rooms[rInfo.DebugRoomName].RoomNumber : _unvisitedRoomsToRoomNumber[rInfo.DebugRoomName];

                var roomChokeRate = 0f;
                if (goldenRunsToRoom != 0) {
                    roomChokeRate = roomChokeRates[rInfo] / (float)goldenRunsToRoom;
                }

                sb.AppendLine(
                    $"{cpInfo.Abbreviation}-{roomNumber},{roomChokeRate},{goldenRunsToRoom},{roomChokeRates[rInfo]}"
                );
            }
        }

        sb.AppendLine($"");

        sb.AppendLine($"- Golden Chance"); //Checkpoint->Chapter
        foreach (var cpInfo in info.Checkpoints) {
            var cpPercentStr = cpInfo.GoldenChance.ToString(
                "P2",
                CultureInfo.InvariantCulture
            );
            sb.AppendLine($"{cpInfo.Name}: {cpPercentStr}");
        }
        cPercentStr = chapterGoldenChance.ToString("P2", CultureInfo.InvariantCulture);
        sb.AppendLine($"Total Golden Chance: {cPercentStr}");

        sb.AppendLine("");

        StringBuilder sbGoldenRun = new();
        sbGoldenRun.AppendLine($"Room #,Room Name,Start->Room,Room->End");
        var roomIndexNumber = 0;

        sb.AppendLine("- Golden Chance Over A Run"); //Room-wise from start to room / room to end
        foreach (var cpInfo in info.Checkpoints) {
            foreach (var rInfo in cpInfo.Rooms) {
                roomIndexNumber++;
                if (!Rooms.ContainsKey(rInfo.DebugRoomName)) {
                    var gcToPercentI = 0.ToString("P2", CultureInfo.InvariantCulture);
                    var gcFromPercentI = 0.ToString("P2", CultureInfo.InvariantCulture);
                    sb.AppendLine($"\t{cpInfo.Abbreviation}-{_unvisitedRoomsToRoomNumber[rInfo.DebugRoomName]}:\tStart -> Room: '{gcToPercentI}',\tRoom -> End '{gcFromPercentI}'");
                    sbGoldenRun.AppendLine($"{roomIndexNumber},{cpInfo.Abbreviation}-{_unvisitedRoomsToRoomNumber[rInfo.DebugRoomName]},{0},{0}");
                    continue;
                }

                var rStats = Rooms[rInfo.DebugRoomName];

                double gcToRoom = 1;
                double gcFromRoom = 1;

                var hasReachedRoom = false;
                foreach (var innerRInfo in info.Checkpoints.SelectMany(innerCpInfo => innerCpInfo.Rooms)) {
                    if (innerRInfo.DebugRoomName == rInfo.DebugRoomName)
                        hasReachedRoom = true;

                    if (!Rooms.ContainsKey(innerRInfo.DebugRoomName)) {
                        if (hasReachedRoom) {
                            gcFromRoom *= 0;
                        } else {
                            gcToRoom *= 0;
                        }
                    } else {
                        var innerRStats = Rooms[innerRInfo.DebugRoomName];
                        if (hasReachedRoom) {
                            gcFromRoom *= innerRStats.AverageSuccessOverN(attemptCount);
                        } else {
                            gcToRoom *= innerRStats.AverageSuccessOverN(attemptCount);
                        }
                    }
                }

                var gcToPercent = gcToRoom.ToString("P2", CultureInfo.InvariantCulture);
                var gcFromPercent = gcFromRoom.ToString("P2", CultureInfo.InvariantCulture);
                sb.AppendLine($"\t{cpInfo.Abbreviation}-{rStats.RoomNumber}:\tStart -> Room: '{gcToPercent}',\tRoom -> End '{gcFromPercent}'");
                sbGoldenRun.AppendLine($"{roomIndexNumber},{cpInfo.Abbreviation}-{rStats.RoomNumber},{gcToRoom},{gcFromRoom}");
            }
        }
        sb.AppendLine("- Golden Chance Over A Run (Google Sheets pastable values)"); //Room-wise from start to room / room to end
        sb.AppendLine(sbGoldenRun.ToString());

        sb.AppendLine("");

        File.WriteAllText(outPath, sb.ToString());
    }

    /*
     Format for Sankey Diagram (https://sankeymatic.com/build/)
0m [481] Death //cp1
0m [508] 500m //totalDeaths - cp1 + 1
500m [172] Death //cp2
500m [336] 1000m //totalDeaths - (cp1+cp2) + 1
1000m [136] Death //...
1000m [200] 1500m
1500m [65] Death
1500m [135] 2000m
2000m [48] Death
2000m [87] 2500m
2500m [41] Death
2500m [46] 3000m
3000m [45] Death
3000m [1] Golden Berry
     */
}