using Celeste.Mod.ConsistencyTracker.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.ConsistencyTracker.ThirdParty;
using Celeste.Mod.ConsistencyTracker.Entities;
using Celeste.Mod.ConsistencyTracker.Stats;
using Celeste.Mod.ConsistencyTracker.Enums;

namespace Celeste.Mod.ConsistencyTracker
{
    public class ConsistencyTrackerModule : EverestModule
    {
        public static ConsistencyTrackerModule Instance;

        public static readonly string OverlayVersion = "1.1.1";
        public static readonly string ModVersion = "1.3.6";

        public override Type SettingsType => typeof(ConsistencyTrackerSettings);
        public ConsistencyTrackerSettings ModSettings => (ConsistencyTrackerSettings)this._Settings;
        static readonly string BaseFolderPath = "./ConsistencyTracker/";

        private bool DidRestart = false;
        private HashSet<string> ChaptersThisSession = new();

        #region Path Recording Variables

        public bool DoRecordPath
        {
            get => _DoRecordPath;
            set
            {
                if (value)
                {
                    if (DisabledInRoomName != CurrentRoomName)
                    {
                        Path = new PathRecorder();
                        InsertCheckpointIntoPath(null, LastRoomWithCheckpoint);
                        Path.AddRoom(CurrentRoomName);
                    }
                }
                else
                {
                    SaveRecordedRoomPath();
                }

                _DoRecordPath = value;
            }
        }
        private bool _DoRecordPath = false;
        private PathRecorder Path;
        private string DisabledInRoomName;

        #endregion

        #region State Variables

        public PathInfo CurrentChapterPath;
        public ChapterStats CurrentChapterStats;

        private string CurrentChapterDebugName;
        private string PreviousRoomName;
        private string CurrentRoomName;

        private string LastRoomWithCheckpoint = null;

        private bool _CurrentRoomCompleted = false;
        private bool _CurrentRoomCompletedResetOnDeath = false;
        private bool _PlayerIsHoldingGolden = false;

        #endregion

        public StatManager StatsManager;

        public ConsistencyTrackerModule()
        {
            Instance = this;
        }

        #region Load/Unload Stuff

        public override void Load()
        {
            CheckFolderExists(BaseFolderPath);
            CheckFolderExists(GetPathToFolder("paths"));
            CheckFolderExists(GetPathToFolder("stats"));
            CheckFolderExists(GetPathToFolder("logs"));
            CheckFolderExists(GetPathToFolder("summaries"));

            HookStuff();

            StatsManager = new StatManager();
        }

        private void HookStuff()
        {
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
            On.Celeste.Strawberry.OnCollect += Strawberry_OnCollect; //doesnt work :(
            On.Celeste.Strawberry.OnPlayer += Strawberry_OnPlayer; //sorta works, but triggers very often for a single berry

            //Changing lava/ice in Core
            On.Celeste.CoreModeToggle.OnChangeMode += CoreModeToggle_OnChangeMode; //works

            //Picking up a Cassette tape
            On.Celeste.Cassette.OnPlayer += Cassette_OnPlayer; //works

            //Open up key doors?
            //On.Celeste.Door.Open += Door_Open; //Wrong door (those are the resort doors)
            On.Celeste.LockBlock.TryOpen += LockBlock_TryOpen; //works
        }

        private void UnHookStuff()
        {
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
            if (Everest.Modules.Any(m => m.Metadata.Name == "SpeedrunTool"))
            {
                SpeedrunToolSupport.Load();
            }
        }

        public override void Unload()
        {
            UnHookStuff();
        }

        #endregion

        #region Hooks

        private void LockBlock_TryOpen(
            On.Celeste.LockBlock.orig_TryOpen orig,
            LockBlock self,
            Player player,
            Follower fol
        )
        {
            orig(self, player, fol);
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(LockBlock_TryOpen)}",
                "Opened a door"
            );
            SetRoomCompleted(resetOnDeath: false);
        }

        private DashCollisionResults ClutterSwitch_OnDashed(
            On.Celeste.ClutterSwitch.orig_OnDashed orig,
            ClutterSwitch self,
            Player player,
            Vector2 direction
        )
        {
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(ClutterSwitch_OnDashed)}",
                $"Activated a clutter switch"
            );
            SetRoomCompleted(resetOnDeath: false);
            return orig(self, player, direction);
        }

        private void Key_OnPlayer(On.Celeste.Key.orig_OnPlayer orig, Key self, Player player)
        {
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Key_OnPlayer)}",
                $"Picked up a key"
            );
            orig(self, player);
            SetRoomCompleted(resetOnDeath: false);
        }

        private void Cassette_OnPlayer(
            On.Celeste.Cassette.orig_OnPlayer orig,
            Cassette self,
            Player player
        )
        {
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Cassette_OnPlayer)}",
                $"Collected a cassette tape"
            );
            orig(self, player);
            SetRoomCompleted(resetOnDeath: false);
        }

        private readonly List<EntityID> TouchedBerries = new();

        // All touched berries need to be reset on death, since they either:
        // - already collected
        // - disappeared on death
        private void Strawberry_OnPlayer(
            On.Celeste.Strawberry.orig_OnPlayer orig,
            Strawberry self,
            Player player
        )
        {
            orig(self, player);

            if (TouchedBerries.Contains(self.ID))
                return; //to not spam the log
            TouchedBerries.Add(self.ID);

            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Strawberry_OnPlayer)}",
                $"Strawberry on player"
            );
            SetRoomCompleted(resetOnDeath: true);
        }

        private void Strawberry_OnCollect(
            On.Celeste.Strawberry.orig_OnCollect orig,
            Strawberry self
        )
        {
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Strawberry_OnCollect)}",
                $"Collected a strawberry"
            );
            orig(self);
            SetRoomCompleted(resetOnDeath: false);
        }

        private void CoreModeToggle_OnChangeMode(
            On.Celeste.CoreModeToggle.orig_OnChangeMode orig,
            CoreModeToggle self,
            Session.CoreModes mode
        )
        {
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(CoreModeToggle_OnChangeMode)}",
                $"Changed core mode to '{mode}'"
            );
            orig(self, mode);
            SetRoomCompleted(resetOnDeath: true);
        }

        private void Checkpoint_TurnOn(
            On.Celeste.Checkpoint.orig_TurnOn orig,
            Checkpoint cp,
            bool animate
        )
        {
            orig(cp, animate);
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Checkpoint_TurnOn)}",
                $"cp.Position={cp.Position}, LastRoomWithCheckpoint={LastRoomWithCheckpoint}"
            );
            if (ModSettings.Enabled && DoRecordPath)
            {
                InsertCheckpointIntoPath(cp, LastRoomWithCheckpoint);
            }
        }

        //Not triggered when teleporting via debug map
        private void Level_TeleportTo(
            On.Celeste.Level.orig_TeleportTo orig,
            Level level,
            Player player,
            string nextLevel,
            Player.IntroTypes introType,
            Vector2? nearestSpawn
        )
        {
            orig(level, player, nextLevel, introType, nearestSpawn);
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Level_TeleportTo)}",
                $"level.Session.LevelData.Name={SanitizeRoomName(level.Session.LevelData.Name)}"
            );
        }

        private void Level_OnLoadLevel(
            Level level,
            Player.IntroTypes playerIntro,
            bool isFromLoader
        )
        {
            string newCurrentRoom = SanitizeRoomName(level.Session.LevelData.Name);
            bool holdingGolden = PlayerIsHoldingGoldenBerry(level.Tracker.GetEntity<Player>());

            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Level_OnLoadLevel)}",
                $"level.Session.LevelData.Name={newCurrentRoom}, playerIntro={playerIntro} | CurrentRoomName: '{CurrentRoomName}', PreviousRoomName: '{PreviousRoomName}'"
            );
            if (playerIntro == Player.IntroTypes.Respawn)
            { //Changing room via golden berry death or debug map teleport
                if (CurrentRoomName != null && newCurrentRoom != CurrentRoomName)
                {
                    SetNewRoom(newCurrentRoom, false, holdingGolden);
                }
            }

            if (DidRestart)
            {
                Logger.Log(
                    LogLevel.Debug,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Level_OnLoadLevel)}",
                    $"\tRequested reset of PreviousRoomName to null"
                );
                DidRestart = false;
                SetNewRoom(newCurrentRoom, false, holdingGolden);
                PreviousRoomName = null;
            }

            if (isFromLoader)
            {
                level.Add(new RoomOverlay());
            }
        }

        private void Level_OnExit(
            Level level,
            LevelExit exit,
            LevelExit.Mode mode,
            Session session,
            HiresSnow snow
        )
        {
            Logger.Log(
                LogLevel.Debug,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Level_OnExit)}",
                $"mode={mode}, snow={snow}"
            );
            if (mode == LevelExit.Mode.Restart)
            {
                DidRestart = true;
            }
            else if (mode == LevelExit.Mode.GoldenBerryRestart)
            {
                DidRestart = true;

                if (ModSettings.Enabled && !ModSettings.PauseDeathTracking)
                { //Only count golden berry deaths when enabled
                    CurrentChapterStats?.AddGoldenBerryDeath();
                    if (ModSettings.OnlyTrackWithGoldenBerry)
                    {
                        CurrentChapterStats.AddAttempt(false);
                    }
                }
            }

            if (DoRecordPath)
            {
                DoRecordPath = false;
                ModSettings.RecordPath = false;
            }
        }

        private void Level_OnComplete(Level level)
        {
            Logger.Log(
                LogLevel.Debug,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Level_OnComplete)}",
                $"Incrementing {CurrentChapterStats?.CurrentRoom.DebugRoomName}"
            );
            if (ModSettings.Enabled && !ModSettings.PauseDeathTracking)
                CurrentChapterStats?.AddAttempt(true);
            SaveChapterStats();
        }

        private void Level_Begin(On.Celeste.Level.orig_Begin orig, Level level)
        {
            Logger.Log(
                LogLevel.Debug,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Level_Begin)}",
                $"Calling ChangeChapter with 'level.Session'"
            );
            ChangeChapter(level.Session);
            orig(level);
        }

        private void Level_OnTransitionTo(Level level, LevelData levelDataNext, Vector2 direction)
        {
            if (levelDataNext.HasCheckpoint)
            {
                LastRoomWithCheckpoint = levelDataNext.Name;
            }

            string roomName = SanitizeRoomName(levelDataNext.Name);
            Logger.Log(
                LogLevel.Debug,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Level_OnTransitionTo)}",
                $"levelData.Name->{roomName}, level.Completed->{level.Completed}, level.NewLevel->{level.NewLevel}, level.Session.StartCheckpoint->{level.Session.StartCheckpoint}"
            );
            bool holdingGolden = PlayerIsHoldingGoldenBerry(level.Tracker.GetEntity<Player>());
            SetNewRoom(roomName, true, holdingGolden);
        }

        private void Player_OnDie(Player player)
        {
            TouchedBerries.Clear();
            bool holdingGolden = PlayerIsHoldingGoldenBerry(player);

            Logger.Log(
                LogLevel.Debug,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(Player_OnDie)}",
                $"Player died. (holdingGolden: {holdingGolden})"
            );
            if (_CurrentRoomCompletedResetOnDeath)
            {
                _CurrentRoomCompleted = false;
            }

            if (ModSettings.Enabled)
            {
                if (
                    !ModSettings.PauseDeathTracking
                    && (!ModSettings.OnlyTrackWithGoldenBerry || holdingGolden)
                )
                    CurrentChapterStats?.AddAttempt(false);
                SaveChapterStats();
            }
        }

        #endregion

        #region State Management

        private string SanitizeRoomName(string name)
        {
            name = name.Replace(";", "");
            return name;
        }

        private void ChangeChapter(Session session)
        {
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(ChangeChapter)}",
                "Called chapter change"
            );
            AreaData area = AreaData.Areas[session.Area.ID];
            string chapName = area.Name;
            string chapNameClean = chapName.DialogCleanOrNull() ?? chapName.SpacedPascalCase();
            string campaignName = DialogExt.CleanLevelSet(area.GetLevelSet());

            Logger.Log(
                LogLevel.Debug,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(ChangeChapter)}",
                $"Level->{session.Level}, session.Area.GetSID()->{session.Area.GetSID()}, session.Area.Mode->{session.Area.Mode}, chapterNameClean->{chapNameClean}, campaignName->{campaignName}"
            );

            CurrentChapterDebugName = ($"{session.MapData.Data.SID}_{session.Area.Mode}").Replace(
                "/",
                "_"
            );

            //string test2 = Dialog.Get($"luma_farewellbb_FarewellBB_b_intro");
            //Log($"[ChangeChapter] Dialog Test 2: {test2}");

            PreviousRoomName = null;
            CurrentRoomName = session.Level;

            CurrentChapterPath = GetPathInputInfo();
            CurrentChapterStats = GetCurrentChapterStats();

            CurrentChapterStats.ChapterSID = session.MapData.Data.SID;
            CurrentChapterStats.ChapterSIDDialogSanitized = SanitizeSIDForDialog(
                session.MapData.Data.SID
            );
            CurrentChapterStats.ChapterName = chapNameClean;
            CurrentChapterStats.CampaignName = campaignName;

            TouchedBerries.Clear();

            SetNewRoom(CurrentRoomName, false, false);
            if (session.LevelData.HasCheckpoint)
            {
                LastRoomWithCheckpoint = CurrentRoomName;
            }
            else
            {
                LastRoomWithCheckpoint = null;
            }

            if (!DoRecordPath && ModSettings.RecordPath)
            {
                DoRecordPath = true;
            }
        }

        public void SetNewRoom(
            string newRoomName,
            bool countDeath = true,
            bool holdingGolden = false
        )
        {
            _PlayerIsHoldingGolden = holdingGolden;

            if (PreviousRoomName == newRoomName && !_CurrentRoomCompleted)
            { //Don't complete if entering previous room and current room was not completed
                Logger.Log(
                    LogLevel.Info,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(SetNewRoom)}",
                    $"Entered previous room '{PreviousRoomName}'"
                );
                PreviousRoomName = CurrentRoomName;
                CurrentRoomName = newRoomName;
                CurrentChapterStats?.SetCurrentRoom(newRoomName);
                SaveChapterStats();
                return;
            }

            Logger.Log(
                LogLevel.Debug,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(SetNewRoom)}",
                $"Entered new room '{newRoomName}' | Holding golden: '{holdingGolden}'"
            );

            PreviousRoomName = CurrentRoomName;
            CurrentRoomName = newRoomName;
            _CurrentRoomCompleted = false;

            if (DoRecordPath)
            {
                Path.AddRoom(newRoomName);
            }

            if (ModSettings.Enabled && CurrentChapterStats != null)
            {
                if (
                    countDeath
                    && !ModSettings.PauseDeathTracking
                    && (!ModSettings.OnlyTrackWithGoldenBerry || holdingGolden)
                )
                {
                    CurrentChapterStats.AddAttempt(true);
                }
                CurrentChapterStats.SetCurrentRoom(newRoomName);
                SaveChapterStats();
            }
        }

        private void SetRoomCompleted(bool resetOnDeath = false)
        {
            _CurrentRoomCompleted = true;
            _CurrentRoomCompletedResetOnDeath = resetOnDeath;
        }

        private bool PlayerIsHoldingGoldenBerry(Player player)
        {
            if (player == null || player.Leader == null || player.Leader.Followers == null)
                return false;

            return player.Leader.Followers.Any(
                (f) =>
                {
                    if (f.Entity is not Strawberry)
                        return false;

                    Strawberry berry = (Strawberry)f.Entity;

                    if (!berry.Golden || berry.Winged)
                        return false;

                    return true;
                }
            );
        }

        #region Speedrun Tool Save States

        public void SpeedrunToolSaveState(
            Dictionary<Type, Dictionary<string, object>> savedvalues,
            Level level
        )
        {
            Type type = GetType();
            if (!savedvalues.ContainsKey(type))
            {
                savedvalues.Add(type, new Dictionary<string, object>());
                savedvalues[type].Add(nameof(PreviousRoomName), PreviousRoomName);
                savedvalues[type].Add(nameof(CurrentRoomName), CurrentRoomName);
                savedvalues[type].Add(nameof(_CurrentRoomCompleted), _CurrentRoomCompleted);
                savedvalues[type].Add(
                    nameof(_CurrentRoomCompletedResetOnDeath),
                    _CurrentRoomCompletedResetOnDeath
                );
            }
            else
            {
                savedvalues[type][nameof(PreviousRoomName)] = PreviousRoomName;
                savedvalues[type][nameof(CurrentRoomName)] = CurrentRoomName;
                savedvalues[type][nameof(_CurrentRoomCompleted)] = _CurrentRoomCompleted;
                savedvalues[type][nameof(_CurrentRoomCompletedResetOnDeath)] =
                    _CurrentRoomCompletedResetOnDeath;
            }
        }

        public void SpeedrunToolLoadState(
            Dictionary<Type, Dictionary<string, object>> savedvalues,
            Level level
        )
        {
            Type type = GetType();
            if (!savedvalues.ContainsKey(type))
            {
                Logger.Log(
                    LogLevel.Info,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(SpeedrunToolLoadState)}",
                    "Trying to load state without prior saving a state..."
                );
                return;
            }

            PreviousRoomName = (string)savedvalues[type][nameof(PreviousRoomName)];
            CurrentRoomName = (string)savedvalues[type][nameof(CurrentRoomName)];
            _CurrentRoomCompleted = (bool)savedvalues[type][nameof(_CurrentRoomCompleted)];
            _CurrentRoomCompletedResetOnDeath = (bool)
                savedvalues[type][nameof(_CurrentRoomCompletedResetOnDeath)];

            CurrentChapterStats.SetCurrentRoom(CurrentRoomName);
            SaveChapterStats();
        }

        public void SpeedrunToolClearState()
        {
            //No action
        }

        #endregion
        #endregion

        #region Data Import/Export

        public static string GetPathToFile(string file) => BaseFolderPath + file;

        public static string GetPathToFolder(string folder) => $"{BaseFolderPath}{folder}/";

        public static void CheckFolderExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        public bool PathInfoExists()
        {
            string path = GetPathToFile($"paths/{CurrentChapterDebugName}.txt");
            return File.Exists(path);
        }

        public PathInfo GetPathInputInfo()
        {
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(GetPathInputInfo)}",
                $"Fetching path info for chapter '{CurrentChapterDebugName}'"
            );

            string path = GetPathToFile($"paths/{CurrentChapterDebugName}.txt");
            Logger.Log(
                LogLevel.Debug,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(GetPathInputInfo)}",
                $"\tSearching for path '{path}'"
            );

            if (File.Exists(path))
            { //Parse File
                Logger.Log(
                    LogLevel.Debug,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(GetPathInputInfo)}",
                    "\tFound file, parsing..."
                );
                string content = File.ReadAllText(path);

                try
                {
                    return PathInfo.ParseString(content);
                }
                catch (Exception)
                {
                    Logger.Log(
                        LogLevel.Error,
                        $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(GetPathInputInfo)}",
                        $"\tCouldn't read old path info, created new PathInfo. Old path info content:\n{content}"
                    );
                    PathInfo toRet = new();
                    return toRet;
                }
            }
            else
            { //Create new
                Logger.Log(
                    LogLevel.Debug,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(GetPathInputInfo)}",
                    $"\tDidn't find file, returned null."
                );
                return null;
            }
        }

        public ChapterStats GetCurrentChapterStats()
        {
            string path = GetPathToFile($"stats/{CurrentChapterDebugName}.txt");

            bool hasEnteredThisSession = ChaptersThisSession.Contains(CurrentChapterDebugName);
            ChaptersThisSession.Add(CurrentChapterDebugName);
            Logger.Log(
                LogLevel.Verbose,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(GetCurrentChapterStats)}",
                $"CurrentChapterName: '{CurrentChapterDebugName}', hasEnteredThisSession: '{hasEnteredThisSession}', ChaptersThisSession: '{string.Join(", ", ChaptersThisSession)}'"
            );

            ChapterStats toRet;

            if (File.Exists(path))
            { //Parse File
                string content = File.ReadAllText(path);
                toRet = ChapterStats.ParseString(content);
                toRet.ChapterDebugName = CurrentChapterDebugName;
            }
            else
            { //Create new
                toRet = new ChapterStats() { ChapterDebugName = CurrentChapterDebugName, };
                toRet.SetCurrentRoom(CurrentRoomName);
            }

            if (!hasEnteredThisSession)
            {
                toRet.ResetCurrentSession();
                Logger.Log(
                    LogLevel.Info,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(GetCurrentChapterStats)}",
                    "Resetting session for GB deaths"
                );
            }
            else
            {
                Logger.Log(
                    LogLevel.Debug,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(GetCurrentChapterStats)}",
                    "Not resetting session for GB deaths"
                );
            }

            return toRet;
        }

        public void SaveChapterStats()
        {
            if (CurrentChapterStats == null)
            {
                Logger.Log(
                    LogLevel.Warn,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(SaveChapterStats)}",
                    $"Aborting saving chapter stats as '{nameof(CurrentChapterStats)}' is null"
                );
                return;
            }

            CurrentChapterStats.ModState.PlayerIsHoldingGolden = _PlayerIsHoldingGolden;
            CurrentChapterStats.ModState.DeathTrackingPaused = ModSettings.PauseDeathTracking;
            CurrentChapterStats.ModState.RecordingPath = ModSettings.RecordPath;
            CurrentChapterStats.ModState.OverlayVersion = OverlayVersion;
            CurrentChapterStats.ModState.ModVersion = ModVersion;

            string path = GetPathToFile($"stats/{CurrentChapterDebugName}.txt");
            File.WriteAllText(path, CurrentChapterStats.ToChapterStatsString());

            string modStatePath = GetPathToFile($"stats/modState.txt");

            string content =
                $"{CurrentChapterStats.CurrentRoom}\n{CurrentChapterStats.ChapterDebugName};{CurrentChapterStats.ModState}\n";
            File.WriteAllText(modStatePath, content);

            StatsManager.OutputFormats(CurrentChapterPath, CurrentChapterStats);
        }

        public void CreateChapterSummary(int attemptCount)
        {
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(CreateChapterSummary)}",
                $"[attemptCount={attemptCount}] Attempting to create tracker summary"
            );

            bool hasPathInfo = PathInfoExists();

            string relativeOutPath = $"summaries/{CurrentChapterDebugName}.txt";
            string outPath = GetPathToFile(relativeOutPath);

            if (!hasPathInfo)
            {
                Logger.Log(
                    LogLevel.Info,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(CreateChapterSummary)}",
                    $"Called CreateChapterSummary without chapter path info. Please create a path before using this feature"
                );
                File.WriteAllText(
                    outPath,
                    "No path info was found for the current chapter.\nPlease create a path before using the summary feature"
                );
                return;
            }

            CurrentChapterStats?.OutputSummary(outPath, CurrentChapterPath, attemptCount);
        }

        #endregion

        #region Stats Data Control

        public void WipeChapterData()
        {
            if (CurrentChapterStats == null)
            {
                Logger.Log(
                    LogLevel.Warn,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(WipeChapterData)}",
                    $"Aborting wiping chapter data as '{nameof(CurrentChapterStats)}' is null"
                );
                return;
            }

            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(WipeChapterData)}",
                $"Wiping death data for chapter '{CurrentChapterDebugName}'"
            );

            RoomStats currentRoom = CurrentChapterStats.CurrentRoom;
            List<string> toRemove = new();

            foreach (string debugName in CurrentChapterStats.Rooms.Keys)
            {
                if (debugName == currentRoom.DebugRoomName)
                    continue;
                toRemove.Add(debugName);
            }

            foreach (string debugName in toRemove)
            {
                CurrentChapterStats.Rooms.Remove(debugName);
            }

            WipeRoomData();
        }

        public void RemoveRoomGoldenBerryDeaths()
        {
            if (CurrentChapterStats == null)
            {
                Logger.Log(
                    LogLevel.Warn,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(RemoveRoomGoldenBerryDeaths)}",
                    $"Aborting wiping room golden berry deaths as '{nameof(CurrentChapterStats)}' is null"
                );
                return;
            }

            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(RemoveRoomGoldenBerryDeaths)}",
                $"Wiping golden berry death data for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'"
            );

            CurrentChapterStats.CurrentRoom.GoldenBerryDeaths = 0;
            CurrentChapterStats.CurrentRoom.GoldenBerryDeathsSession = 0;

            SaveChapterStats();
        }

        public void WipeChapterGoldenBerryDeaths()
        {
            if (CurrentChapterStats == null)
            {
                Logger.Log(
                    LogLevel.Warn,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(WipeChapterGoldenBerryDeaths)}",
                    $"Aborting wiping chapter golden berry deaths as '{nameof(CurrentChapterStats)}' is null"
                );
                return;
            }

            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(WipeChapterGoldenBerryDeaths)}",
                $"Wiping golden berry death data for chapter '{CurrentChapterDebugName}'"
            );

            foreach (string debugName in CurrentChapterStats.Rooms.Keys)
            {
                CurrentChapterStats.Rooms[debugName].GoldenBerryDeaths = 0;
                CurrentChapterStats.Rooms[debugName].GoldenBerryDeathsSession = 0;
            }

            SaveChapterStats();
        }

        public void WipeRoomData()
        {
            if (CurrentChapterStats == null)
            {
                Logger.Log(
                    LogLevel.Warn,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(WipeRoomData)}",
                    $"Aborting wiping room data as '{nameof(CurrentChapterStats)}' is null"
                );
                return;
            }
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(WipeRoomData)}",
                $"Wiping room data for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'"
            );

            CurrentChapterStats.CurrentRoom.PreviousAttempts.Clear();
            SaveChapterStats();
        }

        public void RemoveLastDeathStreak()
        {
            if (CurrentChapterStats == null)
            {
                Logger.Log(
                    LogLevel.Warn,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(RemoveLastDeathStreak)}",
                    $"Aborting removing death streak as '{nameof(CurrentChapterStats)}' is null"
                );
                return;
            }
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(RemoveLastDeathStreak)}",
                $"Removing death streak for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'"
            );

            while (
                CurrentChapterStats.CurrentRoom.PreviousAttempts.Count > 0
                && CurrentChapterStats.CurrentRoom.LastAttempt == false
            )
            {
                CurrentChapterStats.CurrentRoom.RemoveLastAttempt();
            }

            SaveChapterStats();
        }

        public void RemoveLastAttempt()
        {
            if (CurrentChapterStats == null)
            {
                Logger.Log(
                    LogLevel.Warn,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(RemoveLastAttempt)}",
                    $"Aborting removing death streak as '{nameof(CurrentChapterStats)}' is null"
                );
                return;
            }
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(RemoveLastAttempt)}",
                $"Removing last attempt for room '{CurrentChapterStats.CurrentRoom.DebugRoomName}'"
            );

            CurrentChapterStats.CurrentRoom.RemoveLastAttempt();
            SaveChapterStats();
        }

        #endregion

        #region Path Management

        public void SaveRecordedRoomPath()
        {
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(SaveRecordedRoomPath)}",
                $"Saving recorded path..."
            );
            DisabledInRoomName = CurrentRoomName;
            CurrentChapterPath = Path.ToPathInfo();
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(SaveRecordedRoomPath)}",
                $"Recorded path:\n{CurrentChapterPath}"
            );
            SaveRoomPath();
        }

        public void SaveRoomPath()
        {
            string relativeOutPath = $"paths/{CurrentChapterDebugName}.txt";
            string outPath = GetPathToFile(relativeOutPath);
            File.WriteAllText(outPath, CurrentChapterPath.ToString());
            Logger.Log(
                LogLevel.Info,
                $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(SaveRoomPath)}",
                $"Wrote path data to '{relativeOutPath}'"
            );
        }

        public void RemoveRoomFromChapter()
        {
            if (CurrentChapterPath == null)
            {
                Logger.Log(
                    LogLevel.Warn,
                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerModule)}/{nameof(RemoveRoomFromChapter)}",
                    $"CurrentChapterPath was null"
                );
                return;
            }

            bool foundRoom = false;
            foreach (CheckpointInfo cpInfo in CurrentChapterPath.Checkpoints)
            {
                foreach (RoomInfo rInfo in cpInfo.Rooms)
                {
                    if (rInfo.DebugRoomName != CurrentRoomName)
                        continue;

                    cpInfo.Rooms.Remove(rInfo);
                    foundRoom = true;
                    break;
                }

                if (foundRoom)
                    break;
            }

            if (foundRoom)
            {
                SaveRoomPath();
            }
        }

        #endregion

        #region Util
        public static string SanitizeSIDForDialog(string sid)
        {
            string bsSuffix = "_pqeijpvqie";
            string dialogCleaned = Dialog.Get($"{sid}{bsSuffix}");
            return dialogCleaned.Substring(1, sid.Length);
        }

        public void InsertCheckpointIntoPath(Checkpoint cp, string roomName)
        {
            if (roomName == null)
            {
                Path.AddCheckpoint(cp, PathRecorder.DefaultCheckpointName);
                return;
            }

            string cpDialogName = $"{CurrentChapterStats.ChapterSIDDialogSanitized}_{roomName}";
            //Log($"[Dialog Testing] cpDialogName: {cpDialogName}");
            string cpName = Dialog.Get(cpDialogName);
            //Log($"[Dialog Testing] Dialog.Get says: {cpName}");

            if (
                cpName.Length + 1 >= cpDialogName.Length
                && cpName.Substring(1, cpDialogName.Length) == cpDialogName
            )
                cpName = null;

            Path.AddCheckpoint(cp, cpName);
        }
        #endregion


        public class ConsistencyTrackerSettings : EverestModuleSettings
        {
            public bool Enabled { get; set; } = true;

            public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.Disabled;
            public int OverlayOpacity { get; set; } = 8;

            public bool PauseDeathTracking
            {
                get => _PauseDeathTracking;
                set
                {
                    _PauseDeathTracking = value;
                    Instance.SaveChapterStats();
                }
            }
            private bool _PauseDeathTracking { get; set; } = false;

            public bool OnlyTrackWithGoldenBerry { get; set; } = false;

            public void CreateOnlyTrackWithGoldenBerryEntry(TextMenu menu, bool inGame)
            {
                menu.Add(
                    new TextMenu.OnOff(
                        "Only Track Deaths With Golden Berry",
                        OnlyTrackWithGoldenBerry
                    )
                    {
                        OnValueChange = v =>
                        {
                            OnlyTrackWithGoldenBerry = v;
                        }
                    }
                );
            }

            public bool RecordPath { get; set; } = false;

            public void CreateRecordPathEntry(TextMenu menu, bool inGame)
            {
                if (!inGame)
                    return;

                TextMenuExt.SubMenu subMenu = new("Path Recording", false);

                subMenu.Add(new TextMenu.SubHeader("!!!Existing paths will be overwritten!!!"));
                subMenu.Add(
                    new TextMenu.OnOff("Record Path", Instance.DoRecordPath)
                    {
                        OnValueChange = v =>
                        {
                            if (v)
                                Logger.Log(
                                    LogLevel.Info,
                                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerSettings)}/{nameof(CreateRecordPathEntry)}",
                                    $"Recording chapter path..."
                                );
                            else
                                Logger.Log(
                                    LogLevel.Info,
                                    $"{nameof(ConsistencyTracker)}/{nameof(ConsistencyTrackerSettings)}/{nameof(CreateRecordPathEntry)}",
                                    $"Stopped recording path. Outputting info..."
                                );

                            this.RecordPath = v;
                            Instance.DoRecordPath = v;
                            Instance.SaveChapterStats();
                        }
                    }
                );

                subMenu.Add(
                    new TextMenu.SubHeader("Editing the path requires a reload of the Overlay")
                );
                subMenu.Add(
                    new TextMenu.Button("Remove Current Room From Path")
                    {
                        OnPressed = Instance.RemoveRoomFromChapter
                    }
                );

                menu.Add(subMenu);
            }

            public bool WipeChapter { get; set; } = false;

            public void CreateWipeChapterEntry(TextMenu menu, bool inGame)
            {
                if (!inGame)
                    return;

                TextMenuExt.SubMenu subMenu = new("!!Data Wipe!!", false);
                subMenu.Add(new TextMenu.SubHeader("These actions cannot be reverted!"));

                subMenu.Add(new TextMenu.SubHeader("Current Room"));
                subMenu.Add(
                    new TextMenu.Button("Remove Last Attempt")
                    {
                        OnPressed = Instance.RemoveLastAttempt
                    }
                );

                subMenu.Add(
                    new TextMenu.Button("Remove Last Death Streak")
                    {
                        OnPressed = Instance.RemoveLastDeathStreak
                    }
                );

                subMenu.Add(
                    new TextMenu.Button("Remove All Attempts") { OnPressed = Instance.WipeRoomData }
                );

                subMenu.Add(
                    new TextMenu.Button("Remove Golden Berry Deaths")
                    {
                        OnPressed = Instance.RemoveRoomGoldenBerryDeaths
                    }
                );

                subMenu.Add(new TextMenu.SubHeader("Current Chapter"));
                subMenu.Add(
                    new TextMenu.Button("Reset All Attempts")
                    {
                        OnPressed = Instance.WipeChapterData
                    }
                );

                subMenu.Add(
                    new TextMenu.Button("Reset All Golden Berry Deaths")
                    {
                        OnPressed = Instance.WipeChapterGoldenBerryDeaths
                    }
                );

                menu.Add(subMenu);
            }

            public bool CreateSummary { get; set; } = false;
            public int SummarySelectedAttemptCount { get; set; } = 20;

            public void CreateCreateSummaryEntry(TextMenu menu, bool inGame)
            {
                if (!inGame)
                    return;

                TextMenuExt.SubMenu subMenu = new("Tracker Summary", false);
                subMenu.Add(
                    new TextMenu.SubHeader(
                        "Outputs some cool data of the current chapter in a readable .txt format"
                    )
                );

                subMenu.Add(
                    new TextMenu.SubHeader(
                        "When calculating the consistency stats, only the last X attempts will be counted"
                    )
                );
                List<KeyValuePair<int, string>> AttemptCounts =
                    new()
                    {
                        new KeyValuePair<int, string>(5, "5"),
                        new KeyValuePair<int, string>(10, "10"),
                        new KeyValuePair<int, string>(20, "20"),
                        new KeyValuePair<int, string>(100, "100"),
                    };
                subMenu.Add(
                    new TextMenuExt.EnumerableSlider<int>(
                        "Summary Over X Attempts",
                        AttemptCounts,
                        SummarySelectedAttemptCount
                    )
                    {
                        OnValueChange = (value) =>
                        {
                            SummarySelectedAttemptCount = value;
                        }
                    }
                );

                subMenu.Add(
                    new TextMenu.Button("Create Chapter Summary")
                    {
                        OnPressed = () =>
                        {
                            Instance.CreateChapterSummary(SummarySelectedAttemptCount);
                        }
                    }
                );

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
            public RoomNameDisplayType LiveDataRoomNameDisplayType { get; set; } =
                RoomNameDisplayType.AbbreviationAndRoomNumberInCP;

            //Types: 1 -> EH-3 | 2 -> Event-Horizon-3
            [SettingIgnore]
            public ListFormat LiveDataListOutputFormat { get; set; } = ListFormat.Json;

            [SettingIgnore]
            public bool LiveDataHideFormatsWithoutPath { get; set; } = false;

            [SettingIgnore]
            public bool LiveDataIgnoreUnplayedRooms { get; set; } = false;

            public void CreateLiveDataEntry(TextMenu menu, bool inGame)
            {
                TextMenuExt.SubMenu subMenu = new("Live Data Settings", false);

                subMenu.Add(
                    new TextMenu.SubHeader("Floating point numbers will be rounded to this decimal")
                );
                List<KeyValuePair<int, string>> DigitCounts =
                    new()
                    {
                        new KeyValuePair<int, string>(0, "0"),
                        new KeyValuePair<int, string>(1, "1"),
                        new KeyValuePair<int, string>(2, "2"),
                        new KeyValuePair<int, string>(3, "3"),
                        new KeyValuePair<int, string>(4, "4"),
                        new KeyValuePair<int, string>(5, "5"),
                    };
                subMenu.Add(
                    new TextMenuExt.EnumerableSlider<int>(
                        "Max. Decimal Places",
                        DigitCounts,
                        LiveDataDecimalPlaces
                    )
                    {
                        OnValueChange = (value) =>
                        {
                            LiveDataDecimalPlaces = value;
                        }
                    }
                );

                subMenu.Add(
                    new TextMenu.SubHeader(
                        "When calculating room consistency stats, only the last X attempts in each room will be counted"
                    )
                );
                List<KeyValuePair<int, string>> AttemptCounts =
                    new()
                    {
                        new KeyValuePair<int, string>(5, "5"),
                        new KeyValuePair<int, string>(10, "10"),
                        new KeyValuePair<int, string>(20, "20"),
                        new KeyValuePair<int, string>(100, "100"),
                    };
                subMenu.Add(
                    new TextMenuExt.EnumerableSlider<int>(
                        "Consider Last X Attempts",
                        AttemptCounts,
                        LiveDataSelectedAttemptCount
                    )
                    {
                        OnValueChange = (value) =>
                        {
                            LiveDataSelectedAttemptCount = value;
                        }
                    }
                );

                subMenu.Add(
                    new TextMenu.SubHeader(
                        "Whether you want checkpoint names to be full or abbreviated in the room name"
                    )
                );
                List<KeyValuePair<int, string>> PBNameTypes =
                    new()
                    {
                        new KeyValuePair<int, string>(
                            (int)RoomNameDisplayType.AbbreviationAndRoomNumberInCP,
                            "EH-3"
                        ),
                        new KeyValuePair<int, string>(
                            (int)RoomNameDisplayType.FullNameAndRoomNumberInCP,
                            "Event-Horizon-3"
                        ),
                    };
                subMenu.Add(
                    new TextMenuExt.EnumerableSlider<int>(
                        "Room Name Format",
                        PBNameTypes,
                        (int)LiveDataRoomNameDisplayType
                    )
                    {
                        OnValueChange = (value) =>
                        {
                            LiveDataRoomNameDisplayType = (RoomNameDisplayType)value;
                        }
                    }
                );

                subMenu.Add(
                    new TextMenu.SubHeader(
                        "Output format for lists. Plain is easily readable, JSON is for programming purposes"
                    )
                );
                List<KeyValuePair<int, string>> ListTypes =
                    new()
                    {
                        new KeyValuePair<int, string>((int)ListFormat.Plain, "Plain"),
                        new KeyValuePair<int, string>((int)ListFormat.Json, "JSON"),
                    };
                subMenu.Add(
                    new TextMenuExt.EnumerableSlider<int>(
                        "List Output Format",
                        ListTypes,
                        (int)LiveDataListOutputFormat
                    )
                    {
                        OnValueChange = (value) =>
                        {
                            LiveDataListOutputFormat = (ListFormat)value;
                        }
                    }
                );

                subMenu.Add(
                    new TextMenu.SubHeader(
                        "If a format depends on path information and no path is set, the format will be blanked out"
                    )
                );
                subMenu.Add(
                    new TextMenu.OnOff("Hide Formats When No Path", LiveDataHideFormatsWithoutPath)
                    {
                        OnValueChange = v =>
                        {
                            LiveDataHideFormatsWithoutPath = v;
                        }
                    }
                );

                subMenu.Add(
                    new TextMenu.SubHeader(
                        "For chance calculation unplayed rooms count as 0% success rate. Toggle this on to ignore unplayed rooms"
                    )
                );
                subMenu.Add(
                    new TextMenu.OnOff("Ignore Unplayed Rooms", LiveDataIgnoreUnplayedRooms)
                    {
                        OnValueChange = v =>
                        {
                            LiveDataIgnoreUnplayedRooms = v;
                        }
                    }
                );

                subMenu.Add(
                    new TextMenu.SubHeader(
                        $"After editing '{StatManager.BaseFolder}/{StatManager.FormatFileName}' use this to update the live data format"
                    )
                );
                subMenu.Add(
                    new TextMenu.Button("Reload format file")
                    {
                        OnPressed = () =>
                        {
                            Instance.StatsManager.LoadFormats();
                        }
                    }
                );

                menu.Add(subMenu);
            }
        }
    }
}
