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

            var enterunknowntoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_ENTERUNKNOWN"), Settings.EnterUnknown).Change((val) => {
                Settings.EnterUnknown = val;
                updateHashText();
            });

            var shinetoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_SHINE"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_SHINE_" + Enum.GetNames(typeof(ShineLights))[i].ToUpperInvariant());
            }, 0, (int)ShineLights.Last - 1, (int)Settings.Lights).Change((i) => {
                Settings.Lights = (ShineLights)i;
                updateHashText();
            });

            var goldentoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_GOLDENBERRY"), Settings.SpawnGolden).Change((val) => {
                Settings.SpawnGolden = val;
            });

            var moreoptions = false;
            repeatroomstoggle.Visible = false;
            enterunknowntoggle.Visible = false;
            goldentoggle.Visible = false;
            shinetoggle.Visible = false;

            var moreoptionsbtn = new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_MOREOPTIONS"));
            moreoptionsbtn.Pressed(() => {
                moreoptions = !moreoptions;
                moreoptionsbtn.Label = moreoptions ? Dialog.Clean("MODOPTIONS_RANDOMIZER_FEWEROPTIONS") : Dialog.Clean("MODOPTIONS_RANDOMIZER_MOREOPTIONS");

                repeatroomstoggle.Visible = moreoptions;
                enterunknowntoggle.Visible = moreoptions;
                goldentoggle.Visible = moreoptions;
                shinetoggle.Visible = moreoptions;
            });

            void syncModel() {
                repeatroomstoggle.Index = Settings.RepeatRooms ? 1 : 0;
                enterunknowntoggle.Index = Settings.EnterUnknown ? 1 : 0;
                logictoggle.Index = (int)Settings.Algorithm;
                lengthtoggle.Index = (int)Settings.Length;
                numdashestoggle.Index = (int)Settings.Dashes;
                difficultytoggle.Index = (int)Settings.Difficulty;
                shinetoggle.Index = (int)Settings.Lights;
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
                        } catch (Exception e) {
                            if (e.Message == "Could not generate map") {
                                errortext.Title = e.Message;
                            } else {
                                errortext.Title = "Encountered an error - Check log.txt for details";
                                Logger.LogDetailed(e, "randomizer");
                            }
                            errortext.FadeVisible = true;
                            reenableMenu();
                            return;
                        }
                        this.entering = true;

                        Audio.SetMusic((string)null, true, true);
                        Audio.SetAmbience((string)null, true);
                        Audio.Play("event:/ui/main/savefile_begin");

                        // use the debug file
                        SaveData.InitializeDebugMode();
                        // turn on variants mode
                        SaveData.Instance.VariantMode = true;
                        SaveData.Instance.AssistMode = false;
                        // clear summit gems, just in case!
                        SaveData.Instance.SummitGems = new bool[6];
                        // mark as completed to spawn golden berry
                        SaveData.Instance.Areas[newArea.ID].Modes[0].Completed = true;

                        var fade = new FadeWipe(this.Scene, false, () => {   // assign to variable to suppress compiler warning
                            var session = new Session(newArea, null, null);
                            session.FirstLevel = false;
                            LevelEnter.Go(session, true);
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
            menu.Add(shinetoggle);
            menu.Add(goldentoggle);
            menu.Add(startbutton);
            menu.Add(hashtext);
            menu.Add(errortext);

            Scene.Add(menu);
        }
    }
}
