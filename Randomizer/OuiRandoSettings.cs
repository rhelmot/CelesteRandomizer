using Monocle;
using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    public class OuiRandoSettings : Oui {
        private TextMenu menu;
        private int savedMenuIndex = -1;

        private float alpha;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        public RandoSettings Settings {
            get {
                return RandoModule.Instance.Settings;
            }
        }

        public override IEnumerator Enter(Oui from) {
            ReloadMenu();
            menu.Visible = Visible = true;
            menu.Focused = false;

            // restore selection if coming from a submenu.
            if (savedMenuIndex != -1) {
                menu.Selection = Math.Min(savedMenuIndex, menu.LastPossibleSelection);
                menu.Position.Y = menu.ScrollTargetY;
            }

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play(SFX.ui_main_whoosh_large_out);
            menu.Focused = false;

            // save the menu position in case we want to restore it.
            savedMenuIndex = menu.Selection;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;
        }

        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (start == (Overworld.StartMode)55) {
                this.Add((Component)new Coroutine(this.Enter((Oui)null), true));
                return true;
            }
            return false;
        }

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && Input.MenuCancel.Pressed) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            }

            base.Update();
        }

        private void ReloadMenu() {
            menu = new TextMenu {
                new TextMenu.Header(Dialog.Clean("MODOPTIONS_RANDOMIZER_HEADER")),
                new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_SEED") + ": " + Settings.Seed.ToString(RandoModule.MAX_SEED_DIGITS)).Pressed(() => {
                    Audio.Play(SFX.ui_main_savefile_rename_start);
                    menu.SceneAs<Overworld>().Goto<UI.OuiNumberEntry>().Init<OuiRandoSettings>(
                        Settings.Seed,
                        (v) => Settings.Seed = (int)v,
                        RandoModule.MAX_SEED_DIGITS,
                        false,
                        false);
                }),

                new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER")).Pressed(() => {
                    Audio.Play(SFX.ui_main_button_select);
                    menu.SceneAs<Overworld>().Goto<OuiMapPicker>();
                }),
                new TextMenuExt.SubHeaderExt(Settings.LevelCount.ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS")) {
                    HeightExtra = -10f,
                    Offset = new Vector2(30, -5),
                },

                new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_REPEATROOMS"), Settings.RepeatRooms).Change((val) => {
                    Settings.RepeatRooms = val;
                }),

                new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_ENTERUNKNOWN"), Settings.EnterUnknown).Change((val) => {
                    Settings.EnterUnknown = val;
                }),

                new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_LOGIC"), (i) => {
                    return Dialog.Clean("MODOPTIONS_RANDOMIZER_LOGIC_" + Enum.GetNames(typeof(LogicType))[i].ToUpperInvariant());
                }, 0, (int)LogicType.Last - 1, (int)Settings.Algorithm).Change((i) => {
                    Settings.Algorithm = (LogicType)i;
                }),

                new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_LENGTH"), (i) => {
                    return Dialog.Clean("MODOPTIONS_RANDOMIZER_LENGTH_" + Enum.GetNames(typeof(MapLength))[i].ToUpperInvariant());
                }, 0, (int)MapLength.Last - 1, (int)Settings.Length).Change((i) => {
                    Settings.Length = (MapLength)i;
                }),

                new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_START")).Pressed(() => {
                    AreaKey newArea = RandoLogic.GenerateMap(Settings);
                    Audio.SetMusic((string) null, true, true);
                    Audio.SetAmbience((string) null, true);
                    Audio.Play("event:/ui/main/savefile_begin");
                    SaveData.InitializeDebugMode();

                    LevelEnter.Go(new Session(newArea, null, null), true);

                    /*foreach (AreaData area in AreaData.Areas) {
                        Logger.Log("randomizer", $"Skeleton for {area.GetSID()}");
                        RandoConfigFile.YamlSkeleton(area);

                    }*/
                }),
            };

            Scene.Add(menu);
        }
    }
}
