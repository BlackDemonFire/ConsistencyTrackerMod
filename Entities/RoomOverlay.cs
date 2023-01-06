using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.Models;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.ConsistencyTracker.Entities {

    public class RoomOverlay : Entity {
        public const float roomAspectRatio = 1.61803f; // golden ratio
        private const float OffsetPixels = 10f; // this should be configurable
        private const float TotalLengthMultiplier = 0.9f;
        private const float MinCheckpointLengthMultiplier = 0.25f;
        private const float MaxCheckpointLengthMultiplier = 0.4f;
        private const float CheckpointScale = 1.5f;
        private const float CurrentRoomScale = 1.3f;
        private const int RoomPaddingPixels = 4;
        private const int CheckpointSeparatorShortPixels = 4;

        private string _previousDebugRoomName;
        private OverlayPosition _previousOverlayPosition;
        private List<RoomRectangle> _roomRectangles;
        private RoomName _roomNameOverlay;
        private int[] _checkpointMarkers;
        private int _checkpointSeparatorLongPixels;

        public RoomOverlay() {
            Logging.Log("Building RoomOverlay Object");
            Depth = -101;
            Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate;

            Visible = false;
        }

        private static bool TryGetInfo(
            string debugRoomName,
            out CheckpointInfo currentCheckpoint,
            out int otherRoomCount
        ) {
            currentCheckpoint = null;
            otherRoomCount = 0;

            foreach (var checkpoint in ConsistencyTrackerModule.Instance.GetPathInputInfo().Checkpoints) {
                var roomIndex = checkpoint.Rooms.FindIndex(r => r.DebugRoomName == debugRoomName);
                if (roomIndex >= 0) {
                    currentCheckpoint = checkpoint;
                } else {
                    otherRoomCount += checkpoint.RoomCount;
                }
            }

            return currentCheckpoint != null;
        }

        public override void Update() {
            _roomNameOverlay = _roomNameOverlay ?? new RoomName();
            // build rooms if we must
            var createRooms = _roomRectangles == null;
            if (
                ConsistencyTrackerModule.Instance.CurrentChapterStats != null
                && ConsistencyTrackerModule.Instance.GetPathInputInfo() != null
                && createRooms
            ) {
                _roomRectangles = new List<RoomRectangle>();
                foreach (var rect in from checkpoint in ConsistencyTrackerModule.Instance.GetPathInputInfo()
                             .Checkpoints
                                     from room in checkpoint.Rooms
                                     let stats = ConsistencyTrackerModule.Instance.CurrentChapterStats.GetRoom(room.DebugRoomName)
                                     select new RoomRectangle(checkpoint, room, stats)) {
                    _roomRectangles.Add(rect);
                    Add(rect);
                }

                _checkpointMarkers = Enumerable
                    .Range(0, ConsistencyTrackerModule.Instance.GetPathInputInfo().Checkpoints.Count)
                    .ToArray();
            }

            switch (Visible) {
                // hide if we're showing and shouldn't be
                case true when ConsistencyTrackerModule.Instance.ModSettings.OverlayPosition ==
                               OverlayPosition.Disabled:
                    Visible = false;
                    break;
                // show if we're not showing and should be
                case false when ConsistencyTrackerModule.Instance.ModSettings.OverlayPosition !=
                                OverlayPosition.Disabled:
                    Visible = true;
                    break;
            }

            // update the room lengths if we must
            UpdateRoomLengths(createRooms);

            // make rooms tween themselves
            base.Update();

            // update the room positions
            UpdateRoomPositions();
            _roomNameOverlay.Update();
            // set the position based on the current settings
            var overlayPosition = ConsistencyTrackerModule.Instance.ModSettings.OverlayPosition;
            switch (overlayPosition) {
                case OverlayPosition.Bottom:
                    Position = new Vector2(Engine.Width / 2f, Engine.Height - OffsetPixels);
                    break;
                case OverlayPosition.Top:
                    Position = new Vector2(Engine.Width / 2f, OffsetPixels);
                    break;
                case OverlayPosition.Left:
                    Position = new Vector2(OffsetPixels, Engine.Height / 2f);
                    break;
                case OverlayPosition.Right:
                    Position = new Vector2(Engine.Width - OffsetPixels, Engine.Height / 2f);
                    break;
                case OverlayPosition.Disabled:
                default:
                    Position = Vector2.Zero;
                    break;
            }
        }

        public override void Render() {
            base.Render();

            var overlayAlpha = ConsistencyTrackerModule.Instance.ModSettings.OverlayOpacity * 0.1f;
            var overlayPosition = ConsistencyTrackerModule.Instance.ModSettings.OverlayPosition;
            var horizontal = overlayPosition.IsHorizontal();
            var size = horizontal
                ? new Vector2(CheckpointSeparatorShortPixels, _checkpointSeparatorLongPixels)
                : new Vector2(_checkpointSeparatorLongPixels, CheckpointSeparatorShortPixels);
            _roomNameOverlay.Render();
            if (_checkpointMarkers == null || _checkpointMarkers.Length == 0) {
                return;
            }

            foreach (var offset in _checkpointMarkers) {
                var position =
                    Position
                    + (overlayPosition == OverlayPosition.Top ? new Vector2(offset, 0) :
                        overlayPosition == OverlayPosition.Bottom ? new Vector2(offset, -size.Y) :
                        overlayPosition == OverlayPosition.Left ? new Vector2(0, offset) :
                        overlayPosition == OverlayPosition.Right ? new Vector2(-size.X, offset) : Vector2.Zero);

                Draw.Rect(position.X, position.Y, size.X, size.Y, Color.White * overlayAlpha);
            }
        }

        private void UpdateRoomLengths(bool force = false) {
            if (_roomRectangles == null)
                return;

            var currentRoomStats = ConsistencyTrackerModule
                .Instance
                .CurrentChapterStats
                .CurrentRoom;
            if (currentRoomStats == null)
                return;

            // if the overlay position has changed, force
            var overlayPosition = ConsistencyTrackerModule.Instance.ModSettings.OverlayPosition;
            if (_previousOverlayPosition != overlayPosition)
                force = true;

            // break if the room hasn't changed and we're not forcing
            if (!force && _previousDebugRoomName == currentRoomStats.DebugRoomName)
                return;

            // try to get info about the path
            var totalRooms = ConsistencyTrackerModule.Instance.GetPathInputInfo().Checkpoints.Sum(
                c => c.RoomCount
            );
            if (
                !TryGetInfo(
                    currentRoomStats.DebugRoomName,
                    out var currentCheckpoint,
                    out var otherRoomCount
                )
            )
                return;

            _previousDebugRoomName = currentRoomStats.DebugRoomName;
            _previousOverlayPosition = overlayPosition;

            // calculate expected lengths
            var horizontal = overlayPosition.IsHorizontal();
            var expectedTotalLength = (float)
                Math.Floor((horizontal ? Engine.Width : Engine.Height) * TotalLengthMultiplier);
            var expectedRoomSize = expectedTotalLength / totalRooms;
            var expectedCheckpointLength = Calc.Clamp(
                expectedRoomSize * currentCheckpoint.RoomCount * CheckpointScale,
                expectedTotalLength * MinCheckpointLengthMultiplier,
                expectedTotalLength * MaxCheckpointLengthMultiplier
            );
            var expectedCheckpointRoomLength =
                expectedCheckpointLength / currentCheckpoint.RoomCount;
            expectedCheckpointLength +=
                expectedCheckpointRoomLength * CurrentRoomScale - expectedCheckpointRoomLength;
            var expectedRemainingLength = expectedTotalLength - expectedCheckpointLength;

            // calculate actual lengths
            var normalRoomStride = (int)(expectedRemainingLength / otherRoomCount);
            var normalRoomLength = normalRoomStride - RoomPaddingPixels;
            var checkpointRoomStride = (int)expectedCheckpointRoomLength;
            var checkpointRoomLength = checkpointRoomStride - RoomPaddingPixels;
            var currentRoomStride = (int)(expectedCheckpointRoomLength * CurrentRoomScale);
            var currentRoomLength = currentRoomStride - RoomPaddingPixels;

            _checkpointSeparatorLongPixels = (int)(checkpointRoomLength / roomAspectRatio);

            // update rooms
            foreach (var roomRectangle in _roomRectangles) {
                if (roomRectangle.RoomInfo.DebugRoomName == currentRoomStats.DebugRoomName) {
                    roomRectangle.TweenToLength(currentRoomLength, force);
                } else if (roomRectangle.CheckpointInfo == currentCheckpoint) {
                    roomRectangle.TweenToLength(checkpointRoomLength, force);
                } else {
                    roomRectangle.TweenToLength(normalRoomLength, force);
                }
            }
        }

        private void UpdateRoomPositions() {
            if (_roomRectangles == null)
                return;

            var offset = 0;
            var checkpointIndex = 0;
            CheckpointInfo checkpoint = null;

            foreach (var roomRectangle in _roomRectangles) {
                if (checkpoint != null && checkpoint != roomRectangle.CheckpointInfo) {
                    _checkpointMarkers[checkpointIndex++] = offset + RoomPaddingPixels;
                    offset += CheckpointSeparatorShortPixels + RoomPaddingPixels * 3;
                }

                roomRectangle.Offset = offset;
                offset += (int)roomRectangle.Length + RoomPaddingPixels;

                checkpoint = roomRectangle.CheckpointInfo;
            }

            var half = (offset - RoomPaddingPixels) / 2;
            foreach (var roomRectangle in _roomRectangles) {
                roomRectangle.Offset -= half;
            }

            for (var i = 0; i < _checkpointMarkers.Length; i++) {
                _checkpointMarkers[i] -= half;
            }
        }
    }
}