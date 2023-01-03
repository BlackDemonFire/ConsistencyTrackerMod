using Celeste.Mod.ConsistencyTracker.Enums;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.ConsistencyTracker.Entities;

internal class RoomName : Component {
    public RoomName() : base(true, true) {
    }

    public override void Render() {
        base.Render();
        Logging.Log("Rendering the RoomName");
        var chapterStats = ConsistencyTrackerModule.Instance.CurrentChapterPath;
        Logging.Log($"chapterStats={chapterStats}");
        var room = chapterStats?.CurrentRoom;
        if (room == null) {
            Logging.Log("Current room is null and therefor cannot be rendered.");
            return;
        }

        var name = $"{room.GetFormattedRoomName(RoomNameDisplayType.AbbreviationAndRoomNumberInCP)}";
        Logging.Log($"RoomName is {name}");

        const float scale = 2f;
        const float alpha = 1f;
        var font = Dialog.Languages["english"].Font;
        var fontFaceSize = Dialog.Languages["english"].FontFaceSize;
        var color = Color.White * alpha;
        font.DrawOutline(
            fontFaceSize,
            name,
            new Vector2(100, 200),
            new Vector2(),
            Vector2.One * scale,
            color,
            2f,
            Color.Black
        );
    }
}