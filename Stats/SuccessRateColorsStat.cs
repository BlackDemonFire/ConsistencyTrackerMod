﻿using System.Collections.Generic;
using Celeste.Mod.ConsistencyTracker.Models;

namespace Celeste.Mod.ConsistencyTracker.Stats;
/*
    Red: {chapter:color-red}
    Yellow: {chapter:color-yellow}
    Green: {chapter:color-green}
    Light-Green: {chapter:color-lightGreen}

     */

public class SuccessRateColorsStat : Stat {
    public static string ColorRed = "{chapter:color-red}";
    public static string ColorYellow = "{chapter:color-yellow}";
    public static string ColorGreen = "{chapter:color-green}";
    public static string ColorLightGreen = "{chapter:color-lightGreen}";

    public static string CheckpointColorRed = "{checkpoint:color-red}";
    public static string CheckpointColorYellow = "{checkpoint:color-yellow}";
    public static string CheckpointColorGreen = "{checkpoint:color-green}";
    public static string CheckpointColorLightGreen = "{checkpoint:color-lightGreen}";

    public static string ChapterListColorRed = "{chapter:listColor-red}";
    public static string CheckpointListColorRed = "{checkpoint:listColor-red}";

    public static List<string> IDs = new() {
        ColorRed, ColorYellow, ColorGreen, ColorLightGreen,
        CheckpointColorRed, CheckpointColorYellow, CheckpointColorGreen, CheckpointColorLightGreen,
        ChapterListColorRed, CheckpointListColorRed,
    };

    public SuccessRateColorsStat() : base(IDs) { }

    public override string FormatStat(PathInfo chapterPath, ChapterStats chapterStats, string format) {
        if (chapterPath == null) {
            format = StatManager.MissingPathFormat(format, ColorRed);
            format = StatManager.MissingPathFormat(format, ColorYellow);
            format = StatManager.MissingPathFormat(format, ColorGreen);
            format = StatManager.MissingPathFormat(format, ColorLightGreen);

            format = StatManager.MissingPathFormat(format, CheckpointColorRed);
            format = StatManager.MissingPathFormat(format, CheckpointColorYellow);
            format = StatManager.MissingPathFormat(format, CheckpointColorGreen);
            format = StatManager.MissingPathFormat(format, CheckpointColorLightGreen);

            format = StatManager.MissingPathFormat(format, ChapterListColorRed);
            format = StatManager.MissingPathFormat(format, CheckpointListColorRed);
            return format;
        }

        //Light Green, Green, Yellow, Red
        var colorCounts = new[] { 0, 0, 0, 0 };
        var colorCountsCP = new[] { 0, 0, 0, 0 };

        var listColorRedChapter = new List<string>();
        var listColorRedCheckpoint = new List<string>();

        //Walk path
        var foundRoom = false;
        foreach (var cpInfo in chapterPath.Checkpoints) {
            var tempColorCountsCp = new[] { 0, 0, 0, 0 };
            var tempColorListCp = new List<string>();

            foreach (var rInfo in cpInfo.Rooms) {
                var rStats = chapterStats.GetRoom(rInfo.DebugRoomName);

                var successRate = rStats.AverageSuccessOverSelectedN();

                if (successRate >= 0.949999) {
                    colorCounts[0]++;
                    tempColorCountsCp[0]++;

                } else if (successRate >= 0.8) {
                    colorCounts[1]++;
                    tempColorCountsCp[1]++;

                } else if (successRate >= 0.5) {
                    colorCounts[2]++;
                    tempColorCountsCp[2]++;

                } else {
                    colorCounts[3]++;
                    tempColorCountsCp[3]++;
                    listColorRedChapter.Add(StatManager.GetFormattedRoomName(rInfo));
                    tempColorListCp.Add(StatManager.GetFormattedRoomName(rInfo));
                }

                if (rInfo.DebugRoomName == chapterStats.CurrentRoom.DebugRoomName) {
                    foundRoom = true;
                }
            }

            if (!foundRoom)
                continue;
            foundRoom = false;
            colorCountsCP = tempColorCountsCp;
            listColorRedCheckpoint = tempColorListCp;
        }

        format = format.Replace(ColorLightGreen, $"{colorCounts[0]}");
        format = format.Replace(ColorGreen, $"{colorCounts[1]}");
        format = format.Replace(ColorYellow, $"{colorCounts[2]}");
        format = format.Replace(ColorRed, $"{colorCounts[3]}");

        if (chapterPath.CurrentRoom == null) {
            format = StatManager.NotOnPathFormat(format, CheckpointColorLightGreen);
            format = StatManager.NotOnPathFormat(format, CheckpointColorGreen);
            format = StatManager.NotOnPathFormat(format, CheckpointColorYellow);
            format = StatManager.NotOnPathFormat(format, CheckpointColorRed);

            format = StatManager.NotOnPathFormat(format, CheckpointListColorRed);
        } else {
            format = format.Replace(CheckpointColorLightGreen, $"{colorCountsCP[0]}");
            format = format.Replace(CheckpointColorGreen, $"{colorCountsCP[1]}");
            format = format.Replace(CheckpointColorYellow, $"{colorCountsCP[2]}");
            format = format.Replace(CheckpointColorRed, $"{colorCountsCP[3]}");

            format = format.Replace(CheckpointListColorRed, $"{string.Join(", ", listColorRedCheckpoint)}");
        }

        format = format.Replace(ChapterListColorRed, $"{string.Join(", ", listColorRedChapter)}");

        return format;
    }

    public override string FormatSummary(PathInfo chapterPath, ChapterStats chapterStats) {
        return null;
    }

    //color-tracker;Reds: {chapter:color-red}, Yellows: {chapter:color-yellow}, Greens: {chapter:color-green}, Light-Greens: {chapter:color-lightGreen}
    public override List<KeyValuePair<string, string>> GetPlaceholderExplanations() {
        return new List<KeyValuePair<string, string>>() {
            new(ColorRed, "Count of red rooms (success rate <50%)"),
            new(ColorYellow, "Count of yellow rooms (success rate 50%-80%)"),
            new(ColorGreen, "Count of green rooms (success rate 80%-95%)"),
            new(ColorLightGreen, "Count of light green rooms (success rate 95%-100%)"),

            new(CheckpointColorRed, "Count of red rooms in the current checkpoint"),
            new(CheckpointColorYellow, "Count of yellow rooms in the current checkpoint"),
            new(CheckpointColorGreen, "Count of green rooms in the current checkpoint"),
            new(CheckpointColorLightGreen, "Count of light green rooms in the current checkpoint"),

            new(ChapterListColorRed, "Lists all red rooms in the chapter by name"),
            new(CheckpointListColorRed, "Lists all red rooms in the current checkpoint by name"),
        };
    }
    public override List<StatFormat> GetStatExamples() {
        return new List<StatFormat>() {
            new("color-tracker", $"Reds: {ColorRed}, Yellows: {ColorYellow}, Greens: {ColorGreen}, Light-Greens: {ColorLightGreen}"),
            new("color-tracker-cp", $"Checkpoint: Reds: {CheckpointColorRed}, Yellows: {CheckpointColorYellow}, Greens: {CheckpointColorGreen}, Light-Greens: {CheckpointColorLightGreen}"),
            new("red-rooms-list", $"All red rooms: {ChapterListColorRed}\\nIn CP: {CheckpointListColorRed}")
        };
    }
}