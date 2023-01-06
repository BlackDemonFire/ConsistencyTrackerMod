using System;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.Models;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.ConsistencyTracker.Entities {

    internal class RoomRectangle : Component {
        public readonly CheckpointInfo CheckpointInfo;
        public readonly RoomInfo RoomInfo;
        public readonly RoomStats RoomStats;

        public int Offset;
        public float Length;

        public void TweenToLength(int targetLength, bool force = false) {
            if (force) {
                Length = targetLength;
                _tweenTimeRemaining = 0;
            } else {
                _tweenFrom = Length;
                _tweenTo = targetLength;
                _tweenTimeRemaining = TweenTime;
            }
        }

        private const float TweenTime = 0.2f;
        private float _tweenTimeRemaining;
        private float _tweenFrom;
        private float _tweenTo;

        public RoomRectangle(
            CheckpointInfo checkpointInfo,
            RoomInfo roomInfo,
            RoomStats roomStats
        ) : base(true, true) {
            CheckpointInfo = checkpointInfo;
            RoomInfo = roomInfo;
            RoomStats = roomStats;
        }

        public override void Update() {
            base.Update();

            if (!(_tweenTimeRemaining > 0))
                return;
            _tweenTimeRemaining -= Engine.RawDeltaTime;
            Length = Calc.LerpClamp(_tweenFrom, _tweenTo, 1 - _tweenTimeRemaining / TweenTime);
        }

        public override void Render() {
            base.Render();

            var overlayPosition = ConsistencyTrackerModule.Instance.ModSettings.OverlayPosition;
            var horizontal = overlayPosition.IsHorizontal();
            var rounded = (int)Length;
            var perp = Math.Min((int)(rounded / RoomOverlay.roomAspectRatio), 123);
            Vector2 overlayPositionChange;
            switch (overlayPosition) {
                case OverlayPosition.Top:
                    overlayPositionChange = new Vector2(Offset, 0);
                    break;
                case OverlayPosition.Bottom:
                    overlayPositionChange = new Vector2(Offset, -perp);
                    break;
                case OverlayPosition.Left:
                    overlayPositionChange = new Vector2(0, Offset);
                    break;
                case OverlayPosition.Right:
                    overlayPositionChange = new Vector2(-perp, Offset);
                    break;
                default:
                    overlayPositionChange = Vector2.Zero;
                    break;
            }
            var position =
                Entity.Position
                + overlayPositionChange; 

            var color = Color.White;
            if (RoomStats?.PreviousAttempts.Any() == true) {
                var lastFive = RoomStats.LastFiveRate;
                color =
                    lastFive <= 0.33f
                        ? Color.Red
                        : lastFive <= 0.66f
                            ? Color.Yellow
                            : Color.Green;
            }

            var overlayAlpha =
                ConsistencyTrackerModule.Instance.ModSettings.OverlayOpacity * 0.1f;

            var size = new Vector2(horizontal ? rounded : perp, horizontal ? perp : rounded);
            Draw.Rect(position, size.X, size.Y, color * overlayAlpha);

            if (
                RoomStats?.DebugRoomName
                == ConsistencyTrackerModule
                    .Instance
                    .CurrentChapterStats
                    .CurrentRoom
                    .DebugRoomName
            ) {
                Draw.HollowRect(position, size.X, size.Y, Color.White * overlayAlpha);
            }
        }
    }
}