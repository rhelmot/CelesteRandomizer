using Monocle;
using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Threading;

namespace Celeste.Mod.Randomizer {
    public class OuiRandoSettings : Oui {
        private DisablableTextMenu menu;
        private int savedMenuIndex = -1;
        private TextMenu.Button startButton;
        private Thread builderThread;
        private bool entering;

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
                Selected && Input.MenuCancel.Pressed
                && builderThread == null) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            }

            base.Update();
        }

        private void ReloadMenu() {
            menu = new DisablableTextMenu {
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

                new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_NUMDASHES"), (i) => {
                    return Dialog.Clean("MODOPTIONS_RANDOMIZER_NUMDASHES_" + Enum.GetNames(typeof(NumDashes))[i].ToUpperInvariant());
                }, 0, (int)NumDashes.Last - 1, (int)Settings.Dashes).Change((i) => {
                    Settings.Dashes = (NumDashes)i;
                }),

                new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY"), (i) => {
                    return Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_" + Enum.GetNames(typeof(Difficulty))[i].ToUpperInvariant());
                }, 0, (int)Difficulty.Last - 1, (int)Settings.Difficulty).Change((i) => {
                    Settings.Difficulty = (Difficulty)i;
                }),
            };

            var showHash = new TextMenuExt.EaseInSubHeaderExt("{hash}", false, menu) {
                HeightExtra = -10f,
                Offset = new Vector2(30, -5),
            };

            this.startButton = new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_START"));
            this.startButton.Pressed(() => {
                if (this.entering) {
                    return;
                }

                if (this.builderThread == null) {
                    this.startButton.Label = Dialog.Clean("MODOPTIONS_RANDOMIZER_CANCEL");
                    showHash.Title = Dialog.Clean("MODOPTIONS_RANDOMIZER_GENERATING");
                    menu.DisableMovement = true;

                    this.builderThread = new Thread(() => {
                        AreaKey newArea = RandoLogic.GenerateMap(Settings);
                        this.entering = true;

                        Audio.SetMusic((string)null, true, true);
                        Audio.SetAmbience((string)null, true);
                        Audio.Play("event:/ui/main/savefile_begin");

                        // use the debug file
                        SaveData.InitializeDebugMode();
                        // turn on variants mode
                        SaveData.Instance.VariantMode = true;
                        SaveData.Instance.AssistMode = false;

                        new FadeWipe(this.Scene, false, () => {
                            LevelEnter.Go(new Session(newArea, null, null), true);
                            this.builderThread = null;
                            this.entering = false;
                        });

                        /*foreach (AreaData area in AreaData.Areas) {
                            Logger.Log("randomizer", $"Skeleton for {area.GetSID()}");
                            RandoConfigFile.YamlSkeleton(area);

                        }*/
                    });
                    this.builderThread.Start();
                } else {
                    this.builderThread.Abort();
                    this.builderThread = null;

                    this.startButton.Label = Dialog.Clean("MODOPTIONS_RANDOMIZER_START");
                    this.startButton.OnEnter();
                    menu.DisableMovement = false;
                }
            });
            menu.Add(this.startButton);
            this.startButton.OnEnter += () => {
                showHash.Title = Dialog.Clean("MODOPTIONS_RANDOMIZER_HASH") + " " + this.Settings.Hash;
                showHash.FadeVisible = true;
            };
            this.startButton.OnLeave += () => {
                showHash.FadeVisible = false;
            };
            menu.Add(showHash);

            Scene.Add(menu);
        }
    }
}
