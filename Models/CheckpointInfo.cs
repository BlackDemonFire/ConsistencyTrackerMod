using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.ConsistencyTracker.Models {

    [Serializable]
    public class CheckpointInfo {
        public string Name { get; set; }
        public string Abbreviation { get; set; }
        public int RoomCount {
            get => Rooms.Count;
            private set => _ = value;
        }
        public List<RoomInfo> Rooms { get; set; } = new List<RoomInfo>();

        public AggregateStats Stats { get; set; } = null;
        public int CPNumberInChapter { get; set; } = -1;
        public double GoldenChance { get; set; } = 1;

        public override string ToString() {
            var toRet = $"{Name};{Abbreviation};{Rooms.Count}";
            var debugNames = string.Join(",", Rooms);
            return $"{toRet};{debugNames}";
        }

        public static CheckpointInfo ParseString(string line) {
            var parts = line.Trim()
                .Split(new[] { ";" }, StringSplitOptions.None)
                .ToList();
            var name = parts[0];
            var abbreviation = parts[1];

            var rooms = parts[3]
                .Split(new[] { "," }, StringSplitOptions.None)
                .ToList();

            var cpInfo = new CheckpointInfo() {
                Name = name,
                Abbreviation = abbreviation,
            };

            var roomInfos = rooms.Select(room => new RoomInfo() { DebugRoomName = room, Checkpoint = cpInfo }).ToList();

            cpInfo.Rooms = roomInfos;

            return cpInfo;
        }
    }
}