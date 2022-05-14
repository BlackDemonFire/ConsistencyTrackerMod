﻿using Celeste.Mod.ConsistencyTracker.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.ThirdParty;
using Celeste.Mod.ConsistencyTracker.Entities;

namespace Celeste.Mod.ConsistencyTracker {
    public class ConsistencyTrackerModule : EverestModule {

        public static ConsistencyTrackerModule Instance;

        public static readonly string OverlayVersion = "1.1.1";

        public override Type SettingsType => typeof(ConsistencyTrackerSettings);
        public ConsistencyTrackerSettings ModSettings => (ConsistencyTrackerSettings)this._Settings;

        private string CurrentChapterName;
        private string PreviousRoomName;
        private string CurrentRoomName;

        private string DisabledInRoomName;
        private bool DidRestart = false;

        private HashSet<string> ChaptersThisSession = new HashSet<string>();

        public bool DoRecordPath {
            get => _DoRecordPath;
            set {
                if (value) {
                    if (DisabledInRoomName != CurrentRoomName) {
                        Path = new PathRecorder();
                        Path.AddRoom(CurrentRoomName);
                    }
                } else {
                    SaveRecordedRoomPath();
                }

                _DoRecordPath = value;
            }
        }
        private bool _DoRecordPath = false;
        private PathRecorder Path;

        private PathInfo CurrentChapterPath;
        public ChapterStats CurrentChapterStats;


        public ConsistencyTrackerModule() {
            Instance = this;
        }

        public override void Load() {
            CheckFolderExists(baseFolderPath);
            CheckFolderExists(GetPathToFolder("paths"));
            CheckFolderExists(GetPathToFolder("stats"));
            CheckFolderExists(GetPathToFolder("logs"));
            CheckFolderExists(GetPathToFolder("summaries"));

            LogInit();
            Log($"~~~===============~~~");
            ChapterStats.LogCallback = Log;

            HookStuff();
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
            On.Celeste.Strawberry.OnCollect += Strawberry_OnCollect;
            On.Celeste.Strawberry.OnPlayer += Strawberry_OnPlayer; //sorta works, but triggers very often for a single berry

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

        public override void Initialize()
        {
            base.Initialize();

            // load SpeedrunTool if it exists
            if (Everest.Modules.Any(m => m.Metadata.Name == "SpeedrunTool")) {
                SpeedrunToolSupport.Load();
            }
        }

        private void LockBlock_TryOpen(On.Celeste.LockBlock.orig_TryOpen orig, LockBlock self, Player player, Follower fol) {
            orig(self, player, fol);
            Log($"[LockBlock.TryOpen] Opened a door");
            SetRoomCompleted(resetOnDeath: false);
        }

        private DashCollisionResults ClutterSwitch_OnDashed(On.Celeste.ClutterSwitch.orig_OnDashed orig, ClutterSwitch self, Player player, Vector2 direction) {
            Log($"[ClutterSwitch.OnDashed] Activated a clutter switch");
            SetRoomCompleted(resetOnDeath: false);
            return orig(self, player, direction);
        }

        private void Key_OnPlayer(On.Celeste.Key.orig_OnPlayer orig, Key self, Player player) {
            Log($"[Key.OnPlayer] Picked up a key");
            orig(self, player);
            SetRoomCompleted(resetOnDeath: false);
        }

        private void Cassette_OnPlayer(On.Celeste.Cassette.orig_OnPlayer orig, Cassette self, Player player) {
            Log($"[Cassette.OnPlayer] Collected a cassette tape");
            orig(self, player);
            SetRoomCompleted(resetOnDeath: false);
        }

        private Strawberry LastTouchedStrawberry = null;
        private void Strawberry_OnPlayer(On.Celeste.Strawberry.orig_OnPlayer orig, Strawberry self, Player player) {
            if (LastTouchedStrawberry != null && LastTouchedStrawberry == self) return; //to not spam the log
            LastTouchedStrawberry = self;

            Log($"[Strawberry.OnPlayer] Strawberry on player");
            orig(self, player);
            SetRoomCompleted(resetOnDeath: true);
        }

        private void Strawberry_OnCollect(On.Celeste.Strawberry.orig_OnCollect orig, Strawberry self) {
            Log($"[Strawberry.OnCollect] Collected a strawberry");
            orig(self);
            SetRoomCompleted(resetOnDeath: false);
        }

        private void CoreModeToggle_OnChangeMode(On.Celeste.CoreModeToggle.orig_OnChangeMode orig, CoreModeToggle self, Session.CoreModes mode) {
            Log($"[CoreModeToggle.OnChangeMode] Changed core mode to '{mode}'");
            orig(self, mode);
            SetRoomCompleted(resetOnDeath:true);
        }

        private void Checkpoint_TurnOn(On.Celeste.Checkpoint.orig_TurnOn orig, Checkpoint cp, bool animate) {
            orig(cp, animate);
            Log($"[Checkpoint.TurnOn] cp.Position={cp.Position}");
            if (ModSettings.Enabled && DoRecordPath) {
                Path.AddCheckpoint(cp);
            }
        }

        //Not triggered when teleporting via debug map
        private void Level_TeleportTo(On.Celeste.Level.orig_TeleportTo orig, Level level, Player player, string nextLevel, Player.IntroTypes introType, Vector2? nearestSpawn) {
            orig(level, player, nextLevel, introType, nearestSpawn);
            Log($"[Level.TeleportTo] level.Session.LevelData.Name={level.Session.LevelData.Name}");
        }

        private void Level_OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            string newCurrentRoom = level.Session.LevelData.Name;
            bool holdingGolden = PlayerIsHoldingGoldenBerry(level.Tracker.GetEntity<Player>());

            Log($"[Level.OnLoadLevel] level.Session.LevelData.Name={newCurrentRoom}, playerIntro={playerIntro} | CurrentRoomName: '{CurrentRoomName}', PreviousRoomName: '{PreviousRoomName}'");
            if (playerIntro == Player.IntroTypes.Respawn) { //Changing room via golden berry death or debug map teleport
                if (CurrentRoomName != null && newCurrentRoom != CurrentRoomName) {
                    SetNewRoom(newCurrentRoom, false, holdingGolden);
                }
            }

            if (DidRestart) {
                Log($"\tRequested reset of PreviousRoomName to null");
                DidRestart = false;
                SetNewRoom(level.Session.LevelData.Name, false, holdingGolden);
                PreviousRoomName = null;
            }
        }

        private void Level_OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            Log($"[Level.OnExit] mode={mode}, snow={snow}");
            if (mode == LevelExit.Mode.Restart) {
                DidRestart = true;
            } else if (mode == LevelExit.Mode.GoldenBerryRestart) {
                DidRestart = true;

                if (ModSettings.Enabled && !ModSettings.PauseDeathTracking) { //Only count golden berry deaths when enabled
                    CurrentChapterStats?.AddGoldenBerryDeath();
                    if (ModSettings.OnlyTrackWithGoldenBerry) {
                        CurrentChapterStats.AddAttempt(false);
                    }
                }
            }

            if (DoRecordPath) {
                DoRecordPath = false;
                ModSettings.RecordPath = false;
            }
        }

        private void Level_OnComplete(Level level) {
            Log($"[Level.OnComplete] Incrementing {CurrentChapterStats?.CurrentRoom.DebugRoomName}");
            if(!ModSettings.PauseDeathTracking)
                CurrentChapterStats?.AddAttempt(true);
            SaveChapterStats();
        }

        private void Level_Begin(On.Celeste.Level.orig_Begin orig, Level level) {
            Log($"[Level.Begin] Calling ChangeChapter with 'level.Session'");
            ChangeChapter(level.Session);

            orig(level);
        }

        private void LevelLoader_StartLevel(On.Celeste.LevelLoader.orig_StartLevel orig, LevelLoader self) {
            var level = self.Level;
            level.Add(new RoomOverlay(level));
            orig(self);
        }

        private void ChangeChapter(Session session) {
            Log($"[ChangeChapter] Level->{session.Level}, session.Area.GetSID()->{session.Area.GetSID()}, session.Area.Mode->{session.Area.Mode}");

            CurrentChapterName = ($"{session.MapData.Data.SID}_{session.Area.Mode}").Replace("/", "_");


            PreviousRoomName = null;
            CurrentRoomName = session.Level;

            CurrentChapterPath = GetPathInputInfo();
            CurrentChapterStats = GetCurrentChapterStats();

            SetNewRoom(CurrentRoomName, false, false);

            if (!DoRecordPath && ModSettings.RecordPath) {
                DoRecordPath = true;
            }
        }

        private string FormatMapData(MapData data) {
            if (data == null) return "No MapData attached...";

            string metaPath = "";
            if (metaPath != null) {
                metaPath = data.Meta.Path ?? "null";
            }

            string filename = data.Filename ?? "null";
            string filepath = data.Filepath ?? "null";

            string areaChapterIndex = "";
            string areaMode = "";
            if (data.Area != null) {
                areaChapterIndex = data.Area.ChapterIndex.ToString();
                areaMode = data.Area.Mode.ToString();
            }

            string dataName = data.Data.Name ?? "null";
            string dataScreenName = data.Data.CompleteScreenName ?? "null";
            string dataSID = data.Data.SID ?? "null";

            return $"Data.SID->{dataSID}, Data.CompleteScreenName->{dataScreenName}, Data.Name->{dataName}, Meta.Path->{metaPath}, Filename->{filename}, Filepath->{filepath}, Area.ChapterIndex->{areaChapterIndex}, Area.Mode->{areaMode}";
        }

        private void Level_OnTransitionTo(Level level, LevelData levelDataNext, Vector2 direction) {
            Log($"[Level.OnTransitionTo] levelData.Name->{levelDataNext.Name}, level.Completed->{level.Completed}, level.NewLevel->{level.NewLevel}, level.Session.StartCheckpoint->{level.Session.StartCheckpoint}");
            bool holdingGolden = PlayerIsHoldingGoldenBerry(level.Tracker.GetEntity<Player>());
            SetNewRoom(levelDataNext.Name, true, holdingGolden);
        }

        private void Player_OnDie(Player player) {
            bool holdingGolden = PlayerIsHoldingGoldenBerry(player);

            Log($"[Player.OnDie] Player died. (holdingGolden: {holdingGolden})");
            if (_CurrentRoomCompletedResetOnDeath) {
                _CurrentRoomCompleted = false;
            }
            LastTouchedStrawberry = null; //Held strawberry reset on death, collected don't show up again so those don't matter

            if (ModSettings.Enabled) {
                if(!ModSettings.PauseDeathTracking && (!ModSettings.OnlyTrackWithGoldenBerry || holdingGolden))
                    CurrentChapterStats?.AddAttempt(false);
                SaveChapterStats();
            }
        }

        public override void Unload() {
            UnHookStuff();
        }

        private bool _CurrentRoomCompleted = false;
        private bool _CurrentRoomCompletedResetOnDeath = false;
        private bool _PlayerIsHoldingGolden = false;
        public void SetNewRoom(string newRoomName, bool countDeath=true, bool holdingGolden=false) {
            _PlayerIsHoldingGolden = holdingGolden;

            if (PreviousRoomName == newRoomName && !_CurrentRoomCompleted) { //Don't complete if entering previous room and current room was not completed
                Log($"[SetNewRoom] Entered previous room '{PreviousRoomName}'");
                PreviousRoomName = CurrentRoomName;
                CurrentRoomName = newRoomName;
                CurrentChapterStats?.SetCurrentRoom(newRoomName);
                SaveChapterStats();
                return;
            }


            Log($"[SetNewRoom] Entered new room '{newRoomName}' | Holding golden: '{holdingGolden}'");

            PreviousRoomName = CurrentRoomName;
            CurrentRoomName = newRoomName;
            _CurrentRoomCompleted = false;

            if (DoRecordPath) {
                Path.AddRoom(newRoomName);
            }

            if (ModSettings.Enabled && CurrentChapterStats != null) {
                if (countDeath && !ModSettings.PauseDeathTracking && (!ModSettings.OnlyTrackWithGoldenBerry || holdingGolden)) {
                    CurrentChapterStats.AddAttempt(true);
                }
                CurrentChapterStats.SetCurrentRoom(newRoomName);
                SaveChapterStats();
            }
        }

        private void SetRoomCompleted(bool resetOnDeath=false) {
            _CurrentRoomCompleted = true;
            _CurrentRoomCompletedResetOnDeath = resetOnDeath;
        }

        string baseFolderPath = "./ConsistencyTracker/";
        public string GetPathToFile(string file) {
            return baseFolderPath + file;
        }
        public string GetPathToFolder(string folder) {
            return baseFolderPath + folder + "/";
        }
        public void CheckFolderExists(string folderPath) {
            if (!Directory.Exists(folderPath)) {
                Directory.CreateDirectory(folderPath);
            }
        }


        public bool PathInfoExists() {
            string path = GetPathToFile($"paths/{CurrentChapterName}.txt");
            return File.Exists(path);
        }
        public PathInfo GetPathInputInfo() {
            Log($"[GetPathInputInfo] Fetching path info for chapter '{CurrentChapterName}'");

            string path = GetPathToFile($"paths/{CurrentChapterName}.txt");
            Log($"\tSearching for path '{path}'");

            if (File.Exists(path)) { //Parse File
                Log($"\tFound file, parsing...");
                string content = File.ReadAllText(path);

                try {
                    return PathInfo.ParseString(content, Log);
                } catch (Exception) {
                    Log($"\tCouldn't read old path info, created new PathInfo. Old path info content:\n{content}");
                    PathInfo toRet = new PathInfo() { };
                    return toRet;
                }

            } else { //Create new
                Log($"\tDidn't find file, created new PathInfo.");
                PathInfo toRet = new PathInfo() {};
                return toRet;
            }
        }

        public ChapterStats GetCurrentChapterStats() {
            string path = GetPathToFile($"stats/{CurrentChapterName}.txt");

            bool hasEnteredThisSession = ChaptersThisSession.Contains(CurrentChapterName);
            ChaptersThisSession.Add(CurrentChapterName);
            Log($"[GetCurrentChapterStats] CurrentChapterName: '{CurrentChapterName}', hasEnteredThisSession: '{hasEnteredThisSession}', ChaptersThisSession: '{string.Join(", ", ChaptersThisSession)}'");

            ChapterStats toRet;

            if (File.Exists(path)) { //Parse File
                string content = File.ReadAllText(path);
                toRet = ChapterStats.ParseString(content);
                toRet.ChapterName = CurrentChapterName;

            } else { //Create new
                toRet = new ChapterStats() {
                    ChapterName = CurrentChapterName,
                };
                toRet.SetCurrentRoom(CurrentRoomName);
            }

            if (!hasEnteredThisSession) {
                toRet.ResetCurrentSession();
                Log("Resetting session for GB deaths");
            } else {
                Log("Not resetting session for GB deaths");
            }

            return toRet;
        }

        public void SaveChapterStats() {
            if (CurrentChapterStats == null) {
                Log($"[SaveChapterStats] Aborting saving chapter stats as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            string path = GetPathToFile($"stats/{CurrentChapterName}.txt");
            File.WriteAllText(path, CurrentChapterStats.ToChapterStatsString());

            string modStatePath = GetPathToFile($"stats/modState.txt");

            string content = $"{CurrentChapterStats.CurrentRoom}\n{CurrentChapterStats.ChapterName};{ModSettings.PauseDeathTracking};{ModSettings.RecordPath};{OverlayVersion};{_PlayerIsHoldingGolden}\n";
            File.WriteAllText(modStatePath, content);
        }

        public void WipeChapterData() {
            if (CurrentChapterStats == null) {
                Log($"[WipeChapterData] Aborting wiping chapter data as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            Log($"[WipeChapterData] Wiping death data for chapter '{CurrentChapterName}'");

            RoomStats currentRoom = CurrentChapterStats.CurrentRoom;
            List<string> toRemove = new List<string>();

            foreach (string debugName in CurrentChapterStats.Rooms.Keys) {
                if (debugName == currentRoom.DebugRoomName) continue;
                toRemove.Add(debugName);
            }

            foreach (string debugName in toRemove) {
                CurrentChapterStats.Rooms.Remove(debugName);
            }

            WipeRoomData();
        }

        public void WipeChapterGoldenBerryDeaths() {
            if (CurrentChapterStats == null) {
                Log($"[WipeChapterGoldenBerryDeaths] Aborting wiping chapter data as '{nameof(CurrentChapterStats)}' is null");
                return;
            }

            Log($"[WipeChapterGoldenBerryDeaths] Wiping golden berry death data for chapter '{CurrentChapterName}'");

            foreach (string debugName in CurrentChapterStats.Rooms.Keys) {
                CurrentChapterStats.Rooms[debugName].GoldenBerryDeaths = 0;
                CurrentChapterStats.Rooms[debugName].GoldenBerryDeathsThisSession = 0;
            }

            SaveChapterStats();
        }

        public void WipeRoomData() {
            if (CurrentChapterStats == null) {
                Log($"[WipeRoomData] Aborting wiping room data as '{nameof(CurrentChapterStats)}' is null");
                return;
            }
            Log($"[WipeRoomData] Wiping room data for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'");

            CurrentChapterStats.CurrentRoom.PreviousAttempts.Clear();
            SaveChapterStats();
        }

        public void RemoveLastDeathStreak() {
            if (CurrentChapterStats == null) {
                Log($"[RemoveLastDeathStreak] Aborting removing death streak as '{nameof(CurrentChapterStats)}' is null");
                return;
            }
            Log($"[RemoveLastDeathStreak] Removing death streak for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'");

            while (CurrentChapterStats.CurrentRoom.PreviousAttempts.Count > 0 && CurrentChapterStats.CurrentRoom.LastAttempt == false) {
                CurrentChapterStats.CurrentRoom.RemoveLastAttempt();
            }

            SaveChapterStats();
        }

        public void RemoveLastAttempt() {
            if (CurrentChapterStats == null) {
                Log($"[RemoveLastAttempt] Aborting removing death streak as '{nameof(CurrentChapterStats)}' is null");
                return;
            }
            Log($"[RemoveLastAttempt] Removing last attempt for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'");

            CurrentChapterStats.CurrentRoom.RemoveLastAttempt();
            SaveChapterStats();
        }

        public void SaveRecordedRoomPath() {
            Log($"[{nameof(SaveRecordedRoomPath)}] Saving recorded path...");
            DisabledInRoomName = CurrentRoomName;
            CurrentChapterPath = Path.ToPathInfo();
            Log($"[{nameof(SaveRecordedRoomPath)}] Recorded path:\n{CurrentChapterPath.ToString()}");
            SaveRoomPath();
        }
        public void SaveRoomPath() {
            string relativeOutPath = $"paths/{CurrentChapterName}.txt";
            string outPath = GetPathToFile(relativeOutPath);
            File.WriteAllText(outPath, CurrentChapterPath.ToString());
            Log($"Wrote path data to '{relativeOutPath}'");
        }

        public void RemoveRoomFromChapter() {
            if (CurrentChapterPath == null) {
                Log($"[RemoveRoomFromChapter] CurrentChapterPath was null");
                return;
            }

            bool foundRoom = false;
            foreach (CheckpointInfo cpInfo in CurrentChapterPath.Checkpoints) {
                foreach (RoomInfo rInfo in cpInfo.Rooms) {
                    if (rInfo.DebugRoomName != CurrentRoomName) continue;

                    cpInfo.Rooms.Remove(rInfo);
                    foundRoom = true;
                    break;
                }

                if (foundRoom) break;
            }

            if (foundRoom) {
                SaveRoomPath();
            }
        }


        public void CreateChapterSummary(int attemptCount) {
            Log($"[CreateChapterSummary(attemptCount={attemptCount})] Attempting to create tracker summary");

            bool hasPathInfo = PathInfoExists();

            string relativeOutPath = $"summaries/{CurrentChapterName}.txt";
            string outPath = GetPathToFile(relativeOutPath);

            if (!hasPathInfo) {
                Log($"Called CreateChapterSummary without chapter path info. Please create a path before using this feature");
                File.WriteAllText(outPath, "No path info was found for the current chapter.\nPlease create a path before using the summary feature");
                return;
            }

            CurrentChapterStats?.OutputSummary(outPath, CurrentChapterPath, attemptCount);
        }

        private bool PlayerIsHoldingGoldenBerry(Player player) {
            if (player == null || player.Leader == null || player.Leader.Followers == null)
                return false;

            return player.Leader.Followers.Any((f) => {
                if (!(f.Entity is Strawberry))
                    return false;

                Strawberry berry = (Strawberry)f.Entity;

                if (!berry.Golden || berry.Winged)
                    return false;

                return true;
            });
        }


        private static readonly int LOG_FILE_COUNT = 10;
        public void LogInit() {
            string logFileMax = GetPathToFile($"logs/log_old{LOG_FILE_COUNT}.txt");
            if (File.Exists(logFileMax)) {
                File.Delete(logFileMax);
            }

            for (int i = LOG_FILE_COUNT - 1; i >= 1; i--) {
                string logFilePath = GetPathToFile($"logs/log_old{i}.txt");
                if (File.Exists(logFilePath)) {
                    string logFileNewPath = GetPathToFile($"logs/log_old{i+1}.txt");
                    File.Move(logFilePath, logFileNewPath);
                }
            }

            string lastFile = GetPathToFile("logs/log.txt");
            if (File.Exists(lastFile)) {
                string logFileNewPath = GetPathToFile($"logs/log_old{1}.txt");
                File.Move(lastFile, logFileNewPath);
            }
        }
        public void Log(string log) {
            string path = GetPathToFile("logs/log.txt");
            File.AppendAllText(path, log+"\n");
        }
    }
}
