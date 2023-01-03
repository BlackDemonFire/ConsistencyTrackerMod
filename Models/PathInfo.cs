using Celeste.Mod.ConsistencyTracker.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.ConsistencyTracker.Models;

[Serializable]
public class PathInfo {
    public List<CheckpointInfo> Checkpoints { get; set; } = new List<CheckpointInfo>();
    public int RoomCount {
        get { return Checkpoints.Sum((cpInfo) => cpInfo.Rooms.Count); }
    }
    public AggregateStats Stats { get; set; } = null;
    public RoomInfo CurrentRoom { get; set; } = null;

    public string ParseError { get; set; }

    public static PathInfo GetTestPathInfo() {
        return new PathInfo() {
            Checkpoints = new List<CheckpointInfo>()
            {
                new() { Name = "Start", Abbreviation = "0M" },
                new() { Name = "500 M", Abbreviation = "500M" },
            },
        };
    }

    public RoomInfo FindRoom(RoomStats roomStats) {
        return FindRoom(roomStats.DebugRoomName);
    }
    public RoomInfo FindRoom(string roomName)
    {
        return Checkpoints.Select(cpInfo => cpInfo.Rooms.Find((r) => r.DebugRoomName == roomName)).FirstOrDefault(rInfo => rInfo != null);
    }

    public override string ToString() {
        var lines = Checkpoints.Select(cpInfo => cpInfo.ToString()).ToList();

        return string.Join("\n", lines);
    }

    public static PathInfo ParseString(string content) {
        Logging.Log($"Parsing path info string");
        var lines = content
            .Trim()
            .Split(new[] { "\n" }, StringSplitOptions.None)
            .ToList();

        var pathInfo = new PathInfo();

        foreach (var line in lines) {
            Logging.Log($"\tParsing line '{line}'");
            pathInfo.Checkpoints.Add(CheckpointInfo.ParseString(line));
        }

        return pathInfo;
    }
}

[Serializable]
public class RoomInfo {
    public CheckpointInfo Checkpoint;

    public string DebugRoomName { get; set; }

    public override string ToString() {
        return DebugRoomName;
    }

    public int RoomNumberInCP { get; set; } = -1;
    public int RoomNumberInChapter { get; set; } = -1;

    public string GetFormattedRoomName(RoomNameDisplayType format)
    {
        return format switch
        {
            RoomNameDisplayType.AbbreviationAndRoomNumberInCP => $"{Checkpoint.Abbreviation}-{RoomNumberInCP}",
            RoomNameDisplayType.FullNameAndRoomNumberInCP => $"{Checkpoint.Name}-{RoomNumberInCP}",
            RoomNameDisplayType.DebugRoomName => DebugRoomName,
            _ => DebugRoomName
        };
    }
}