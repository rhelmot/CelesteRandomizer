using Monocle;
using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Threading;

namespace Celeste.Mod.Randomizer {
    public class OuiRandoSettings : Oui {
        private DisablableTextMenu menu;
        private int savedMenuIndex = -1;
        private Thread builderThread;
        private bool entering;

        private float alpha;
        private float journalEase;

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

            if (from is OuiRandoRecords) {
                savedMenuIndex = menu.LastPossibleSelection;
            }

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

            // save settings
            RandoModule.Instance.SavedData.SavedSettings = Settings.Copy();
            RandoModule.Instance.SaveSettings();

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
            if (menu != null && menu.Focused && Selected) {
                if (Input.MenuCancel.Pressed && builderThread == null) {
                    Audio.Play(SFX.ui_main_button_back);
                    Overworld.Goto<OuiMainMenu>();
                } else if (Input.Pause.Pressed) {
                    Audio.Play(SFX.ui_main_button_select);
                    menu.Selection = menu.LastPossibleSelection;
                    menu.Current.OnPressed();
                } else if (Input.MenuJournal.Pressed) {
                    Audio.Play(SFX.ui_world_journal_select);
                    Overworld.Goto<OuiRandoRecords>();
                }
            }

			journalEase = Calc.Approach (journalEase, (menu?.Focused ?? false) ? 1f : 0f, Engine.DeltaTime * 4f);

            base.Update();
        }

        public override void Render() {
            base.Render();

			if (journalEase > 0f) {
				Vector2 position = new Vector2(128f * Ease.CubeOut (journalEase), 952f);
				GFX.Gui ["menu/journal"].DrawCentered (position, Color.White * Ease.CubeOut (journalEase));
				Input.GuiButton (Input.MenuJournal, "controls/keyboard/oemquestion").Draw (position, Vector2.Zero, Color.White * Ease.CubeOut (journalEase));
			}
        }

        private void ReloadMenu() {
            menu = new DisablableTextMenu {
                new TextMenu.Header(Dialog.Clean("MODOPTIONS_RANDOMIZER_HEADER"))
            };

            var hashtext = new TextMenuExt.EaseInSubHeaderExt("{hash}", true, menu) {
                HeightExtra = -10f,
                Offset = new Vector2(30, -5),
            };
            void updateHashText() {
                hashtext.Title = "v" + RandoModule.Instance.Metadata.VersionString;
                if (Settings.SeedType == SeedType.Custom) {
                    hashtext.Title += " #" + Settings.Hash.ToString();
                }
            }
            updateHashText();

            var errortext = new TextMenuExt.EaseInSubHeaderExt("{error}", false, menu) {
                HeightExtra = -10f,
                Offset = new Vector2(30, -5),
            };

            var seedbutton = new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_SEED") + ": " + Settings.Seed); 
            seedbutton.Pressed(() => {
                Audio.Play(SFX.ui_main_savefile_rename_start);
                menu.SceneAs<Overworld>().Goto<UI.OuiTextEntry>().Init<OuiRandoSettings>(
                    Settings.Seed,
                    (v) => Settings.Seed = v,
                    RandoModule.MAX_SEED_CHARS
                );
            });
            seedbutton.Visible = Settings.SeedType == SeedType.Custom;

            var seedtypetoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_SEEDTYPE"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_SEEDTYPE_" + Enum.GetNames(typeof(SeedType))[i].ToUpperInvariant());
            }, 0, (int)SeedType.Last - 1, (int)Settings.SeedType).Change((i) => {
                Settings.SeedType = (SeedType)i;
                seedbutton.Visible = Settings.SeedType == SeedType.Custom;
                // just in case...
                seedbutton.Label = Dialog.Clean("MODOPTIONS_RANDOMIZER_SEED") + ": " + Settings.Seed;
                updateHashText();
            });

            var mapbutton = new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER")).Pressed(() => {
                Audio.Play(SFX.ui_main_button_select);
                menu.SceneAs<Overworld>().Goto<OuiMapPicker>();
            });

            var mapcountlbl = new TextMenuExt.SubHeaderExt(Settings.LevelCount.ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS")) {
                HeightExtra = -10f,
                Offset = new Vector2(30, -5),
            };

            var logictoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_LOGIC"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_LOGIC_" + Enum.GetNames(typeof(LogicType))[i].ToUpperInvariant());
            }, 0, (int)LogicType.Last - 1, (int)Settings.Algorithm).Change((i) => {
                Settings.Algorithm = (LogicType)i;
                updateHashText();
            });

            var lengthtoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_LENGTH"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_LENGTH_" + Enum.GetNames(typeof(MapLength))[i].ToUpperInvariant());
            }, 0, (int)MapLength.Last - 1, (int)Settings.Length).Change((i) => {
                Settings.Length = (MapLength)i;
                updateHashText();
            });

            var numdashestoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_NUMDASHES"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_NUMDASHES_" + Enum.GetNames(typeof(NumDashes))[i].ToUpperInvariant());
            }, 0, (int)NumDashes.Last - 1, (int)Settings.Dashes).Change((i) => {
                Settings.Dashes = (NumDashes)i;
                updateHashText();
            });

            var difficultytoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_" + Enum.GetNames(typeof(Difficulty))[i].ToUpperInvariant());
            }, 0, (int)Difficulty.Last - 1, (int)Settings.Difficulty).Change((i) => {
                Settings.Difficulty = (Difficulty)i;
                updateHashText();
            });

            var repeatroomstoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_REPEATROOMS"), Settings.RepeatRooms).Change((val) => {
                Settings.RepeatRooms = val;
                updateHashText();
            });

            var enterunknowntext = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("MODOPTIONS_RANDOMIZER_ENTERUNKNOWN_EXPLAIN"), false, menu) {
                HeightExtra = 17f,
                Offset = new Vector2(30, -5),
            };

            var enterunknowntoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_ENTERUNKNOWN"), Settings.EnterUnknown).Change((val) => {
                Settings.EnterUnknown = val;
                updateHashText();
            });
            enterunknowntoggle.OnEnter += () => { enterunknowntext.FadeVisible = true; };
            enterunknowntoggle.OnLeave += () => { enterunknowntext.FadeVisible = false; };

            var shinetoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_SHINE"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_SHINE_" + Enum.GetNames(typeof(ShineLights))[i].ToUpperInvariant());
            }, 0, (int)ShineLights.Last - 1, (int)Settings.Lights).Change((i) => {
                Settings.Lights = (ShineLights)i;
                updateHashText();
            });

            var darktoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_DARK"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_DARK_" + Enum.GetNames(typeof(Darkness))[i].ToUpperInvariant());
            }, 0, (int)Darkness.Last - 1, (int)Settings.Darkness).Change((i) => {
                Settings.Darkness = (Darkness)i;
                updateHashText();
            });

            var goldentoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_GOLDENBERRY"), Settings.SpawnGolden).Change((val) => {
                Settings.SpawnGolden = val;
            });

            var variantstoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_VARIANTS"), Settings.Variants).Change((val) => {
                Settings.Variants = val;
            });

            var moreoptions = false;
            repeatroomstoggle.Visible = false;
            enterunknowntoggle.Visible = false;
            goldentoggle.Visible = false;
            shinetoggle.Visible = false;
            darktoggle.Visible = false;
            variantstoggle.Visible = false;

            var moreoptionsbtn = new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_MOREOPTIONS"));
            moreoptionsbtn.Pressed(() => {
                moreoptions = !moreoptions;
                moreoptionsbtn.Label = moreoptions ? Dialog.Clean("MODOPTIONS_RANDOMIZER_FEWEROPTIONS") : Dialog.Clean("MODOPTIONS_RANDOMIZER_MOREOPTIONS");

                repeatroomstoggle.Visible = moreoptions;
                enterunknowntoggle.Visible = moreoptions;
                goldentoggle.Visible = moreoptions;
                shinetoggle.Visible = moreoptions;
                darktoggle.Visible = moreoptions;
                variantstoggle.Visible = moreoptions;
            });

            void syncModel() {
                repeatroomstoggle.Index = Settings.RepeatRooms ? 1 : 0;
                enterunknowntoggle.Index = Settings.EnterUnknown ? 1 : 0;
                variantstoggle.Index = Settings.Variants ? 1 : 0;
                logictoggle.Index = (int)Settings.Algorithm;
                lengthtoggle.Index = (int)Settings.Length;
                numdashestoggle.Index = (int)Settings.Dashes;
                difficultytoggle.Index = (int)Settings.Difficulty;
                shinetoggle.Index = (int)Settings.Lights;
                darktoggle.Index = (int)Settings.Darkness;
                mapcountlbl.Title = Settings.LevelCount.ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS");

                var locked = Settings.Rules != Ruleset.Custom;
                mapbutton.Disabled = locked;
                repeatroomstoggle.Disabled = locked;
                enterunknowntoggle.Disabled = locked;
                logictoggle.Disabled = locked;
                lengthtoggle.Disabled = locked;
                numdashestoggle.Disabled = locked;
                difficultytoggle.Disabled = locked;
                shinetoggle.Disabled = locked;
                darktoggle.Disabled = locked;
                variantstoggle.Disabled = locked;
            }
            syncModel();

            var rulestoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_RULES"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_RULES_" + Enum.GetNames(typeof(Ruleset))[i].ToUpperInvariant());
            }, 0, (int)Ruleset.Last - 1, (int)Settings.Rules).Change((i) => {
                Settings.Rules = (Ruleset)i;
                Settings.Enforce();
                syncModel();
                updateHashText();
            });

            var startbutton = new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_START"));
            startbutton.Pressed(() => {
                if (this.entering) {
                    return;
                }

                void reenableMenu() {
                    this.builderThread = null;

                    startbutton.Label = Dialog.Clean("MODOPTIONS_RANDOMIZER_START");
                    updateHashText();
                    menu.DisableMovement = false;
                }

                if (this.builderThread == null) {
                    errortext.FadeVisible = false;
                    startbutton.Label = Dialog.Clean("MODOPTIONS_RANDOMIZER_CANCEL");
                    hashtext.Title += " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_GENERATING");
                    menu.DisableMovement = true;

                    this.builderThread = new Thread(() => {
                        Settings.Enforce();
                        AreaKey newArea;
                        try {
                            newArea = RandoLogic.GenerateMap(Settings);
                        } catch (ThreadAbortException) {
                            return;
                        } catch (GenerationError e) {
                            errortext.Title = e.Message;
                            errortext.FadeVisible = true;
                            reenableMenu();
                            return;
                        } catch (Exception e) {
                            errortext.Title = "Encountered an error - Check log.txt for details";
                            Logger.LogDetailed(e, "randomizer");
                            errortext.FadeVisible = true;
                            reenableMenu();
                            return;
                        }
                        // save settings
                        RandoModule.Instance.SavedData.SavedSettings = Settings.Copy();
                        RandoModule.Instance.SaveSettings();
                        
                        this.entering = true;
                        RandoModule.StartMe = newArea;
                        while (RandoModule.StartMe != null) {
                            Thread.Sleep(10);
                        }
                        this.builderThread = null;
                        this.entering = false;
                    });
                    this.builderThread.Start();
                } else {
                    this.builderThread.Abort();
                    reenableMenu();
                }
            });

            menu.Add(seedtypetoggle);
            menu.Add(seedbutton);
            menu.Add(rulestoggle);
            menu.Add(mapbutton);
            menu.Add(mapcountlbl);
            menu.Add(logictoggle);
            menu.Add(lengthtoggle);
            menu.Add(numdashestoggle);
            menu.Add(difficultytoggle);
            menu.Add(moreoptionsbtn);
            menu.Add(repeatroomstoggle);
            menu.Add(enterunknowntoggle);
            menu.Add(enterunknowntext);
            menu.Add(shinetoggle);
            menu.Add(darktoggle);
            menu.Add(goldentoggle);
            menu.Add(variantstoggle);
            menu.Add(startbutton);
            menu.Add(hashtext);
            menu.Add(errortext);

            Scene.Add(menu);
        }
    }
}
