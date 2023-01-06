using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.ConsistencyTracker.Models {

    public class PathRecorder {
        public static string DefaultCheckpointName = "Start";

        //Remember all previously visited rooms. Rooms only get added to the first checkpoint they appear in.
        public HashSet<string> VisitedRooms { get; set; } = new HashSet<string>();

        private static readonly List<List<string>> List = new List<List<string>>();

        public List<List<string>> Checkpoints { get; set; } = List;
        public List<string> CheckpointNames = new List<string>();
        public List<string> CheckpointAbbreviations = new List<string>();

        public HashSet<Vector2> CheckpointsVisited { get; set; } = new HashSet<Vector2>();

        public void AddRoom(string name) {
            if (VisitedRooms.Contains(name))
                return;

            VisitedRooms.Add(name);
            Checkpoints.Last().Add(name);
        }

        public void AddCheckpoint(Checkpoint cp, string name) {
            Logging.Log($"In AddCheckpoint: 1 | cp = '{cp}', name = '{name}'");
            if (Checkpoints.Count != 0) {
                Logging.Log("In AddCheckpoint: 2.1");
                if (cp != null && CheckpointsVisited.Contains(cp.Position))
                    return;
                if (cp != null)
                    CheckpointsVisited.Add(cp.Position);


                Logging.Log("In AddCheckpoint: 2.1.1");
                var lastRoom = Checkpoints.Last().Last();
                Checkpoints.Last().Remove(lastRoom);
                Logging.Log("In AddCheckpoint: 2.1.2");
                Checkpoints.Add(new List<string>() { lastRoom });
            } else {
                Logging.Log("In AddCheckpoint: 2.2");
                Checkpoints.Add(new List<string>());
            }

            Logging.Log("In AddCheckpoint: 3");

            if (name == null) {
                Logging.Log("In AddCheckpoint: 4.1");
                CheckpointNames.Add($"CP{Checkpoints.Count}");
                CheckpointAbbreviations.Add($"CP{Checkpoints.Count}");
            } else {
                Logging.Log("In AddCheckpoint: 4.2");
                CheckpointNames.Add(name);
                CheckpointAbbreviations.Add(AbbreviateName(name));
            }
        }

        public PathInfo ToPathInfo() {
            var toRet = new PathInfo();

            var checkpointIndex = 0;
            foreach (var checkpoint in Checkpoints) {
                var cpName = CheckpointNames[checkpointIndex];
                var cpAbbreviation = CheckpointAbbreviations[checkpointIndex];

                if (checkpointIndex == 0) {
                    if (Checkpoints.Count == 1) {
                        cpName = "Room";
                        cpAbbreviation = "R";
                    }
                }

                var cpInfo = new CheckpointInfo() {
                    Name = cpName,
                    Abbreviation = cpAbbreviation,
                };

                foreach (var roomName in checkpoint) {
                    cpInfo.Rooms.Add(new RoomInfo() { DebugRoomName = roomName });
                }

                toRet.Checkpoints.Add(cpInfo);

                checkpointIndex++;
            }

            return toRet;
        }

        public string AbbreviateName(string name, int letterCount = 2) {
            var words = name.Split(' ');

            if (words.Length == 1) {
                return words[0].Substring(0, letterCount).ToUpper();
            } else {
                var abbr = words.Aggregate("", (current, word) => current + word[0]);

                return abbr.ToUpper();
            }
        }
    }
}