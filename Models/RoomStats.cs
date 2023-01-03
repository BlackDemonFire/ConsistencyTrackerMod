using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.ConsistencyTracker.Models;

public class RoomStats {
    public string DebugRoomName { get; set; }
    public int GoldenBerryDeaths { get; set; }
    public int GoldenBerryDeathsSession { get; set; }
    public List<bool> PreviousAttempts { get; set; } = new();
    public bool IsUnplayed => PreviousAttempts.Count == 0;
    public bool LastAttempt => PreviousAttempts[PreviousAttempts.Count - 1];
    public float LastFiveRate => AverageSuccessOverN(5);
    public float LastTenRate => AverageSuccessOverN(10);
    public float LastTwentyRate => AverageSuccessOverN(20);

    public float MaxRate => AverageSuccessOverN(ChapterStats.MaxAttemptCount);

    public int SuccessStreak {
        get {
            if (PreviousAttempts.Count == 0)
                return 0;

            var count = 0;
            var success = PreviousAttempts[PreviousAttempts.Count - 1];
            while (success) {
                count++;
                if (PreviousAttempts.Count == count)
                    return count;
                success = PreviousAttempts[PreviousAttempts.Count - (1 + count)];
            }

            return count;
        }
    }

    public int DeathsInCurrentRun { get; set; } = 0;

    public int RoomNumber { get; set; }

    public float AverageSuccessOverN(int n) {
        var countSucceeded = 0;
        var countTotal = 0;

        for (var i = 0; i < n; i++) {
            var neededIndex = PreviousAttempts.Count - 1 - i;
            if (neededIndex < 0)
                break;

            countTotal++;
            if (PreviousAttempts[neededIndex])
                countSucceeded++;
        }

        if (countTotal == 0)
            return 0;

        return (float)countSucceeded / countTotal;
    }

    public float AverageSuccessOverSelectedN() {
        var attemptCount = ConsistencyTrackerModule
            .Instance
            .ModSettings
            .SummarySelectedAttemptCount;
        return AverageSuccessOverN(attemptCount);
    }

    public int SuccessesOverN(int n) {
        var countSucceeded = 0;
        for (var i = 0; i < n; i++) {
            var neededIndex = PreviousAttempts.Count - 1 - i;
            if (neededIndex < 0)
                break;
            if (PreviousAttempts[neededIndex])
                countSucceeded++;
        }
        return countSucceeded;
    }

    public int SuccessesOverSelectedN() {
        var attemptCount = ConsistencyTrackerModule
            .Instance
            .ModSettings
            .SummarySelectedAttemptCount;
        return SuccessesOverN(attemptCount);
    }

    public int AttemptsOverN(int n) {
        var countTotal = 0;
        for (var i = 0; i < n; i++) {
            var neededIndex = PreviousAttempts.Count - 1 - i;
            if (neededIndex < 0)
                break;
            countTotal++;
        }
        return countTotal;
    }

    public int AttemptsOverSelectedN() {
        var attemptCount = ConsistencyTrackerModule
            .Instance
            .ModSettings
            .SummarySelectedAttemptCount;
        return AttemptsOverN(attemptCount);
    }

    public void AddAttempt(bool success) {
        if (PreviousAttempts.Count >= ChapterStats.MaxAttemptCount) {
            PreviousAttempts.RemoveAt(0);
        }

        PreviousAttempts.Add(success);
    }

    public void RemoveLastAttempt() {
        if (PreviousAttempts.Count <= 0) {
            return;
        }
        PreviousAttempts.RemoveAt(PreviousAttempts.Count - 1);
    }

    public override string ToString() {
        var attemptList = string.Join(",", PreviousAttempts);
        return $"{DebugRoomName};{GoldenBerryDeaths};{GoldenBerryDeathsSession};{LastFiveRate};{LastTenRate};{LastTwentyRate};{MaxRate};{attemptList}";
    }

    public static RoomStats ParseString(string line) {
        //ChapterStats.LogCallback($"RoomStats -> Parsing line '{line}'");

        var lines = line.Split(new[] { ";" }, StringSplitOptions.None).ToList();
        //ChapterStats.LogCallback($"\tlines.Count = {lines.Count}");
        var name = lines[0];
        var gbDeaths = 0;
        var gbDeathsSession = 0;
        string attemptListString;

        try {
            attemptListString = lines[7];
            gbDeaths = int.Parse(lines[1]);
            gbDeathsSession = int.Parse(lines[2]);
        } catch (Exception) {
            try {
                attemptListString = lines[6];
                gbDeaths = int.Parse(lines[1]);
            } catch (Exception) {
                attemptListString = lines[5];
            }
        }

        //if (name == "a-01") {
        //    ChapterStats.LogCallback($"RoomStats.ParseString -> 'a-01': GB Deaths Session: {gbDeathsSession}");
        //}

        //int gbDeaths = int.Parse(lines[1]);
        //string attemptListString = lines[6];

        var attemptList = (from boolStr in attemptListString.Split(',') where boolStr.Trim() != "" select bool.Parse(boolStr)).ToList();

        return new RoomStats() {
            DebugRoomName = name,
            PreviousAttempts = attemptList,
            GoldenBerryDeaths = gbDeaths,
            GoldenBerryDeathsSession = gbDeathsSession,
        };
    }
}