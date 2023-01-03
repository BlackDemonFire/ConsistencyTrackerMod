using System.Collections.Generic;
using Celeste.Mod.ConsistencyTracker.Enums;
using Celeste.Mod.ConsistencyTracker.Stats;

namespace Celeste.Mod.ConsistencyTracker;

public class ConsistencyTrackerSettings : EverestModuleSettings {
    private bool _pauseDeathTracking;
    public bool Enabled { get; set; } = true;

    public bool PauseDeathTracking {
        get => _pauseDeathTracking;
        set {
            ConsistencyTrackerModule.Instance.SaveChapterStats();
            _pauseDeathTracking = value;
        }
    }


    public bool OnlyTrackWithGoldenBerry { get; set; } = false;
    public void CreateOnlyTrackWithGoldenBerryEntry(TextMenu menu) {
        menu.Add(new TextMenu.OnOff("Only Track Deaths With Golden Berry", OnlyTrackWithGoldenBerry) {
            OnValueChange = v => {
                OnlyTrackWithGoldenBerry = v;
            }
        });
    }

    public bool RecordPath { get; set; } = false;
    public void CreateRecordPathEntry(TextMenu menu, bool inGame) {
        if (!inGame)
            return;

        var subMenu = new TextMenuExt.SubMenu("Path Recording", false);

        subMenu.Add(new TextMenu.SubHeader("!!!Existing paths will be overwritten!!!"));
        subMenu.Add(new TextMenu.OnOff("Record Path", ConsistencyTrackerModule.Instance.DoRecordPath) {
            OnValueChange = v => {
                Logging.Log(v ? "Recording chapter path..." : "Stopped recording path. Outputting info...");

                RecordPath = v;
                ConsistencyTrackerModule.Instance.DoRecordPath = v;
                ConsistencyTrackerModule.Instance.SaveChapterStats();
            }
        });

        subMenu.Add(new TextMenu.SubHeader("Editing the path requires a reload of the Overlay"));
        subMenu.Add(new TextMenu.Button("Remove Current Room From Path") {
            OnPressed = ConsistencyTrackerModule.Instance.RemoveRoomFromChapter
        });

        menu.Add(subMenu);
    }


    public bool WipeChapter { get; set; } = false;
    public void CreateWipeChapterEntry(TextMenu menu, bool inGame) {
        if (!inGame)
            return;

        var subMenu = new TextMenuExt.SubMenu("!!Data Wipe!!", false);
        subMenu.Add(new TextMenu.SubHeader("These actions cannot be reverted!"));


        subMenu.Add(new TextMenu.SubHeader("Current Room"));
        subMenu.Add(new TextMenu.Button("Remove Last Attempt") {
            OnPressed = () => {
                ConsistencyTrackerModule.Instance.RemoveLastAttempt();
            }
        });

        subMenu.Add(new TextMenu.Button("Remove Last Death Streak") {
            OnPressed = () => {
                ConsistencyTrackerModule.Instance.RemoveLastDeathStreak();
            }
        });

        subMenu.Add(new TextMenu.Button("Remove All Attempts") {
            OnPressed = () => {
                ConsistencyTrackerModule.Instance.WipeRoomData();
            }
        });

        subMenu.Add(new TextMenu.Button("Remove Golden Berry Deaths") {
            OnPressed = () => {
                ConsistencyTrackerModule.Instance.RemoveRoomGoldenBerryDeaths();
            }
        });


        subMenu.Add(new TextMenu.SubHeader("Current Chapter"));
        subMenu.Add(new TextMenu.Button("Reset All Attempts") {
            OnPressed = () => {
                ConsistencyTrackerModule.Instance.WipeChapterData();
            }
        });

        subMenu.Add(new TextMenu.Button("Reset All Golden Berry Deaths") {
            OnPressed = () => {
                ConsistencyTrackerModule.Instance.WipeChapterGoldenBerryDeaths();
            }
        });


        menu.Add(subMenu);
    }


    public bool CreateSummary { get; set; } = false;
    public int SummarySelectedAttemptCount { get; set; } = 20;
    public void CreateCreateSummaryEntry(TextMenu menu, bool inGame) {
        if (!inGame)
            return;

        var subMenu = new TextMenuExt.SubMenu("Tracker Summary", false);
        subMenu.Add(new TextMenu.SubHeader("Outputs some cool data of the current chapter in a readable .txt format"));


        subMenu.Add(new TextMenu.SubHeader("When calculating the consistency stats, only the last X attempts will be counted"));
        var attemptCounts = new List<KeyValuePair<int, string>>() {
                new(5, "5"),
                new(10, "10"),
                new(20, "20"),
                new(100, "100"),
            };
        subMenu.Add(new TextMenuExt.EnumerableSlider<int>("Summary Over X Attempts", attemptCounts, SummarySelectedAttemptCount) {
            OnValueChange = (value) => {
                SummarySelectedAttemptCount = value;
            }
        });


        subMenu.Add(new TextMenu.Button("Create Chapter Summary") {
            OnPressed = () => {
                ConsistencyTrackerModule.Instance.CreateChapterSummary(SummarySelectedAttemptCount);
            }
        });

        menu.Add(subMenu);
    }



    //Live Data Settings:
    //- Percentages digit cutoff (default: 2)
    //- Stats over X Attempts
    //- Reload format file
    //- Toggle name/abbreviation for e.g. PB Display
    public bool LiveData { get; set; } = false;
    public int LiveDataDecimalPlaces { get; set; } = 2;
    public int LiveDataSelectedAttemptCount { get; set; } = 20;

    //Types: 1 -> EH-3 | 2 -> Event-Horizon-3
    [SettingIgnore]
    public RoomNameDisplayType LiveDataRoomNameDisplayType { get; set; } = RoomNameDisplayType.AbbreviationAndRoomNumberInCP;

    //Types: 1 -> EH-3 | 2 -> Event-Horizon-3
    [SettingIgnore]
    public ListFormat LiveDataListOutputFormat { get; set; } = ListFormat.Json;

    [SettingIgnore]
    public bool LiveDataHideFormatsWithoutPath { get; set; } = false;

    [SettingIgnore]
    public bool LiveDataIgnoreUnplayedRooms { get; set; } = false;

    public OverlayPosition OverlayPosition { get; internal set; }
    public int OverlayOpacity { get; set; } = 8;

    public void CreateLiveDataEntry(TextMenu menu, bool inGame) {
        var subMenu = new TextMenuExt.SubMenu("Live Data Settings", false);


        subMenu.Add(new TextMenu.SubHeader("Floating point numbers will be rounded to this decimal"));
        var digitCounts = new List<KeyValuePair<int, string>>() {
                new(0, "0"),
                new(1, "1"),
                new(2, "2"),
                new(3, "3"),
                new(4, "4"),
                new(5, "5"),
            };
        subMenu.Add(new TextMenuExt.EnumerableSlider<int>("Max. Decimal Places", digitCounts, LiveDataDecimalPlaces) {
            OnValueChange = (value) => {
                LiveDataDecimalPlaces = value;
            }
        });


        subMenu.Add(new TextMenu.SubHeader("When calculating room consistency stats, only the last X attempts in each room will be counted"));
        var attemptCounts = new List<KeyValuePair<int, string>>() {
                new(5, "5"),
                new(10, "10"),
                new(20, "20"),
                new(100, "100"),
            };
        subMenu.Add(new TextMenuExt.EnumerableSlider<int>("Consider Last X Attempts", attemptCounts, LiveDataSelectedAttemptCount) {
            OnValueChange = (value) => {
                LiveDataSelectedAttemptCount = value;
            }
        });


        subMenu.Add(new TextMenu.SubHeader("Whether you want checkpoint names to be full or abbreviated in the room name"));
        var PBNameTypes = new List<KeyValuePair<int, string>>() {
                new((int)RoomNameDisplayType.AbbreviationAndRoomNumberInCP, "EH-3"),
                new((int)RoomNameDisplayType.FullNameAndRoomNumberInCP, "Event-Horizon-3"),
            };
        subMenu.Add(new TextMenuExt.EnumerableSlider<int>("Room Name Format", PBNameTypes, (int)LiveDataRoomNameDisplayType) {
            OnValueChange = (value) => {
                LiveDataRoomNameDisplayType = (RoomNameDisplayType)value;
            }
        });


        subMenu.Add(new TextMenu.SubHeader("Output format for lists. Plain is easily readable, JSON is for programming purposes"));
        var listTypes = new List<KeyValuePair<int, string>>() {
                new((int)ListFormat.Plain, "Plain"),
                new((int) ListFormat.Json, "JSON"),
            };
        subMenu.Add(new TextMenuExt.EnumerableSlider<int>("List Output Format", listTypes, (int)LiveDataListOutputFormat) {
            OnValueChange = (value) => {
                LiveDataListOutputFormat = (ListFormat)value;
            }
        });


        subMenu.Add(new TextMenu.SubHeader("If a format depends on path information and no path is set, the format will be blanked out"));
        subMenu.Add(new TextMenu.OnOff("Hide Formats When No Path", LiveDataHideFormatsWithoutPath) {
            OnValueChange = v => {
                LiveDataHideFormatsWithoutPath = v;
            }
        });


        subMenu.Add(new TextMenu.SubHeader("For chance calculation unplayed rooms count as 0% success rate. Toggle this on to ignore unplayed rooms"));
        subMenu.Add(new TextMenu.OnOff("Ignore Unplayed Rooms", LiveDataIgnoreUnplayedRooms) {
            OnValueChange = v => {
                LiveDataIgnoreUnplayedRooms = v;
            }
        });


        subMenu.Add(new TextMenu.SubHeader($"After editing '{StatManager.BaseFolder}/{StatManager.FormatFileName}' use this to update the live data format"));
        subMenu.Add(new TextMenu.Button("Reload format file") {
            OnPressed = () => {
                ConsistencyTrackerModule.Instance.StatsManager.LoadFormats();
            }
        });

        menu.Add(subMenu);
    }
    public LogType LogType { get; set; } = LogType.ConsistencyTracker;
    public LogLevel LogLevel { get; set; } = LogLevel.Warn;

}

public enum OverlayPosition {
    Disabled,
    Bottom,
    Top,
    Left,
    Right,
}

public static class OverlayPositionExtensions {
    public static bool IsHorizontal(this OverlayPosition self) => self is OverlayPosition.Top or OverlayPosition.Bottom;
    public static bool IsVertical(this OverlayPosition self) => self is OverlayPosition.Left or OverlayPosition.Right;
}