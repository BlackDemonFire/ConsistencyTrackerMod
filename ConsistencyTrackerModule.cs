using Celeste.Mod.ConsistencyTracker.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.ThirdParty;
using Celeste.Mod.ConsistencyTracker.Stats;
using Celeste.Mod.ConsistencyTracker.Entities;

namespace Celeste.Mod.ConsistencyTracker {

    public class ConsistencyTrackerModule : EverestModule {

        public static ConsistencyTrackerModule Instance;

        public static readonly string OverlayVersion = "1.1.1";
        public static readonly string ModVersion = "1.3.8";

        public override Type SettingsType => typeof(ConsistencyTrackerSettings);
        public ConsistencyTrackerSettings ModSettings => (ConsistencyTrackerSettings)_Settings;
        private const string BaseFolderPath = "./ConsistencyTracker/";


        private bool _didRestart;
        private readonly HashSet<string> _chaptersThisSession = new HashSet<string>();

        #region Path Recording Variables

        public bool DoRecordPath {
            get => _doRecordPath;
            set {
                if (value) {
                    if (_disabledInRoomName != _currentRoomName) {
                        _path = new PathRecorder();
                        InsertCheckpointIntoPath(null, _lastRoomWithCheckpoint);
                        _path.AddRoom(_currentRoomName);
                    }
                } else {
                    SaveRecordedRoomPath();
                }

                _doRecordPath = value;
            }
        }

        private bool _doRecordPath;
        private PathRecorder _path;
        private string _disabledInRoomName;

        #endregion

        #region State Variables

        public PathInfo CurrentChapterPath { get; private set; }
        public ChapterStats CurrentChapterStats;

        private string _currentChapterDebugName;
        private string _previousRoomName;
        private string _currentRoomName;

        private string _lastRoomWithCheckpoint;

        private bool _currentRoomCompleted;
        private bool _currentRoomCompletedResetOnDeath;
        private bool _playerIsHoldingGolden;

        #endregion

        public StatManager StatsManager;


        public ConsistencyTrackerModule() {
            Instance = this;
        }

        #region Load/Unload Stuff

        public override void Load() {
            CheckFolderExists(BaseFolderPath);
            CheckFolderExists(GetPathToFolder("paths"));
            CheckFolderExists(GetPathToFolder("stats"));
            CheckFolderExists(GetPathToFolder("logs"));
            CheckFolderExists(GetPathToFolder("summaries"));

            Logging.LogInit();

            HookStuff();

            StatsManager = new StatManager();
        }

        private void HookStuff() {
            //Track where the player is
            On.Celeste.Level.Begin += Level_Begin;
            Everest.Events.Level.OnExit += Level_OnExit;
            Everest.Events.Level.OnComplete += Level_OnComplete;
            Everest.Events.Level.OnTransitionTo += Level_OnTransitionTo;
            Everest.Events.Level.OnLoadLevel += Level_OnLoadLevel;
            On.Celeste.Level.TeleportTo += Level_TeleportTo;
            //Track deaths
            Everest.Events.Player.OnDie += Player_OnDie;
            //Track checkpoints
            On.Celeste.Checkpoint.TurnOn += Checkpoint_TurnOn;

            //Track in-room events, to determine when exiting back into a previous room counts as success
            //E.g. Power Source rooms where you collect a key but exit back into the HUB room should be marked as success

            //Picking up a kye
            On.Celeste.Key.OnPlayer += Key_OnPlayer; //works

            //Activating Resort clutter switches
            On.Celeste.ClutterSwitch.OnDashed += ClutterSwitch_OnDashed; //works

            //Picking up a strawberry
            On.Celeste.Strawberry.OnCollect += Strawberry_OnCollect; //doesn't work :(
            On.Celeste.Strawberry.OnPlayer +=
                Strawberry_OnPlayer; //sorta works, but triggers very often for a single berry

            //Changing lava/ice in Core
            On.Celeste.CoreModeToggle.OnChangeMode += CoreModeToggle_OnChangeMode; //works

            //Picking up a Cassette tape
            On.Celeste.Cassette.OnPlayer += Cassette_OnPlayer; //works

            //Open up key doors?
            //On.Celeste.Door.Open += Door_Open; //Wrong door (those are the resort doors)
            On.Celeste.LockBlock.TryOpen += LockBlock_TryOpen; //works
        }

        private void UnHookStuff() {
            On.Celeste.Level.Begin -= Level_Begin;
            Everest.Events.Level.OnExit -= Level_OnExit;
            Everest.Events.Level.OnComplete -= Level_OnComplete;
            Everest.Events.Level.OnTransitionTo -= Level_OnTransitionTo;
            Everest.Events.Level.OnLoadLevel -= Level_OnLoadLevel;
            On.Celeste.Level.TeleportTo -= Level_TeleportTo;

            //Track deaths
            Everest.Events.Player.OnDie -= Player_OnDie;

            //Track checkpoints
            On.Celeste.Checkpoint.TurnOn -= Checkpoint_TurnOn;

            //Picking up a kye
            On.Celeste.Key.OnPlayer -= Key_OnPlayer;

            //Activating Resort clutter switches
            On.Celeste.ClutterSwitch.OnDashed -= ClutterSwitch_OnDashed;

            //Picking up a strawberry
            On.Celeste.Strawberry.OnPlayer -= Strawberry_OnPlayer;

            //Changing lava/ice in Core
            On.Celeste.CoreModeToggle.OnChangeMode -= CoreModeToggle_OnChangeMode;

            //Picking up a Cassette tape
            On.Celeste.Cassette.OnPlayer -= Cassette_OnPlayer;

            //Open up key doors
            On.Celeste.LockBlock.TryOpen -= LockBlock_TryOpen;
        }

        public override void Initialize() {
            base.Initialize();

            // load SpeedrunTool if it exists
            if (Everest.Modules.Any(m => m.Metadata.Name == "SpeedrunTool")) {
                SpeedrunToolSupport.Load();
            }
        }

        public override void Unload() {
            UnHookStuff();
            Logging.Unload();
        }

        #endregion

        #region Hooks

        private void LockBlock_TryOpen(On.Celeste.LockBlock.orig_TryOpen orig, LockBlock self, Player player,
            Follower fol) {
            orig(self, player, fol);
            Logging.Log("Opened a door");
            SetRoomCompleted(resetOnDeath: false);
        }

        private DashCollisionResults ClutterSwitch_OnDashed(On.Celeste.ClutterSwitch.orig_OnDashed orig,
            ClutterSwitch self, Player player, Vector2 direction) {
            Logging.Log("Activated a clutter switch");
            SetRoomCompleted(resetOnDeath: false);
            return orig(self, player, direction);
        }

        private void Key_OnPlayer(On.Celeste.Key.orig_OnPlayer orig, Key self, Player player) {
            Logging.Log("Picked up a key");
            orig(self, player);
            SetRoomCompleted(resetOnDeath: false);
        }

        private void Cassette_OnPlayer(On.Celeste.Cassette.orig_OnPlayer orig, Cassette self, Player player) {
            Logging.Log("Collected a cassette tape");
            orig(self, player);
            SetRoomCompleted(resetOnDeath: false);
        }

        private readonly List<EntityID> _touchedBerries = new List<EntityID>();

        // All touched berries need to be reset on death, since they either:
        // - already collected
        // - disappeared on death
        private void Strawberry_OnPlayer(On.Celeste.Strawberry.orig_OnPlayer orig, Strawberry self, Player player) {
            orig(self, player);

            if (_touchedBerries.Contains(self.ID))
                return; //to not spam the log
            _touchedBerries.Add(self.ID);

            Logging.Log("Strawberry on player");
            SetRoomCompleted(resetOnDeath: true);
        }

        private void Strawberry_OnCollect(On.Celeste.Strawberry.orig_OnCollect orig, Strawberry self) {
            Logging.Log("Collected a strawberry");
            orig(self);
            SetRoomCompleted(resetOnDeath: false);
        }

        private void CoreModeToggle_OnChangeMode(On.Celeste.CoreModeToggle.orig_OnChangeMode orig, CoreModeToggle self,
            Session.CoreModes mode) {
            Logging.Log($"Changed core mode to '{mode}'");
            orig(self, mode);
            SetRoomCompleted(resetOnDeath: true);
        }

        private void Checkpoint_TurnOn(On.Celeste.Checkpoint.orig_TurnOn orig, Checkpoint cp, bool animate) {
            orig(cp, animate);
            Logging.Log($"cp.Position={cp.Position}, LastRoomWithCheckpoint={_lastRoomWithCheckpoint}");
            if (ModSettings.Enabled && DoRecordPath) {
                InsertCheckpointIntoPath(cp, _lastRoomWithCheckpoint);
            }
        }

        //Not triggered when teleporting via debug map
        private void Level_TeleportTo(On.Celeste.Level.orig_TeleportTo orig, Level level, Player player,
            string nextLevel, Player.IntroTypes introType, Vector2? nearestSpawn) {
            orig(level, player, nextLevel, introType, nearestSpawn);
            Logging.Log($"level.Session.LevelData.Name={SanitizeRoomName(level.Session.LevelData.Name)}");
        }

        private void Level_OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            var newCurrentRoom = SanitizeRoomName(level.Session.LevelData.Name);
            var holdingGolden = PlayerIsHoldingGoldenBerry(level.Tracker.GetEntity<Player>());

            Logging.Log(
                $"level.Session.LevelData.Name={newCurrentRoom}, playerIntro={playerIntro} | CurrentRoomName: '{_currentRoomName}', PreviousRoomName: '{_previousRoomName}'");
            if (playerIntro == Player.IntroTypes.Respawn) {
                //Changing room via golden berry death or debug map teleport
                if (_currentRoomName != null && newCurrentRoom != _currentRoomName) {
                    SetNewRoom(newCurrentRoom, false, holdingGolden);
                }
            }

            if (_didRestart) {
                Logging.Log("\tRequested reset of PreviousRoomName to null");
                _didRestart = false;
                SetNewRoom(newCurrentRoom, false, holdingGolden);
                _previousRoomName = null;
            }

            if (isFromLoader) {
                level.Add(new RoomOverlay());
            }
        }

        private void Level_OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            Logging.Log($"mode={mode}, snow={snow}");
            switch (mode) {
                case LevelExit.Mode.Restart:
                    _didRestart = true;
                    break;
                case LevelExit.Mode.GoldenBerryRestart: {
                    _didRestart = true;

                    if (ModSettings.Enabled && !ModSettings.PauseDeathTracking) {
                        //Only count golden berry deaths when enabled
                        CurrentChapterStats?.AddGoldenBerryDeath();
                        if (ModSettings.OnlyTrackWithGoldenBerry) {
                            CurrentChapterStats?.AddAttempt(false);
                        }
                    }

                    break;
                }
            }

            if (!DoRecordPath)
                return;
            DoRecordPath = false;
            ModSettings.RecordPath = false;
        }

        private void Level_OnComplete(Level level) {
            Logging.Log($"Incrementing {CurrentChapterStats?.CurrentRoom.DebugRoomName}");
            if (ModSettings.Enabled && !ModSettings.PauseDeathTracking &&
                (!ModSettings.OnlyTrackWithGoldenBerry || _playerIsHoldingGolden))
                CurrentChapterStats?.AddAttempt(true);
            if (CurrentChapterStats != null)
                CurrentChapterStats.ModState.ChapterCompleted = true;
            SaveChapterStats();
        }

        private void Level_Begin(On.Celeste.Level.orig_Begin orig, Level level) {
            Logging.Log("Calling ChangeChapter with 'level.Session'");
            ChangeChapter(level.Session);
            orig(level);
        }

        private void Level_OnTransitionTo(Level level, LevelData levelDataNext, Vector2 direction) {
            if (levelDataNext.HasCheckpoint) {
                _lastRoomWithCheckpoint = levelDataNext.Name;
            }

            var roomName = SanitizeRoomName(levelDataNext.Name);
            Logging.Log(
                $"levelData.Name->{roomName}, level.Completed->{level.Completed}, level.NewLevel->{level.NewLevel}, level.Session.StartCheckpoint->{level.Session.StartCheckpoint}");
            var holdingGolden = PlayerIsHoldingGoldenBerry(level.Tracker.GetEntity<Player>());
            SetNewRoom(roomName, true, holdingGolden);
        }

        private void Player_OnDie(Player player) {
            _touchedBerries.Clear();
            var holdingGolden = PlayerIsHoldingGoldenBerry(player);

            Logging.Log($"Player died. (holdingGolden: {holdingGolden})");
            if (_currentRoomCompletedResetOnDeath) {
                _currentRoomCompleted = false;
            }

            if (ModSettings.Enabled) {
                if (!ModSettings.PauseDeathTracking && (!ModSettings.OnlyTrackWithGoldenBerry || holdingGolden))
                    CurrentChapterStats?.AddAttempt(false);

                if (CurrentChapterStats != null)
                    CurrentChapterStats.CurrentRoom.DeathsInCurrentRun++;

                SaveChapterStats();
            }
        }

        #endregion

        #region State Management

        private string SanitizeRoomName(string name) {
            name = name.Replace(";", "");
            return name;
        }

        private void ChangeChapter(Session session) {
            Logging.Log("Called chapter change");
            var area = AreaData.Areas[session.Area.ID];
            var chapName = area.Name;
            var chapNameClean = chapName.DialogCleanOrNull() ?? chapName.SpacedPascalCase();
            var campaignName = DialogExt.CleanLevelSet(area.GetLevelSet());

            Logging.Log(
                $"Level->{session.Level}, session.Area.GetSID()->{session.Area.GetSID()}, session.Area.Mode->{session.Area.Mode}, chapterNameClean->{chapNameClean}, campaignName->{campaignName}");

            _currentChapterDebugName = ($"{session.MapData.Data.SID}_{session.Area.Mode}").Replace("/", "_");

            //string test2 = Dialog.Get($"luma_farewellbb_FarewellBB_b_intro");
            //Log($"[ChangeChapter] Dialog Test 2: {test2}");

            _previousRoomName = null;
            _currentRoomName = session.Level;

            CurrentChapterPath = GetPathInputInfo();
            CurrentChapterStats = GetCurrentChapterStats();

            CurrentChapterStats.ChapterSID = session.MapData.Data.SID;
            CurrentChapterStats.ChapterSIDDialogSanitized = SanitizeSidForDialog(session.MapData.Data.SID);
            CurrentChapterStats.ChapterName = chapNameClean;
            CurrentChapterStats.CampaignName = campaignName;

            _touchedBerries.Clear();

            SetNewRoom(_currentRoomName, false, false);
            _lastRoomWithCheckpoint = session.LevelData.HasCheckpoint ? _currentRoomName : null;

            if (!DoRecordPath && ModSettings.RecordPath) {
                DoRecordPath = true;
            }
        }

        public void SetNewRoom(string newRoomName, bool countDeath = true, bool holdingGolden = false) {
            _playerIsHoldingGolden = holdingGolden;
            CurrentChapterStats.ModState.ChapterCompleted = false;

            if (_previousRoomName == newRoomName && !_currentRoomCompleted) {
                //Don't complete if entering previous room and current room was not completed
                Logging.Log($"Entered previous room '{_previousRoomName}'");
                _previousRoomName = _currentRoomName;
                _currentRoomName = newRoomName;
                CurrentChapterStats?.SetCurrentRoom(newRoomName);
                SaveChapterStats();
                return;
            }


            Logging.Log($"Entered new room '{newRoomName}' | Holding golden: '{holdingGolden}'");

            _previousRoomName = _currentRoomName;
            _currentRoomName = newRoomName;
            _currentRoomCompleted = false;

            if (DoRecordPath) {
                _path.AddRoom(newRoomName);
            }

            if (ModSettings.Enabled && CurrentChapterStats != null) {
                if (countDeath && !ModSettings.PauseDeathTracking &&
                    (!ModSettings.OnlyTrackWithGoldenBerry || holdingGolden)) {
                    CurrentChapterStats.AddAttempt(true);
                }

                CurrentChapterStats.SetCurrentRoom(newRoomName);
                SaveChapterStats();
            }
        }

        private void SetRoomCompleted(bool resetOnDeath = false) {
            _currentRoomCompleted = true;
            _currentRoomCompletedResetOnDeath = resetOnDeath;
        }

        private static bool PlayerIsHoldingGoldenBerry(Player player) {
            if (player?.Leader?.Followers == null)
                return false;

            return player.Leader.Followers.Where((f) => {
                if (!(f.Entity is Strawberry berry))
                    return false;

                return berry.Golden && !berry.Winged;
            }).Any();
        }

        #region Speedrun Tool Save States

        public void SpeedrunToolSaveState(Dictionary<Type, Dictionary<string, object>> savedValues, Level level) {
            var type = GetType();
            if (!savedValues.ContainsKey(type)) {
                savedValues.Add(type, new Dictionary<string, object>());
                savedValues[type].Add(nameof(_previousRoomName), _previousRoomName);
                savedValues[type].Add(nameof(_currentRoomName), _currentRoomName);
                savedValues[type].Add(nameof(_currentRoomCompleted), _currentRoomCompleted);
                savedValues[type].Add(nameof(_currentRoomCompletedResetOnDeath), _currentRoomCompletedResetOnDeath);
            } else {
                savedValues[type][nameof(_previousRoomName)] = _previousRoomName;
                savedValues[type][nameof(_currentRoomName)] = _currentRoomName;
                savedValues[type][nameof(_currentRoomCompleted)] = _currentRoomCompleted;
                savedValues[type][nameof(_currentRoomCompletedResetOnDeath)] = _currentRoomCompletedResetOnDeath;
            }
        }

        public void SpeedrunToolLoadState(Dictionary<Type, Dictionary<string, object>> savedValues, Level level) {
            var type = GetType();
            if (!savedValues.ContainsKey(type)) {
                Logger.Log(nameof(ConsistencyTrackerModule), "Trying to load state without prior saving a state...");
                return;
            }

            _previousRoomName = (string)savedValues[type][nameof(_previousRoomName)];
            _currentRoomName = (string)savedValues[type][nameof(_currentRoomName)];
            _currentRoomCompleted = (bool)savedValues[type][nameof(_currentRoomCompleted)];
            _currentRoomCompletedResetOnDeath = (bool)savedValues[type][nameof(_currentRoomCompletedResetOnDeath)];

            CurrentChapterStats.SetCurrentRoom(_currentRoomName);
            SaveChapterStats();
        }

        public void SpeedrunToolClearState() {
            //No action
        }

        #endregion

        #endregion

        #region Data Import/Export

        public static string GetPathToFile(string file) {
            return BaseFolderPath + file;
        }

        public static string GetPathToFolder(string folder) {
            return BaseFolderPath + folder + "/";
        }

        public static void CheckFolderExists(string folderPath) {
            if (!Directory.Exists(folderPath)) {
                Directory.CreateDirectory(folderPath);
            }
        }


        public bool PathInfoExists() {
            var path = GetPathToFile($"paths/{_currentChapterDebugName}.txt");
            return File.Exists(path);
        }

        public PathInfo GetPathInputInfo() {
            Logging.Log($"Fetching path info for chapter '{_currentChapterDebugName}'");

            var path = GetPathToFile($"paths/{_currentChapterDebugName}.txt");
            Logging.Log($"\tSearching for path '{path}'");

            if (File.Exists(path)) {
                //Parse File
                Logging.Log("\tFound file, parsing...");
                var content = File.ReadAllText(path);

                try {
                    return PathInfo.ParseString(content);
                } catch (Exception) {
                    Logging.Log(
                        $"\tCouldn't read old path info, created new PathInfo. Old path info content:\n{content}");
                    var toRet = new PathInfo();
                    return toRet;
                }

            } else {
                //Create new
                Logging.Log("\tDidn't find file, returned null.");
                return null;
            }
        }

        public ChapterStats GetCurrentChapterStats() {
            var path = GetPathToFile($"stats/{_currentChapterDebugName}.txt");

            var hasEnteredThisSession = _chaptersThisSession.Contains(_currentChapterDebugName);
            _chaptersThisSession.Add(_currentChapterDebugName);
            Logging.Log(
                $"CurrentChapterName: '{_currentChapterDebugName}', hasEnteredThisSession: '{hasEnteredThisSession}', ChaptersThisSession: '{string.Join(", ", _chaptersThisSession)}'");

            ChapterStats toRet;

            if (File.Exists(path)) {
                //Parse File
                var content = File.ReadAllText(path);
                toRet = ChapterStats.ParseString(content);
                toRet.ChapterDebugName = _currentChapterDebugName;

            } else {
                //Create new
                toRet = new ChapterStats() {
                    ChapterDebugName = _currentChapterDebugName,
                };
                toRet.SetCurrentRoom(_currentRoomName);
            }

            if (!hasEnteredThisSession) {
                toRet.ResetCurrentSession();
                Logging.Log("Resetting session for GB deaths");
            } else {
                Logging.Log("Not resetting session for GB deaths");
            }

            return toRet;
        }

        public void SaveChapterStats() {
            if (CurrentChapterStats == null) {
                Logging.Log($"Aborting saving chapter stats as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            CurrentChapterStats.ModState.PlayerIsHoldingGolden = _playerIsHoldingGolden;
            CurrentChapterStats.ModState.GoldenDone =
                _playerIsHoldingGolden && CurrentChapterStats.ModState.ChapterCompleted;

            CurrentChapterStats.ModState.DeathTrackingPaused = ModSettings.PauseDeathTracking;
            CurrentChapterStats.ModState.RecordingPath = ModSettings.RecordPath;
            CurrentChapterStats.ModState.OverlayVersion = OverlayVersion;
            CurrentChapterStats.ModState.ModVersion = ModVersion;


            var path = GetPathToFile($"stats/{_currentChapterDebugName}.txt");
            File.WriteAllText(path, CurrentChapterStats.ToChapterStatsString());

            var modStatePath = GetPathToFile("stats/modState.txt");

            var content =
                $"{CurrentChapterStats.CurrentRoom}\n{CurrentChapterStats.ChapterDebugName};{CurrentChapterStats.ModState}\n";
            File.WriteAllText(modStatePath, content);

            StatsManager.OutputFormats(CurrentChapterPath, CurrentChapterStats);
        }

        public void CreateChapterSummary(int attemptCount) {
            Logging.Log($"attemptCount={attemptCount} Attempting to create tracker summary");

            var hasPathInfo = PathInfoExists();

            var relativeOutPath = $"summaries/{_currentChapterDebugName}.txt";
            var outPath = GetPathToFile(relativeOutPath);

            if (!hasPathInfo) {
                Logging.Log(
                    "Called CreateChapterSummary without chapter path info. Please create a path before using this feature");
                File.WriteAllText(outPath,
                    "No path info was found for the current chapter.\nPlease create a path before using the summary feature");
                return;
            }

            CurrentChapterStats?.OutputSummary(outPath, CurrentChapterPath, attemptCount);
        }

        #endregion

        #region Stats Data Control

        public void WipeChapterData() {
            if (CurrentChapterStats == null) {
                Logging.Log($"Aborting wiping chapter data as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            Logging.Log($"Wiping death data for chapter '{_currentChapterDebugName}'");

            var currentRoom = CurrentChapterStats.CurrentRoom;
            var toRemove = CurrentChapterStats.Rooms.Keys.Where(debugName => debugName != currentRoom.DebugRoomName)
                .ToList();

            foreach (var debugName in toRemove) {
                CurrentChapterStats.Rooms.Remove(debugName);
            }

            WipeRoomData();
        }

        public void RemoveRoomGoldenBerryDeaths() {
            if (CurrentChapterStats == null) {
                Logging.Log($"Aborting wiping room golden berry deaths as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            Logging.Log($"Wiping golden berry death data for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'");

            CurrentChapterStats.CurrentRoom.GoldenBerryDeaths = 0;
            CurrentChapterStats.CurrentRoom.GoldenBerryDeathsSession = 0;

            SaveChapterStats();
        }

        public void WipeChapterGoldenBerryDeaths() {
            if (CurrentChapterStats == null) {
                Logging.Log($"Aborting wiping chapter golden berry deaths as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            Logging.Log($"Wiping golden berry death data for chapter '{_currentChapterDebugName}'");

            foreach (var debugName in CurrentChapterStats.Rooms.Keys) {
                CurrentChapterStats.Rooms[debugName].GoldenBerryDeaths = 0;
                CurrentChapterStats.Rooms[debugName].GoldenBerryDeathsSession = 0;
            }

            SaveChapterStats();
        }



        public void WipeRoomData() {
            if (CurrentChapterStats == null) {
                Logging.Log($"Aborting wiping room data as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            Logging.Log($"Wiping room data for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'");

            CurrentChapterStats.CurrentRoom.PreviousAttempts.Clear();
            SaveChapterStats();
        }

        public void RemoveLastDeathStreak() {
            if (CurrentChapterStats == null) {
                Logging.Log($"Aborting removing death streak as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            Logging.Log($"Removing death streak for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'");

            while (CurrentChapterStats.CurrentRoom.PreviousAttempts.Count > 0 &&
                   CurrentChapterStats.CurrentRoom.LastAttempt == false) {
                CurrentChapterStats.CurrentRoom.RemoveLastAttempt();
            }

            SaveChapterStats();
        }

        public void RemoveLastAttempt() {
            if (CurrentChapterStats == null) {
                Logging.Log($"Aborting removing death streak as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            Logging.Log($"Removing last attempt for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'");

            CurrentChapterStats.CurrentRoom.RemoveLastAttempt();
            SaveChapterStats();
        }

        #endregion

        #region Path Management

        public void SaveRecordedRoomPath() {
            Logging.Log("Saving recorded path...");
            _disabledInRoomName = _currentRoomName;
            CurrentChapterPath = _path.ToPathInfo();
            Logging.Log($"Recorded path:\n{CurrentChapterPath}");
            SaveRoomPath();
        }

        public void SaveRoomPath() {
            var relativeOutPath = $"paths/{_currentChapterDebugName}.txt";
            var outPath = GetPathToFile(relativeOutPath);
            File.WriteAllText(outPath, CurrentChapterPath.ToString());
            Logging.Log($"Wrote path data to '{relativeOutPath}'");
        }

        public void RemoveRoomFromChapter() {
            if (CurrentChapterPath == null) {
                Logging.Log("CurrentChapterPath was null");
                return;
            }

            var foundRoom = false;
            foreach (var cpInfo in CurrentChapterPath.Checkpoints) {
                foreach (var rInfo in cpInfo.Rooms.Where(rInfo => rInfo.DebugRoomName == _currentRoomName)) {
                    cpInfo.Rooms.Remove(rInfo);
                    foundRoom = true;
                    break;
                }

                if (foundRoom)
                    break;
            }

            if (foundRoom) {
                SaveRoomPath();
            }
        }

        #endregion

        #region Util

        public static string SanitizeSidForDialog(string sid) {
            const string bsSuffix = "_pqeijpvqie";
            var dialogCleaned = Dialog.Get($"{sid}{bsSuffix}");
            return dialogCleaned.Substring(1, sid.Length);
        }

        public void InsertCheckpointIntoPath(Checkpoint cp, string roomName) {
            if (roomName == null) {
                _path.AddCheckpoint(cp, PathRecorder.DefaultCheckpointName);
                return;
            }

            var cpDialogName = $"{CurrentChapterStats.ChapterSIDDialogSanitized}_{roomName}";
            //Log($"[Dialog Testing] cpDialogName: {cpDialogName}");
            var cpName = Dialog.Get(cpDialogName);
            //Log($"[Dialog Testing] Dialog.Get says: {cpName}");

            if (cpName.Length + 1 >= cpDialogName.Length && cpName.Substring(1, cpDialogName.Length) == cpDialogName)
                cpName = null;

            _path.AddCheckpoint(cp, cpName);
        }

        #endregion
    }
}