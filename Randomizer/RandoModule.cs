using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    public class RandoModule : EverestModule {
        public static RandoModule Instance;

        public RandoSettings Settings;
        public const int MAX_SEED_DIGITS = 6;

        public RandoModule() {
            Instance = this;

            var r = new Random();
            Settings = new RandoSettings {
                Seed = r.Next((int)Math.Pow(10, MAX_SEED_DIGITS)),
                RepeatRooms = false,
            };
        }

        public override void Load() {
            Everest.Events.MainMenu.OnCreateButtons += CreateMainMenuButton;
            Everest.Events.Level.OnCreatePauseMenuButtons += ModifyLevelMenu;
            On.Celeste.OverworldLoader.ctor += EnterToRandoMenu;
            On.Celeste.MapData.Load += DontLoadRandoMaps;
            On.Celeste.AreaData.Load += InitRandoData;
        }

        public override void LoadContent(bool firstLoad) {
        }


        public override void Unload() {
            Everest.Events.MainMenu.OnCreateButtons -= CreateMainMenuButton;
            Everest.Events.Level.OnCreatePauseMenuButtons -= ModifyLevelMenu;
            On.Celeste.OverworldLoader.ctor -= EnterToRandoMenu;
            On.Celeste.MapData.Load -= DontLoadRandoMaps;
            On.Celeste.AreaData.Load -= InitRandoData;
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            base.CreateModMenuSection(menu, inGame, snapshot);
        }

        public bool InRandomizer {
            get {
                if (SaveData.Instance == null) {
                    return false;
                }
                if (SaveData.Instance.CurrentSession == null) {
                    return false;
                }
                if (SaveData.Instance.CurrentSession.Area == null) {
                    return false;
                }
                AreaData area = AreaData.Get(SaveData.Instance.CurrentSession.Area);
                return area.GetSID().StartsWith("randomizer/");
            }
        }

        public void CreateMainMenuButton(OuiMainMenu menu, List<MenuButton> buttons) {
            MainMenuSmallButton btn = new MainMenuSmallButton("MODOPTIONS_RANDOMIZER_TOPMENU", "menu/randomizer", menu, Vector2.Zero, Vector2.Zero, () => {
                Audio.Play(SFX.ui_main_button_select);
                Audio.Play(SFX.ui_main_whoosh_large_in);
                menu.Overworld.Goto<OuiRandoSettings>();
            });
            buttons.Insert(1, btn);
        }

        public void ModifyLevelMenu(Level level, TextMenu pausemenu, bool minimal) {
            if (this.InRandomizer) {
                foreach (var item in new List<TextMenu.Item>(pausemenu.GetItems())) {
                    if (item.GetType() == typeof(TextMenu.Button)) {
                        var btn = (TextMenu.Button)item;
                        if (btn.Label == Dialog.Clean("MENU_PAUSE_SAVEQUIT") || btn.Label == Dialog.Clean("MENU_PAUSE_RETURN")) {
                            pausemenu.Remove(item);
                        }
                        if (btn.Label == Dialog.Clean("MENU_PAUSE_RESTARTAREA")) {
                            btn.Label = Dialog.Clean("MENU_PAUSE_RESTARTRANDO");
                        }
                    }
                }

                int returnIdx = pausemenu.GetItems().Count;
                pausemenu.Add(new TextMenu.Button(Dialog.Clean("MENU_PAUSE_QUITRANDO")).Pressed(() => {
                    level.PauseMainMenuOpen = false;
                    pausemenu.RemoveSelf();

                    TextMenu menu = new TextMenu();
                    menu.AutoScroll = false;
                    menu.Position = new Vector2((float)Engine.Width / 2f, (float)((double)Engine.Height / 2.0 - 100.0));
                    menu.Add(new TextMenu.Header(Dialog.Clean("MENU_QUITRANDO_TITLE")));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MENU_QUITRANDO_CONFIRM")).Pressed((Action)(() => {
                        Engine.TimeRate = 1f;
                        menu.Focused = false;
                        level.Session.InArea = false;
                        Audio.SetMusic((string)null, true, true);
                        Audio.BusStopAll("bus:/gameplay_sfx", true);
                        level.DoScreenWipe(false, (Action)(() => Engine.Scene = (Scene)new LevelExit(LevelExit.Mode.SaveAndQuit, level.Session, level.HiresSnow)), true);
                        foreach (LevelEndingHook component in level.Tracker.GetComponents<LevelEndingHook>()) {
                            if (component.OnEnd != null)
                                component.OnEnd();
                        }
                    })));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MENU_QUITRANDO_CANCEL")).Pressed((Action)(() => menu.OnCancel())));
                    menu.OnPause = menu.OnESC = (Action)(() => {
                        menu.RemoveSelf();
                        level.Paused = false;
                        Engine.FreezeTimer = 0.15f;
                        Audio.Play("event:/ui/game/unpause");
                    });
                    menu.OnCancel = (Action)(() => {
                        Audio.Play("event:/ui/main/button_back");
                        menu.RemoveSelf();
                        level.Pause(returnIdx, minimal, false);
                    });
                    level.Add((Entity)menu);
                }));
            }
        }

        public void EnterToRandoMenu(On.Celeste.OverworldLoader.orig_ctor orig, OverworldLoader self, Overworld.StartMode startMode, HiresSnow snow) {
            if (startMode == Overworld.StartMode.MainMenu && this.InRandomizer) {
                startMode = (Overworld.StartMode)55;
            }
            orig(self, startMode, snow);
        }

        public void DontLoadRandoMaps(On.Celeste.MapData.orig_Load orig, MapData self) {
            if (self.Data?.GetSID()?.StartsWith("randomizer/") ?? false) {
                return;
            }
            orig(self);
        }

        public void InitRandoData(On.Celeste.AreaData.orig_Load orig) {
            orig();
            RandoLogic.ProcessAreas();

            foreach (var key in RandoLogic.AvailableAreas) {
                RandoModule.Instance.Settings.EnableMap(key);
            }
        }
    }
}
