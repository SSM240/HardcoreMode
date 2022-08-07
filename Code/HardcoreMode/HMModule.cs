using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Monocle;
using Celeste.Mod.CollabUtils2.Entities;

namespace Celeste.Mod.HardcoreMode
{
    public class HMModule : EverestModule
    {
        #region Fields

        public const string hardcorePortrait = "idle_distracted";

        // dying in these states will not result in a hardcore death
        public static readonly int[] NoControlStates = new int[]
        {
            Player.StDummy,
            Player.StIntroWalk,
            Player.StIntroJump,
            Player.StIntroRespawn,
            Player.StIntroWakeUp,
            Player.StFrozen,
            Player.StReflectionFall,
            Player.StTempleFall,
            Player.StCassetteFly,
            Player.StIntroMoonJump,
            Player.StIntroThinkForABit
        };

        // used for re-enabling debug mode
        Core.CoreModuleSettings.VanillaTristate wasDebug = Core.CoreModule.Settings.DebugMode;

        // dictionary that stores the state of each file
        public Dictionary<int, bool> HardcoreFiles = new Dictionary<int, bool>();

        // current file select slot (for OnHardcoreToggleSelected)
        private OuiFileSelectSlot currentSlot;

        // current PlayerDeadBody instance (for DetermineDeathSound)
        private PlayerDeadBody currentPlayerDeadBody;

        // button to toggle hardcore mode
        private OuiFileSelectSlot.Button toggleButton;

        private readonly MethodInfo startPauseEffectsInfo = typeof(Level).GetMethod("StartPauseEffects",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private ILHook deathRoutineHook;

        private readonly MethodInfo deathRoutineInfo = typeof(PlayerDeadBody).GetMethod("DeathRoutine",
            BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget();

        private ILHook silverBerryHook;

        #endregion

        #region Module setup

        public static HMModule Instance;

        public HMModule()
        {
            Instance = this;
        }

        public override Type SettingsType => typeof(HMSettings);
        public static HMSettings HMSettings => (HMSettings)Instance._Settings;

        public override Type SaveDataType => typeof(HMSaveData);
        public static HMSaveData HMSaveData => (HMSaveData)Instance._SaveData;

        public override void Load()
        {
            // lots hooks
            Everest.Events.Level.OnLoadEntity += Level_OnLoadEntity;
            On.Celeste.Level.LoadLevel += On_Level_LoadLevel;
            On.Celeste.Level.Pause += On_Level_Pause;
            On.Celeste.Level.VariantMode += On_Level_VariantMode;
            On.Celeste.AreaComplete.Info += On_AreaComplete_Info;
            On.Celeste.Mod.UI.OuiModOptions.CreateMenu += On_OuiModOptions_CreateMenu;
            //On.Celeste.CS10_FinalLaunch.OnEnd += On_CS10_FinalLaunch_OnEnd;
            IL.Celeste.DreamWipe.Update += IL_DreamWipe_Update;
            On.Celeste.LevelExit.ctor += On_LevelExit_ctor;
            On.Celeste.PlayerDeadBody.Awake += On_PlayerDeadBody_Awake;
            On.Celeste.PlayerDeadBody.End += On_PlayerDeadBody_End;
            On.Celeste.SaveData.TryDelete += On_SaveData_TryDelete;
            deathRoutineHook = new ILHook(deathRoutineInfo, IL_DeathRoutine);
            Everest.Events.FileSelectSlot.OnCreateButtons += FileSelectSlot_OnCreateButtons;
            On.Celeste.OuiFileSelectSlot.Setup += On_OuiFileSelectSlot_Setup;
            On.Celeste.OuiFileSelectSlot.Update += On_OuiFileSelectSlot_Update;
            On.Celeste.OuiFileSelectSlot.Render += On_OuiFileSelectSlot_Render;
            On.Celeste.OuiFileSelectSlot.Select += On_OuiFileSelectSlot_Select;
            On.Celeste.OuiFileSelectSlot.OnNewGameSelected += On_OuiFileSelectSlot_OnNewGameSelected;
            On.Celeste.OuiFileSelectSlot.OnContinueSelected += On_OuiFileSelectSlot_OnContinueSelected;
            On.Celeste.OuiJournal.Render += On_OuiJournal_Render;
            On.Celeste.OuiChapterSelect.Update += On_OuiChapterSelect_Update;
        }

        public override void LoadContent(bool firstLoad)
        {
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata { Name = "CollabUtils2", Version = new Version(1, 5, 11)}))
            {
                HookSilverBerryAdded();
            }
        }

        // has to be a separate method so the JIT won't throw a tantrum if CU2 isn't installed
        private void HookSilverBerryAdded()
        {
            MethodInfo silverBerryAddedInfo = typeof(SilverBerry).GetMethod("Added",
                BindingFlags.Instance | BindingFlags.Public);
            silverBerryHook = new ILHook(silverBerryAddedInfo, IL_SilverBerry_Added);
        }

        public override void Unload()
        {
            Everest.Events.Level.OnLoadEntity -= Level_OnLoadEntity;
            On.Celeste.Level.LoadLevel -= On_Level_LoadLevel;
            On.Celeste.Level.Pause -= On_Level_Pause;
            On.Celeste.Level.VariantMode -= On_Level_VariantMode;
            On.Celeste.AreaComplete.Info -= On_AreaComplete_Info;
            On.Celeste.Mod.UI.OuiModOptions.CreateMenu -= On_OuiModOptions_CreateMenu;
            //On.Celeste.CS10_FinalLaunch.OnEnd -= On_CS10_FinalLaunch_OnEnd;
            IL.Celeste.DreamWipe.Update -= IL_DreamWipe_Update;
            On.Celeste.LevelExit.ctor -= On_LevelExit_ctor;
            On.Celeste.PlayerDeadBody.Awake -= On_PlayerDeadBody_Awake;
            On.Celeste.PlayerDeadBody.End -= On_PlayerDeadBody_End;
            On.Celeste.SaveData.TryDelete -= On_SaveData_TryDelete;
            deathRoutineHook?.Dispose();
            Everest.Events.FileSelectSlot.OnCreateButtons -= FileSelectSlot_OnCreateButtons;
            On.Celeste.OuiFileSelectSlot.Setup -= On_OuiFileSelectSlot_Setup;
            On.Celeste.OuiFileSelectSlot.Update -= On_OuiFileSelectSlot_Update;
            On.Celeste.OuiFileSelectSlot.Render -= On_OuiFileSelectSlot_Render;
            On.Celeste.OuiFileSelectSlot.Select -= On_OuiFileSelectSlot_Select;
            On.Celeste.OuiFileSelectSlot.OnNewGameSelected -= On_OuiFileSelectSlot_OnNewGameSelected;
            On.Celeste.OuiFileSelectSlot.OnContinueSelected -= On_OuiFileSelectSlot_OnContinueSelected;
            On.Celeste.OuiJournal.Render -= On_OuiJournal_Render;
            On.Celeste.OuiChapterSelect.Update -= On_OuiChapterSelect_Update;
            silverBerryHook?.Dispose();
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Reads a file slot's dictionary entry to determine whether it is in hardcore mode.
        /// If the entry is not present in the dictionary, it will add it.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <returns>True if the file is in hardcore mode, false otherwise.</returns>
        public bool IsHardcoreFile(int slot)
        {
            // attempt to get from dictionary
            if (HardcoreFiles.TryGetValue(slot, out bool value))
            {
                return value;
            }
            // default to false if the save file doesn't exist at all
            else if (!UserIO.Exists(SaveData.GetFilename(slot)))
            {
                return false;
            }
            else
            {
                // attempt to load from save file
                try
                {
                    Logger.Log(LogLevel.Info, "HardcoreMode", $"Getting save data from file {slot}");
                    base.LoadSaveData(slot);
                    HardcoreFiles[slot] = HMSaveData.HardcoreModeEnabled;
                    return HardcoreFiles[slot];
                }
                // default to false if that fails somehow
                catch
                {
                    Logger.Log(LogLevel.Warn, "HardcoreMode", $"Could not get save data from file {slot}");
                    HardcoreFiles[slot] = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// Determines whether a particular death should count as a hardcore death.
        /// </summary>
        /// <param name="playerDeadBody">The PlayerDeadBody instance for the death.</param>
        /// <returns>True if it is a hardcore death, false otherwise.</returns>
        public bool IsHardcoreDeath(PlayerDeadBody playerDeadBody)
        {
            // ALWAYS return false if not in hardcore mode
            if (!HMSaveData.HardcoreModeEnabled)
            {
                return false;
            }
            Level level = playerDeadBody.SceneAs<Level>();
            Session session = level.Session;

            // exception for mööm
            if (session.Area.GetLevelSet() == "Celeste" &&
                (session.Area.ID == 10 && session.Level == "j-17"))
            {
                return false;
            }

            DynData<PlayerDeadBody> deadBodyData = new DynData<PlayerDeadBody>(playerDeadBody);
            Player player = deadBodyData.Get<Player>("player");

            // exception for dying when the player has no control
            if (NoControlStates.Contains(player.StateMachine.State))
            {
                return false;
            }

            // other checks have failed, RIP to the save file
            return true;
        }

        /// <summary>
        /// Saves the current state of debug mode to a field and then disables it.
        /// </summary>
        public void DisableDebugMode()
        {
            wasDebug = Core.CoreModule.Settings.DebugMode;
            Core.CoreModule.Settings.DebugMode = Core.CoreModuleSettings.VanillaTristate.Never;
            Core.CoreModule.Instance.SaveSettings();
        }

        /// <summary>
        /// Restores the current debug mode setting.
        /// </summary>
        public void RestoreDebugMode()
        {
            Core.CoreModule.Settings.DebugMode = wasDebug;
            Core.CoreModule.Instance.SaveSettings();
        }

        #endregion

        #region In-game hooks

        /// <summary>
        /// Always spawn golden berries in hardcore mode.
        /// </summary>
        /// <param name="level">The current level.</param>
        /// <param name="levelData">The current level's data.</param>
        /// <param name="offset">The position of the entity.</param>
        /// <param name="entityData">The entity's data.</param>
        /// <returns>True if the berry was spawned, false otherwise.</returns>
        public bool Level_OnLoadEntity(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
        {
            if (entityData.Name == "goldenBerry")
            {
                // vanilla conditions for spawning a gold berry (checked to avoid duplicates)
                bool cheatMode = SaveData.Instance.CheatMode;
                bool flag6 = level.Session.FurthestSeenLevel == level.Session.Level || level.Session.Deaths == 0;
                bool flag7 = SaveData.Instance.UnlockedModes >= 3 || SaveData.Instance.DebugMode;
                bool completed = SaveData.Instance.Areas_Safe[level.Session.Area.ID].Modes[(int)level.Session.Area.Mode].Completed;
                // if these conditions are true, abort
                if (!((cheatMode || (flag7 && completed)) && flag6))
                {
                    if (HMSaveData.HardcoreModeEnabled && HMSettings.AlwaysSpawnGoldens)
                    {
                        level.Add(new Strawberry(entityData, offset, new EntityID(levelData.Name, entityData.ID)));
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Always spawn Collab Utils 2 silver berries in hardcore mode.
        /// </summary>
        public void IL_SilverBerry_Added(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Action<Entity, Scene>>(ModifySilverBerry);
        }

        // can't pass SilverBerry directly because fuck mac mono
        public static void ModifySilverBerry(Entity entity, Scene scene)
        {
            Session session = (scene as Level).Session;
            if (entity is SilverBerry silverBerry && HMSaveData.HardcoreModeEnabled && HMSettings.AlwaysSpawnGoldens
                && (session.FurthestSeenLevel == session.Level || session.Deaths == 0))
            {
                // make it immune to being removed
                new DynData<SilverBerry>(silverBerry)["spawnedThroughGiveSilver"] = true;
            }
        }

        /// <summary>
        /// Adds the Hardcore Mode icon to every level.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The current level.</param>
        /// <param name="playerIntro"></param>
        /// <param name="isFromLoader"></param>
        public void On_Level_LoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
        {
            orig(self, playerIntro, isFromLoader);
            if (HMSaveData.HardcoreModeEnabled && self.Tracker.GetEntity<Player>() != null)
            {
                self.Add(new HMIcon());
            }
        }

        /// <summary>
        /// Uses a custom restricted pause menu if the player is not safe in hardcore mode.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The current level.</param>
        /// <param name="startIndex">The initial menu selection index.</param>
        /// <param name="minimal">Whether the pause menu should be minimal.</param>
        /// <param name="quickReset">Whether the pause was triggered with the Quick Restart button.</param>
        public void On_Level_Pause(On.Celeste.Level.orig_Pause orig, Level self, int startIndex, bool minimal, bool quickReset)
        {
            Player player = self.Tracker.GetEntity<Player>();

            // fall back if not in hardcore mode (or player not found)
            if (!HMSaveData.HardcoreModeEnabled || player == null)
            {
                orig(self, startIndex, minimal, quickReset);
                return;
            }

            // if player is on safe ground or in a cutscene, use regular pause menu (but disable retry)
            bool isSafe = (player.OnSafeGround && !player.StrawberriesBlocked) ||
                self.InCutscene || self.SkippingCutscene;
            if (isSafe)
            {
                self.CanRetry = false;
                orig(self, startIndex, minimal, quickReset);
                return;
            }

            // otherwise, use custom pause menu functionality
            DynData<Level> levelData = new DynData<Level>(self);
            levelData["wasPaused"] = true;
            //SetInstanceField(self, "wasPaused", true);
            if (!self.Paused)
            {
                startPauseEffectsInfo.Invoke(self, null);
            }
            self.Paused = true;
            self.PauseMainMenuOpen = !quickReset;
            TextMenu menu = new TextMenu();
            string title = quickReset ? "menu_restart_title" : "menu_pause_title";
            string buttonText = quickReset ? "menu_restart_cancel" : "menu_pause_resume";
            string info = quickReset ? "menu_hardcore_quickrestart" : "menu_hardcore_normal";
            menu.Add(new TextMenu.Header(Dialog.Clean(title)));
            menu.Add(new TextMenu.Button(Dialog.Clean(buttonText)).Pressed(() => menu.OnCancel()));
            menu.Add(new HMMenuInfo(Dialog.Clean(info)));
            menu.OnESC = menu.OnCancel = menu.OnPause = () =>
            {
                menu.RemoveSelf();
                self.Paused = false;
                self.PauseMainMenuOpen = false;
                Audio.Play("event:/ui/game/unpause");
                Engine.FreezeTimer = 0.15f;
            };
            self.Add(menu);
        }

        /// <summary>
        /// Disables assist options in variant mode.
        /// </summary>
        /// <param name="orig"></param>
        /// <param name="self"></param>
        /// <param name="returnIndex"></param>
        /// <param name="minimal"></param>
        public void On_Level_VariantMode(On.Celeste.Level.orig_VariantMode orig, Level self, int returnIndex, bool minimal)
        {
            orig(self, returnIndex, minimal);
            if (HMSaveData.HardcoreModeEnabled)
            {
                foreach (Entity entity in self.Entities.ToAdd)
                {
                    if (entity is TextMenu)
                    {
                        TextMenu menu = entity as TextMenu;
                        menu.Selection = 3;
                        // have to populate this now since language won't always be the same as startup
                        string[] assistOptions = new string[]
                        {
                            Dialog.Clean("MENU_ASSIST_GAMESPEED"),
                            Dialog.Clean("MENU_ASSIST_INVINCIBLE"),
                            Dialog.Clean("MENU_ASSIST_AIR_DASHES"),
                            Dialog.Clean("MENU_ASSIST_DASH_ASSIST"),
                            Dialog.Clean("MENU_ASSIST_INFINITE_STAMINA")
                        };
                        foreach (TextMenu.Item item in menu.Items)
                        {
                            if (item is TextMenu.OnOff)
                            {
                                TextMenu.OnOff onOff = item as TextMenu.OnOff;
                                string label = onOff.Label;
                                if (assistOptions.Contains(label))
                                {
                                    onOff.Disabled = true;
                                }
                            }
                            else if (item is TextMenu.Slider)
                            {
                                TextMenu.Slider slider = item as TextMenu.Slider;
                                string label = slider.Label;
                                if (assistOptions.Contains(label))
                                {
                                    slider.Disabled = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Slows down the game and stops the music on a hardcore death, for dramatic effect.
        /// Also deletes the save file immediately.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The current PlayerDeadBody instance.</param>
        /// <param name="scene">The current scene.</param>
        public void On_PlayerDeadBody_Awake(On.Celeste.PlayerDeadBody.orig_Awake orig, PlayerDeadBody self, Scene scene)
        {
            currentPlayerDeadBody = self;
            if (IsHardcoreDeath(self))
            {
                SaveData.TryDelete(SaveData.Instance.FileSlot);  // spooky!
                DynData<PlayerDeadBody> deadBodyData = new DynData<PlayerDeadBody>(self);
                Vector2 bounce = deadBodyData.Get<Vector2>("bounce");
                if (bounce != Vector2.Zero)
                {
                    self.Add(new Coroutine(SlowDownRoutine(0.35f, 1.4f)));
                }
                else
                {
                    self.Add(new Coroutine(SlowDownRoutine(0.30f, 1.75f)));
                }
                // fixes a crash in ch9 (also feels better in general)
                self.SceneAs<Level>().Tracker.GetEntity<CassetteBlockManager>()?.RemoveSelf();
                Audio.SetMusic(null);
                // fixes stuff that forces the timerate to 1
                self.Scene.Tracker.GetEntity<AngryOshiro>()?.StopControllingTime();
                foreach (Entity entity in self.Scene.Entities)  // SeekerEffectsController isn't tracked
                {
                    if (entity is SeekerEffectsController)
                    {
                        entity.RemoveSelf();
                    }
                }
            }
            orig(self, scene);
        }

        /// <summary>
        /// Routine that gradually slows down the game.
        /// </summary>
        /// <param name="target">The target TimeRate.</param>
        /// <param name="slowRate">The rate at which to approach the target TimeRate.</param>
        /// <returns>Yields null until TimeRate has reached its target, then exits.</returns>
        private IEnumerator SlowDownRoutine(float target, float slowRate)
        {
            Engine.TimeRate = 1f;
            while (Engine.TimeRate > target)
            {
                Engine.TimeRate = Calc.Approach(Engine.TimeRate, target, slowRate * Engine.RawDeltaTime);
                yield return null;
            }
        }

        /// <summary>
        /// Brings up the Game Over screen if the player died in hardcore mode.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The current PlayerDeadBody instance.</param>
        public void On_PlayerDeadBody_End(On.Celeste.PlayerDeadBody.orig_End orig, PlayerDeadBody self)
        {
            if (IsHardcoreDeath(self))
            {
                Logger.Log(LogLevel.Info, "HardcoreMode", $"Player died, RIP (file {SaveData.Instance.FileSlot})");
                Level level = self.SceneAs<Level>();
                self.DeathAction = () =>
                {
                    Engine.TimeRate = 1f;
                    level.Session.InArea = false;
                    Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);
                    // SaveData.Instance isn't actually gone yet, despite that the save file has been deleted
                    Engine.Scene = new GameOverScreen(SaveData.Instance, level);
                };
            }

            orig(self);
        }

        /// <summary>
        /// Always send the player to Farewell's golden room when in hardcore mode.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The cutscene for the final launch in Farewell.</param>
        /// <param name="level">The current level.</param>
        public void On_CS10_FinalLaunch_OnEnd(On.Celeste.CS10_FinalLaunch.orig_OnEnd orig, CS10_FinalLaunch self, Level level)
        {
            if (HMSaveData.HardcoreModeEnabled)
            {
                DynData<CS10_FinalLaunch> cutsceneData = new DynData<CS10_FinalLaunch>(self);
                cutsceneData["hasGolden"] = true;
                //SetInstanceField(self, "hasGolden", true);
            }
            orig(self, level);
        }

        /// <summary>
        /// Disables the debug mode option when in-game.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="inGame">Whether we are in-game.</param>
        /// <param name="snapshot"></param>
        /// <returns>The text menu corresponding to the mod options.</returns>
        public TextMenu On_OuiModOptions_CreateMenu(On.Celeste.Mod.UI.OuiModOptions.orig_CreateMenu orig, bool inGame, FMOD.Studio.EventInstance snapshot)
        {
            TextMenu textMenu = orig(inGame, snapshot);
            if (inGame && HMSaveData.HardcoreModeEnabled)
            {
                foreach (TextMenu.Item item in textMenu.Items)
                {
                    if (item is TextMenu.OnOff)
                    {
                        TextMenu.OnOff onOff = item as TextMenu.OnOff;
                        if (onOff.Label == Dialog.Clean("MODOPTIONS_COREMODULE_DEBUGMODE"))
                        {
                            onOff.Disabled = true;
                            break;
                        }
                    }
                    else if (item is TextMenu.Slider)
                    {
                        TextMenu.Slider slider = item as TextMenu.Slider;
                        if (slider.Label == Dialog.Clean("MODOPTIONS_COREMODULE_DEBUGMODE"))
                        {
                            slider.Disabled = true;
                            break;
                        }
                    }
                }
                textMenu.Selection = 3;
            }
            return textMenu;
        }

        /// <summary>
        /// Modifies the death sound in Hardcore Mode.
        /// </summary>
        /// <param name="il">Object allowing CIL patching.</param>
        public void IL_DeathRoutine(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            // look for where the death sound is loaded
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("event:/char/madeline/death")))
            {
                Logger.Log("HardcoreMode",
                    $"Modifying death sound at index {cursor.Index} in CIL code for PlayerDeadBody.DeathRoutine");
                cursor.Emit(OpCodes.Pop);
                cursor.EmitDelegate<Func<string>>(DetermineDeathSound);
            }
        }

        /// <summary>
        /// Returns golden death sound if the death is a hardcore death, or default one otherwise.
        /// </summary>
        /// <returns>The event name of the death sound.</returns>
        private string DetermineDeathSound()
        {
            if (IsHardcoreDeath(currentPlayerDeadBody))
            {
                return "event:/new_content/char/madeline/death_golden";
            }
            else
            {
                return "event:/char/madeline/death";
            }
        }

        /// <summary>
        /// Modifies DreamWipe.Update to use RawDeltaTime instead of DeltaTime.
        /// </summary>
        /// <param name="il">Object allowing CIL patching.</param>
        public void IL_DreamWipe_Update(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCall<Engine>("get_DeltaTime")))
            {
                Logger.Log("HardcoreMode",
                    $"Modifying DeltaTime call at index {cursor.Index} in CIL code for DreamWipe.Update");
                cursor.Remove();
                cursor.Emit<Engine>(OpCodes.Call, "get_RawDeltaTime");
            }
        }

        #endregion

        #region File select slot hooks

        /// <summary>
        /// Play the correct portrait when a hardcore file is first created.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The file select slot.</param>
        public void On_OuiFileSelectSlot_Setup(On.Celeste.OuiFileSelectSlot.orig_Setup orig, OuiFileSelectSlot self)
        {
            orig(self);
            if (IsHardcoreFile(self.FileSlot))
            {
                self.Portrait.Play(hardcorePortrait);
            }
        }

        /// <summary>
        /// Enables the quick restart button to toggle hardcore mode.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The file select slot we are creating.</param>
        public void On_OuiFileSelectSlot_Update(On.Celeste.OuiFileSelectSlot.orig_Update orig, OuiFileSelectSlot self)
        {
            orig(self);
            // abort immediately if file already exists
            if (self.Exists)
            {
                return;
            }
            // use DynData to get private fields
            DynData<OuiFileSelectSlot> slotData = new DynData<OuiFileSelectSlot>(self);
            bool selected = slotData.Get<bool>("selected");
            OuiFileSelect fileSelect = slotData.Get<OuiFileSelect>("fileSelect");
            Tween tween = slotData.Get<Tween>("tween");
            float inputDelay = slotData.Get<float>("inputDelay");
            // reproduce conditions for allowing button presses
            if (selected && fileSelect.Selected && fileSelect.Focused && !self.StartingGame && tween == null && inputDelay <= 0f && !self.StartingGame)
            {
                // toggle hardcore mode if quick reset button is pressed
                if (HMSettings.QuickHardcoreToggle.Pressed)
                {
                    OnHardcoreToggleSelected();
                }
            }
        }

        /// <summary>
        /// Draws the hardcore tab if that file is in hardcore mode.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The file select slot we are drawing on.</param>
        public void On_OuiFileSelectSlot_Render(On.Celeste.OuiFileSelectSlot.orig_Render orig, OuiFileSelectSlot self)
        {
            if(IsHardcoreFile(self.FileSlot))
            {
                // mimic the original code for drawing tabs
                DynData<OuiFileSelectSlot> slotData = new DynData<OuiFileSelectSlot>(self);
                float highlightEase = slotData.Get<float>("highlightEase");
                float newgameFade = slotData.Get<float>("newgameFade");
                float scaleFactor = Ease.CubeInOut(highlightEase);
                Vector2 vector = self.Position - Vector2.UnitX * scaleFactor * 360f;
                float scale = self.Exists ? 1f : newgameFade;
                if(!self.Corrupted && (newgameFade > 0f || self.Exists))
                {
                    MTN.FileSelect["hardcoretab"].DrawCentered(vector, Color.White * scale);
                }
            }
            orig(self);
        }

        /// <summary>
        /// Makes sure the Hardcore Mode flag is reset when a new file is selected.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The file slot selected.</param>
        /// <param name="resetButtonIndex">Whether the button index should be reset
        ///     (that is, not returning from assist mode screen or something).</param>
        public void On_OuiFileSelectSlot_Select(On.Celeste.OuiFileSelectSlot.orig_Select orig, OuiFileSelectSlot self, bool resetButtonIndex)
        {
            if (!self.Exists && resetButtonIndex)
            {
                HardcoreFiles[self.FileSlot] = false;
                self.Portrait.Play("idle_normal");
            }
            orig(self, resetButtonIndex);
        }

        /// <summary>
        /// Adds the Hardcore Mode button to file creation.
        /// </summary>
        /// <param name="buttons">The list of buttons.</param>
        /// <param name="slot">The slot we are creating.</param>
        /// <param name="modSaveData">The module save data for this slot.</param>
        /// <param name="fileExists">True if the file already exists, false if we're creating a new one.</param>
        public void FileSelectSlot_OnCreateButtons(List<OuiFileSelectSlot.Button> buttons, OuiFileSelectSlot slot, EverestModuleSaveData modSaveData, bool fileExists)
        {
            currentSlot = slot;
            if (!fileExists)
            {
                string dialogId = HardcoreFiles[slot.FileSlot] ? "FILE_HARDCORE_ON" : "FILE_HARDCORE_OFF";
                toggleButton = new OuiFileSelectSlot.Button()
                {
                    Label = Dialog.Clean(dialogId),
                    Action = OnHardcoreToggleSelected,
                    Scale = 0.7f
                };
                buttons.Add(toggleButton);
            }
        }

        /// <summary>
        /// Called when toggleButton is pressed.
        /// Toggles Hardcore Mode for the currently selected file.
        /// </summary>
        private void OnHardcoreToggleSelected()
        {
            int index = currentSlot.FileSlot;
            if (!IsHardcoreFile(index))
            {
                HardcoreFiles[index] = true;
                toggleButton.Label = Dialog.Clean("FILE_HARDCORE_ON");
                Audio.Play("event:/ui/main/button_toggle_on");
                currentSlot.Portrait.Play(hardcorePortrait);
            }
            else
            {
                HardcoreFiles[index] = false;
                toggleButton.Label = Dialog.Clean("FILE_HARDCORE_OFF");
                Audio.Play("event:/ui/main/button_toggle_off");
                currentSlot.Portrait.Play("idle_normal");
            }
        }

        /// <summary>
        /// Writes the mod's SaveData to a newly created save file, and disables assist/debug mode if hardcore.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The file select slot being created.</param>
        public void On_OuiFileSelectSlot_OnNewGameSelected(On.Celeste.OuiFileSelectSlot.orig_OnNewGameSelected orig, OuiFileSelectSlot self)
        {
            orig(self);
            LoadSaveData(self.FileSlot);
            HMSaveData.HardcoreModeEnabled = HardcoreFiles[self.FileSlot];
            SaveSaveData(self.FileSlot);
            if (HMSaveData.HardcoreModeEnabled)
            {
                SaveData.Instance.AssistMode = false;
                DisableDebugMode();
            }
        }

        /// <summary>
        /// Disables assist and debug mode if continuing a hardcore file.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The file being continued.</param>
        public void On_OuiFileSelectSlot_OnContinueSelected(On.Celeste.OuiFileSelectSlot.orig_OnContinueSelected orig, OuiFileSelectSlot self)
        {
            orig(self);
            if (HMSaveData.HardcoreModeEnabled)
            {
                SaveData.Instance.AssistMode = false;
                DisableDebugMode();
            }
        }

        // The below is now obsolete, but I'm keeping it here because of how ridiculous it is.
        /*
        /// <summary>
        /// Adds the "Hardcore Mode" button to file creation.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The file select slot we are creating.</param>
        public void On_OuiFileSelectSlot_CreateButtons(On.Celeste.OuiFileSelectSlot.orig_CreateButtons orig, OuiFileSelectSlot self)
        {
            orig(self);
            currentSlot = self;
            if (!self.Exists)
            {
                // mess of reflection necessary because the Button class is private
                Type buttonClass = GetNestedType(typeof(OuiFileSelectSlot), "Button");
                toggleButton = Activator.CreateInstance(buttonClass);
                string dialogId = HardcoreFiles[self.FileSlot] ? "FILE_HARDCORE_ON" : "FILE_HARDCORE_OFF";
                SetInstanceField(toggleButton, "Label", Dialog.Clean(dialogId));
                SetInstanceField(toggleButton, "Action", (Action)OnHardcoreToggleSelected);
                SetInstanceField(toggleButton, "Scale", 0.7f);

                Type t = typeof(List<>).MakeGenericType(buttonClass);
                var buttons = Convert.ChangeType(GetInstanceField(self, "buttons"), t);
                buttons.GetType().GetMethod("Add").Invoke(buttons, new object[] { toggleButton });
                SetInstanceField(self, "buttons", buttons);
            }
        }
        */

        #endregion

        #region Miscellaneous hooks

        /// <summary>
        /// Shows the hardcore tab on the journal screen.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The journal we are drawing on.</param>
        public void On_OuiJournal_Render(On.Celeste.OuiJournal.orig_Render orig, OuiJournal self)
        {
            Vector2 vector = self.Position + new Vector2(128f, 120f);
            if (HMSaveData.HardcoreModeEnabled)
            {
                MTN.FileSelect["hardcoretab"].DrawCentered(vector + new Vector2(80f, 375f), Color.White, 1f, (float)Math.PI / 2f);
            }
            orig(self);
        }

        /// <summary>
        /// Restores debug mode when backing out of level select.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The current chapter select instance.</param>
        public void On_OuiChapterSelect_Update(On.Celeste.OuiChapterSelect.orig_Update orig, OuiChapterSelect self)
        {
            orig(self);
            // use DynData to get private field
            DynData<OuiChapterSelect> chapterSelectData = new DynData<OuiChapterSelect>(self);
            bool disableInput = chapterSelectData.Get<bool>("disableInput");
            // reproduce conditions for allowing button presses
            if (self.Focused && !disableInput)
            {
                if (HMSaveData.HardcoreModeEnabled)
                {
                    // restore debug settings if cancel is pressed
                    if (Input.MenuCancel.Pressed)
                    {
                        RestoreDebugMode();
                    }
                }
            }
        }

        /// <summary>
        /// Restores debug mode on save & quit in hardcore mode.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The LevelExit object.</param>
        /// <param name="mode">The mode by which the level is exited.</param>
        /// <param name="session"></param>
        /// <param name="snow"></param>
        public void On_LevelExit_ctor(On.Celeste.LevelExit.orig_ctor orig, LevelExit self, LevelExit.Mode mode, Session session, HiresSnow snow)
        {
            if (HMSaveData.HardcoreModeEnabled && mode == LevelExit.Mode.SaveAndQuit)
            {
                RestoreDebugMode();
            }
            orig(self, mode, session, snow);
        }

        /// <summary>
        /// Shows the Hardcore Mode icon on the chapter complete screen.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="ease"></param>
        /// <param name="speedrunTimerChapterString"></param>
        /// <param name="speedrunTimerFileString"></param>
        /// <param name="chapterSpeedrunText"></param>
        /// <param name="versionText"></param>
        public void On_AreaComplete_Info(On.Celeste.AreaComplete.orig_Info orig, float ease, string speedrunTimerChapterString,
            string speedrunTimerFileString, string chapterSpeedrunText, string versionText)
        {
            orig(ease, speedrunTimerChapterString, speedrunTimerFileString, chapterSpeedrunText, versionText);
            if (HMSaveData.HardcoreModeEnabled && ease > 0f)
            {
                MTexture icon = GFX.Gui["hardcoreicon"];
                Vector2 finalPos;
                if (Settings.Instance.SpeedrunClock == 0)
                {
                    finalPos = new Vector2(100f, 980f);
                }
                else
                {
                    finalPos = new Vector2(135f, 845f);
                }
                Vector2 position = new Vector2(finalPos.X - 300f * (1f - Ease.CubeOut(ease)), finalPos.Y);
                Vector2 justify = new Vector2(0.5f, 0.5f);
                float scale = 0.85f;
                icon.DrawJustified(position, justify, Color.White, scale);
            }
        }

        /// <summary>
        /// Resets a file's Hardcore Mode status when it is successfully deleted.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="slot">The ID of the file being deleted.</param>
        /// <returns>The result of the original method (whether the file was successfully deleted).</returns>
        public bool On_SaveData_TryDelete(On.Celeste.SaveData.orig_TryDelete orig, int slot)
        {
            bool result = orig(slot);
            if (result)
            {
                HardcoreFiles[slot] = false;
            }
            return result;
        }

        #endregion
    }
}
