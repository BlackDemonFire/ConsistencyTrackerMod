﻿using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.Models;

namespace Celeste.Mod.ConsistencyTracker.Stats;

/*
 Stats to implement:
 {room:successRate} - Success Rate of the current room
 {checkpoint:successRate} - Average Success Rate of current checkpoint
 {chapter:successRate} - Average Success Rate of the entire chapter

 {room:successes} - Count of successful room clears within the last X attempts
 {room:failures} - Count of deaths trying to clear the room within the last X attempts
 {room:attempts} - Count of at max. X last attempts

     */

public class SuccessRateStat : Stat {

    public static string RoomSuccessRate = "{room:successRate}";
    public static string CheckpointSuccessRate = "{checkpoint:successRate}";
    public static string ChapterSuccessRate = "{chapter:successRate}";
    public static string RoomSuccesses = "{room:successes}";
    public static string RoomFailures = "{room:failures}";
    public static string RoomAttempts = "{room:attempts}";
    public static List<string> IDs = new() {
        RoomSuccessRate, CheckpointSuccessRate, ChapterSuccessRate,
        RoomSuccesses, RoomFailures, RoomAttempts
    };

    public SuccessRateStat() : base(IDs) { }

    public override string FormatStat(
        PathInfo chapterPath,
        ChapterStats chapterStats,
        string format
    ) {
        var attemptCount = StatManager.AttemptCount;

        format = format.Replace(RoomSuccessRate, $"{StatManager.FormatPercentage(chapterStats.CurrentRoom.AverageSuccessOverN(attemptCount))}");

        var successes = chapterStats.CurrentRoom.SuccessesOverN(attemptCount);
        var attempts = chapterStats.CurrentRoom.AttemptsOverN(attemptCount);
        var failures = attempts - successes;

        format = format.Replace(RoomSuccesses, $"{successes}");
        format = format.Replace(RoomFailures, $"{failures}");
        format = format.Replace(RoomAttempts, $"{attempts}");


        if (chapterPath == null) {
            format = StatManager.MissingPathFormat(format, CheckpointSuccessRate);
            format = StatManager.MissingPathFormat(format, ChapterSuccessRate);
            return format;
        }

        format = format.Replace(ChapterSuccessRate, $"{StatManager.FormatPercentage(chapterPath.Stats.SuccessRate)}");

        var foundRoom = false;

        //Walk the path
        foreach (var cpInfo in chapterPath.Checkpoints.Where(cpInfo => cpInfo.Rooms.Any(rInfo => rInfo.DebugRoomName == chapterStats.CurrentRoom.DebugRoomName))) {
            format = format.Replace(CheckpointSuccessRate, $"{StatManager.FormatPercentage(cpInfo.Stats.SuccessRate)}");
            foundRoom = true;
        }

        if (!foundRoom) {
            format = StatManager.NotOnPathFormatPercent(format, CheckpointSuccessRate);
        }

        return format;
    }

    public override string FormatSummary(PathInfo chapterPath, ChapterStats chapterStats) {
        return null;
    }

    //success-rate;Room SR: {room:successRate} | CP: {checkpoint:successRate} | Total: {chapter:successRate}
    public override List<KeyValuePair<string, string>> GetPlaceholderExplanations() {
        return new List<KeyValuePair<string, string>>() {
            new(RoomSuccessRate, "Current room's success rate over last X attempts (count of successful attempts / total attempts)"),
            new(CheckpointSuccessRate, "Current checkpoint's success rate"),
            new(ChapterSuccessRate, "The chapter's success rate"),

            new(RoomSuccesses, "Count of successful room clears within the last X attempts (X is the attempt count configured in Mod Options)"),
            new(RoomFailures, "Count of deaths trying to clear the room within the last X attempts"),
            new(RoomAttempts, "Count of at max. X last attempts"),
        };
    }
    public override List<StatFormat> GetStatExamples() {
        return new List<StatFormat>() {
            new("success-rate", $"Room SR: {RoomSuccessRate} ({RoomSuccesses}/{RoomAttempts}) | CP: {CheckpointSuccessRate} | Total: {ChapterSuccessRate}")
        };
    }
}