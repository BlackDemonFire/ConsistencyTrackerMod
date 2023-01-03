﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Celeste.Mod.ConsistencyTracker.Models;

namespace Celeste.Mod.ConsistencyTracker.Stats;

/*
 Stats to implement:
 {chapter:averageRunDistance} - Average run distance over all runs ever
 {chapter:averageRunDistanceSession} - Average run distance over all runs of the current session
 
 {chapter:averageRunDistanceSession#x} - Average run distance over the last X runs of the current session
 {chapter:lastRunDistance#x} - Distance of the last #x run

     */

public class AverageLastRunsStat : Stat {
    public static string ChapterAverageRunDistance = "{chapter:averageRunDistance}";
    public static string ChapterAverageRunDistanceSession = "{chapter:averageRunDistanceSession}";

    public static string ChapterAverageRunDistanceSessionOverX = @"\{chapter:averageRunDistanceSession#(.*?)\}";
    public static string ChapterLastRunDistanceOverX = @"\{chapter:lastRunDistance#(.*?)\}";

    public static List<string> IDs = new() { ChapterAverageRunDistance, ChapterAverageRunDistanceSession };

    public AverageLastRunsStat() : base(IDs) { }

    public override bool ContainsIdentificator(string format) {
        if (format.Contains(ChapterAverageRunDistance) || format.Contains(ChapterAverageRunDistanceSession))
            return true;

        return Regex.IsMatch(format, ChapterAverageRunDistanceSessionOverX) || Regex.IsMatch(format, ChapterLastRunDistanceOverX);
    }

    public override string FormatStat(PathInfo chapterPath, ChapterStats chapterStats, string format) {
        if (chapterPath == null) { //Player doesn't have path
            format = StatManager.MissingPathFormat(format, ChapterAverageRunDistance);
            format = StatManager.MissingPathFormat(format, ChapterAverageRunDistanceSession);

            format = Regex.Replace(format, ChapterAverageRunDistanceSessionOverX, StatManager.MissingPathOutput);
            format = Regex.Replace(format, ChapterLastRunDistanceOverX, StatManager.MissingPathOutput);
            return format;
        }

        double averageRunDistance = 0;
        var countRunsTotal = 0;

        double averageRunDistanceSession = 0;
        var countRunsTotalSession = 0;

        foreach (var rStats in chapterStats.Rooms.Values) {
            var rInfo = chapterPath.FindRoom(rStats);
            if (rInfo == null) //rStats room is not on the path
                continue;

            countRunsTotal += rStats.GoldenBerryDeaths;
            averageRunDistance += rInfo.RoomNumberInChapter * rStats.GoldenBerryDeaths;

            countRunsTotalSession += rStats.GoldenBerryDeathsSession;
            averageRunDistanceSession += rInfo.RoomNumberInChapter * rStats.GoldenBerryDeathsSession;
        }

        if (countRunsTotal > 0)
            averageRunDistance /= countRunsTotal;
        if (countRunsTotalSession > 0)
            averageRunDistanceSession /= countRunsTotalSession;




        var invalidFormats = new Dictionary<string, string>();
        var runCountsToFormatSession = new Dictionary<int, string>();
        var lastRunNumbersToFormat = new Dictionary<int, string>();

        // ========= REGEX PART ==========
        var matches = Regex.Matches(format, ChapterAverageRunDistanceSessionOverX);
        foreach (Match match in matches) {
            for (var i = 1; i < match.Groups.Count; i++) {
                var runCountStr = match.Groups[i].Value;
                try {
                    var runCountInt = int.Parse(runCountStr);
                    if (runCountInt < 1)
                        throw new ArgumentException();

                    if (!runCountsToFormatSession.ContainsKey(runCountInt))
                        runCountsToFormatSession.Add(runCountInt, null);

                } catch (FormatException) {
                    if (invalidFormats.ContainsKey($"{{chapter:averageRunDistanceSession#{match.Groups[i].Value}}}"))
                        continue;
                    invalidFormats.Add($"{{chapter:averageRunDistanceSession#{match.Groups[i].Value}}}", $"<Invalid run count value: {match.Groups[i].Value}>");
                } catch (ArgumentException) {
                    if (invalidFormats.ContainsKey($"{{chapter:averageRunDistanceSession#{match.Groups[i].Value}}}"))
                        continue;
                    invalidFormats.Add($"{{chapter:averageRunDistanceSession#{match.Groups[i].Value}}}", $"<Run count value must be 1 or greater: {match.Groups[i].Value}>");
                } catch (Exception) {
                    if (invalidFormats.ContainsKey($"{{chapter:averageRunDistanceSession#{match.Groups[i].Value}}}"))
                        continue;
                    invalidFormats.Add($"{{chapter:averageRunDistanceSession#{match.Groups[i].Value}}}", $"<Invalid run count value: {match.Groups[i].Value}>");
                }
            }
        }


        matches = Regex.Matches(format, ChapterLastRunDistanceOverX);
        foreach (Match match in matches) {
            for (var i = 1; i < match.Groups.Count; i++) {
                var runNrStr = match.Groups[i].Value;
                try {
                    var runNrInt = int.Parse(runNrStr);
                    if (runNrInt < 1)
                        throw new ArgumentException();

                    if (!lastRunNumbersToFormat.ContainsKey(runNrInt))
                        lastRunNumbersToFormat.Add(runNrInt, null);

                } catch (FormatException) {
                    if (invalidFormats.ContainsKey($"{{chapter:lastRunDistance#{match.Groups[i].Value}}}"))
                        continue;
                    invalidFormats.Add($"{{chapter:lastRunDistance#{match.Groups[i].Value}}}", $"<Invalid run count value: {match.Groups[i].Value}>");
                } catch (ArgumentException) {
                    if (invalidFormats.ContainsKey($"{{chapter:lastRunDistance#{match.Groups[i].Value}}}"))
                        continue;
                    invalidFormats.Add($"{{chapter:lastRunDistance#{match.Groups[i].Value}}}", $"<Run count value must be 1 or greater: {match.Groups[i].Value}>");
                } catch (Exception) {
                    if (invalidFormats.ContainsKey($"{{chapter:lastRunDistance#{match.Groups[i].Value}}}"))
                        continue;
                    invalidFormats.Add($"{{chapter:lastRunDistance#{match.Groups[i].Value}}}", $"<Invalid run count value: {match.Groups[i].Value}>");
                }
            }
        }

        // ===========================================

        double averageRunDistanceLastXSession = 0;
        var runCountLastXSession = 0;
        for (var i = chapterStats.CurrentChapterLastGoldenRuns.Count - 1; i >= 0; i--) {
            var rStats = chapterStats.CurrentChapterLastGoldenRuns[i];

            var rInfo = chapterPath.FindRoom(rStats);
            if (rInfo == null)
                continue;

            averageRunDistanceLastXSession += rInfo.RoomNumberInChapter;
            runCountLastXSession++;

            if (runCountsToFormatSession.ContainsKey(runCountLastXSession)) {
                var avg = averageRunDistanceLastXSession / runCountLastXSession;
                runCountsToFormatSession[runCountLastXSession] = $"{StatManager.FormatDouble(avg)}";
            }

            if (lastRunNumbersToFormat.ContainsKey(runCountLastXSession)) {
                lastRunNumbersToFormat[runCountLastXSession] = $"{rInfo.RoomNumberInChapter}";
            }
        }


        var leftOverAvg = averageRunDistanceLastXSession / runCountLastXSession;

        var toSet = (from nr in runCountsToFormatSession.Keys let formatted = runCountsToFormatSession[nr] where formatted == null select nr).ToDictionary(nr => nr, nr => $"{StatManager.FormatDouble(leftOverAvg)}");

        foreach (var kv in toSet) {
            runCountsToFormatSession[kv.Key] = kv.Value;
        }


        // ========================================

        //Output requested runs
        //Average run distance
        foreach (var nr in runCountsToFormatSession.Keys) {
            var formatted = runCountsToFormatSession[nr];
            formatted ??= "-";

            format = format.Replace($"{{chapter:averageRunDistanceSession#{nr}}}", formatted);
        }
        foreach (var nr in lastRunNumbersToFormat.Keys) {
            var formatted = lastRunNumbersToFormat[nr];
            formatted ??= "-";

            format = format.Replace($"{{chapter:lastRunDistance#{nr}}}", formatted);
        }

        format = format.Replace(ChapterAverageRunDistance, $"{StatManager.FormatDouble(averageRunDistance)}");
        format = format.Replace(ChapterAverageRunDistanceSession, $"{StatManager.FormatDouble(averageRunDistanceSession)}");

        return format;
    }

    public override string FormatSummary(PathInfo chapterPath, ChapterStats chapterStats) {
        return null;
    }


    //success-rate;Room SR: {room:successRate} | CP: {checkpoint:successRate} | Total: {chapter:successRate} 
    public override List<KeyValuePair<string, string>> GetPlaceholderExplanations() {
        return new List<KeyValuePair<string, string>>() {
            new(ChapterAverageRunDistance, "Average run distance over all runs ever"),
            new(ChapterAverageRunDistanceSession, "Average run distance over all runs of the current session"),
            new("{chapter:averageRunDistanceSession#X}", "Average run distance over the last X runs of the current session"),
            new("{chapter:lastRunDistance#X}", "The room number of last run nr. X (e.g. {chapter:lastRunDistance#1} would give you the room number of the most recent golden run, #2 of the 2nd most recent, ...)"),
        };
    }
    public override List<StatFormat> GetStatExamples() {
        return new List<StatFormat>() {
            new("avg-run-distance", $"Avg. run distance: {ChapterAverageRunDistance}/{LiveProgressStat.ChapterRoomCount}" +
                                    $"\nAvg. run distance: {ChapterAverageRunDistanceSession}/{LiveProgressStat.ChapterRoomCount}" +
                                    $"\nAvg. over last 10 runs: {{chapter:averageRunDistanceSession#10}}/{LiveProgressStat.ChapterRoomCount}" +
                                    "\n\nLast runs (most recent run first):" +
                                    $"\n{{chapter:lastRunDistance#1}}/{LiveProgressStat.ChapterRoomCount}" +
                                    $"\n{{chapter:lastRunDistance#2}}/{LiveProgressStat.ChapterRoomCount}" +
                                    $"\n{{chapter:lastRunDistance#3}}/{LiveProgressStat.ChapterRoomCount}" +
                                    $"\n{{chapter:lastRunDistance#4}}/{LiveProgressStat.ChapterRoomCount}" +
                                    $"\n{{chapter:lastRunDistance#5}}/{LiveProgressStat.ChapterRoomCount}")
        };
    }
}
/*
 Avg. run distance: {chapter:averageRunDistance}/{chapter:roomCount}
 Avg. run distance (Session): {chapter:averageRunDistanceSession}/{chapter:roomCount}
 Avg. over last 10 runs: {chapter:averageRunDistanceSession#10}/{chapter:roomCount}
 
    Last runs:
    {chapter:lastRunDistance#1}/{chapter:roomCount}
    {chapter:lastRunDistance#2}/{chapter:roomCount}
    {chapter:lastRunDistance#3}/{chapter:roomCount}
    {chapter:lastRunDistance#4}/{chapter:roomCount}
    {chapter:lastRunDistance#5}/{chapter:roomCount}
     
     */
