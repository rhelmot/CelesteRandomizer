using Monocle;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Celeste.Mod.Randomizer {
    public class OuiRandoSettings : GenericOui {
        private float JournalEase;
        private bool Entering = false;
        private Thread BuilderThread = null;
            
        
        protected override bool IsDeeperThan(Oui other) {
            // deeper than everything but the journal and the text entry
            return !(other is OuiRandoRecords) && !(other is UI.OuiTextEntry);
        }

        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (start == RandoModule.STARTMODE_RANDOMIZER) {
                this.Add(new Coroutine(this.Enter(null)));
                return true;
            }
            return false;
        }
        
        public override void Render() {
            base.Render();

			if (this.JournalEase > 0f) {
				var position = new Vector2(128f * Ease.CubeOut(this.JournalEase), 952f);
                var color = Color.White * Ease.CubeOut(this.JournalEase);
				GFX.Gui["menu/journal"].DrawCentered (position, color);
				Input.GuiButton(Input.MenuJournal).Draw(position, Vector2.Zero, color);
			}
        }
        
        public override void Update() {
            base.Update();
            
            if ((this.Menu?.Active ?? false) && !this.Entering && this.BuilderThread == null && Input.MenuJournal.Pressed) {
                Audio.Play(SFX.ui_world_journal_select);
                Overworld.Goto<OuiRandoRecords>();
            }

			this.JournalEase = Calc.Approach(this.JournalEase, this.Menu?.Active ?? false ? 1f : 0f, Engine.DeltaTime * 4f);
        }

        private enum OptionsPages {
            Ruleset, Basic, Levels, Advanced, Cosmetic, Last
        }

        protected override Entity ReloadMenu() {
            var menu = new DisablableTextMenu {
                new TextMenu.Header(Dialog.Clean("MODOPTIONS_RANDOMIZER_HEADER"))
            };

            var currentPage = OptionsPages.Basic;
            var pages = new[] {new List<TextMenu.Item>(), new List<TextMenu.Item>(), new List<TextMenu.Item>(), new List<TextMenu.Item>(), new List<TextMenu.Item>()};

            var hashtext = new TextMenuExt.EaseInSubHeaderExt("{hash}", true, menu) {
                HeightExtra = -10f,
                Offset = new Vector2(30, -5),
            };
            void updateHashText() {
                hashtext.Title = "v" + RandoModule.Instance.VersionString;
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
            pages[1].Add(seedbutton);

            var seedtypetoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_SEEDTYPE"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_SEEDTYPE_" + Enum.GetNames(typeof(SeedType))[i].ToUpperInvariant());
            }, 0, (int)SeedType.Last - 1, (int)Settings.SeedType).Change((i) => {
                Settings.SeedType = (SeedType)i;
                seedbutton.Visible = Settings.SeedType == SeedType.Custom;
                // just in case...
                seedbutton.Label = Dialog.Clean("MODOPTIONS_RANDOMIZER_SEED") + ": " + Settings.Seed;
                updateHashText();
            });
            pages[1].Add(seedtypetoggle);

            var lengthtoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_LENGTH"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_LENGTH_" + Enum.GetNames(typeof(MapLength))[i].ToUpperInvariant());
            }, 0, (int)MapLength.Last - 1, (int)Settings.Length).Change((i) => {
                Settings.Length = (MapLength)i;
                updateHashText();
            });
            pages[1].Add(lengthtoggle);

            var numdashestoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_NUMDASHES"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_NUMDASHES_" + Enum.GetNames(typeof(NumDashes))[i].ToUpperInvariant());
            }, 0, (int)NumDashes.Last - 1, (int)Settings.Dashes).Change((i) => {
                Settings.Dashes = (NumDashes)i;
                updateHashText();
            });
            pages[1].Add(numdashestoggle);
            
            var endlesslivespicker = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_LIVES"), i => {
                return i == 0 ? Dialog.Clean("MODOPTIONS_RANDOMIZER_LIVES_INFINITE") : i.ToString();
            }, 0, 50, Settings.EndlessLives);
            endlesslivespicker.OnValueChange = i => {
                Settings.EndlessLives = i;
            };
            endlesslivespicker.Visible = Settings.Algorithm == LogicType.Endless;

            var logictoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_LOGIC"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_LOGIC_" + Enum.GetNames(typeof(LogicType))[i].ToUpperInvariant());
            }, 0, (int)LogicType.Last - 1, (int)Settings.Algorithm).Change((i) => {
                Settings.Algorithm = (LogicType)i;
                endlesslivespicker.Visible = Settings.Algorithm == LogicType.Endless;
                updateHashText();
            });
            pages[1].Add(logictoggle);
            pages[1].Add(endlesslivespicker);

            var difficultytoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_" + Enum.GetNames(typeof(Difficulty))[i].ToUpperInvariant());
            }, 0, (int)Difficulty.Last - 1, (int)Settings.Difficulty).Change((i) => {
                Settings.Difficulty = (Difficulty)i;
                updateHashText();
            });
            pages[1].Add(difficultytoggle);
            
            var mapinfo = this.MakeMapPicker(updateHashText);
            pages[2].AddRange(mapinfo.Item1);
            
            var strawberriestoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_STRAWBERRIES"), i => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_STRAWBERRIES_" + Enum.GetNames(typeof(StrawberryDensity))[i].ToUpperInvariant());
            }, 0, (int) StrawberryDensity.Last - 1, (int) Settings.Strawberries).Change(i => {
                Settings.Strawberries = (StrawberryDensity) i;
                updateHashText();
            });
            pages[3].Add(strawberriestoggle);

            var difficultycurvetoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTYCURVE"), i => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTYCURVE_" + Enum.GetNames(typeof(DifficultyEagerness))[i].ToUpperInvariant());
            }, 0, (int) DifficultyEagerness.Last - 1, (int) Settings.DifficultyEagerness).Change(i => {
                Settings.DifficultyEagerness = (DifficultyEagerness) i;
                updateHashText();
            });
            pages[3].Add(difficultycurvetoggle);

            var repeatroomstoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_REPEATROOMS"), Settings.RepeatRooms).Change((val) => {
                Settings.RepeatRooms = val;
                updateHashText();
            });
            pages[3].Add(repeatroomstoggle);

            var enterunknowntoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_ENTERUNKNOWN"), Settings.EnterUnknown).Change((val) => {
                Settings.EnterUnknown = val;
                updateHashText();
            });
            pages[3].Add(enterunknowntoggle);

            var goldentoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_GOLDENBERRY"), Settings.SpawnGolden).Change((val) => {
                Settings.SpawnGolden = val;
            });
            pages[3].Add(goldentoggle);

            var variantstoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_VARIANTS"), Settings.Variants).Change((val) => {
                Settings.Variants = val;
            });
            pages[3].Add(variantstoggle);

            var shinetoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_SHINE"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_SHINE_" + Enum.GetNames(typeof(ShineLights))[i].ToUpperInvariant());
            }, 0, (int)ShineLights.Last - 1, (int)Settings.Lights).Change((i) => {
                Settings.Lights = (ShineLights)i;
                updateHashText();
            });
            pages[4].Add(shinetoggle);

            var darktoggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_DARK"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_DARK_" + Enum.GetNames(typeof(Darkness))[i].ToUpperInvariant());
            }, 0, (int)Darkness.Last - 1, (int)Settings.Darkness).Change((i) => {
                Settings.Darkness = (Darkness)i;
                updateHashText();
            });
            pages[4].Add(darktoggle);

            var decorationstoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_DECORATIONS"), Settings.RandomDecorations).Change((val) => {
                Settings.RandomDecorations = val;
            });
            pages[4].Add(decorationstoggle);
            
            var colorstoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_COLORS"), Settings.RandomColors).Change((val) => {
                Settings.RandomColors = val;
            });
            pages[4].Add(colorstoggle);
            
            var bgstoggle = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_RANDOMIZER_BACKGROUNDS"), Settings.RandomBackgrounds).Change((val) => {
                Settings.RandomBackgrounds = val;
            });
            pages[4].Add(bgstoggle);

            var rulestoggles = new Dictionary<String, TextMenuExt.ButtonExt>();
            
            void syncModel() {
                repeatroomstoggle.Index = Settings.RepeatRooms ? 1 : 0;
                enterunknowntoggle.Index = Settings.EnterUnknown ? 1 : 0;
                variantstoggle.Index = Settings.Variants ? 1 : 0;
                logictoggle.Index = (int)Settings.Algorithm;
                lengthtoggle.Index = (int)Settings.Length;
                numdashestoggle.Index = (int)Settings.Dashes;
                difficultytoggle.Index = (int)Settings.Difficulty;
                difficultycurvetoggle.Index = (int)Settings.DifficultyEagerness;
                shinetoggle.Index = (int)Settings.Lights;
                darktoggle.Index = (int)Settings.Darkness;
                endlesslivespicker.Index = Settings.EndlessLives;
                strawberriestoggle.Index = (int)Settings.Strawberries;

                var locked = !String.IsNullOrEmpty(Settings.Rules);
                foreach (var item in pages[2]) {
                    item.Disabled = locked;
                }
                repeatroomstoggle.Disabled = locked;
                enterunknowntoggle.Disabled = locked;
                logictoggle.Disabled = locked;
                lengthtoggle.Disabled = locked;
                numdashestoggle.Disabled = locked;
                difficultytoggle.Disabled = locked;
                difficultycurvetoggle.Disabled = locked;
                shinetoggle.Disabled = locked;
                darktoggle.Disabled = locked;
                variantstoggle.Disabled = locked;
                endlesslivespicker.Disabled = locked;
                strawberriestoggle.Disabled = locked;

                var i = (OptionsPages) 0;
                foreach (var page in pages) {
                    var visible = i == currentPage;
                    foreach (var widget in page) {
                        widget.Visible = visible;
                    }
                    i++;
                }
                endlesslivespicker.Visible &= Settings.Algorithm == LogicType.Endless;
                seedbutton.Visible &= Settings.SeedType == SeedType.Custom;

                foreach (var kv in rulestoggles) {
                    //kv.Value.Visible &= kv.Key == "" || RandoModule.Instance.MetaConfig.RulesetsDict[kv.Key].Algorithm == this.Settings.Algorithm;
                    kv.Value.Icon = kv.Key == this.Settings.Rules ? "menu/poemarrow" : "";
                }

                mapinfo.Item2();
            }
            
            var pageToggle = new TextMenu.Slider(Dialog.Clean("MODOPTIONS_RANDOMIZER_OPTIONS"), (i) => {
                return Dialog.Clean("MODOPTIONS_RANDOMIZER_OPTIONS_" + Enum.GetNames(typeof(OptionsPages))[i].ToUpperInvariant());
            }, 0, (int)OptionsPages.Last - 1, (int)currentPage).Change((i) => {
                currentPage = (OptionsPages)i;
                syncModel();
                menu.RecalculateSize();
                menu.Position.Y = menu.ScrollTargetY;
            });

            void syncRuleset() {
                Settings.Enforce();
                syncModel();
                updateHashText();
            }

            var nullToggle = new TextMenuExt.ButtonExt(Dialog.Clean("MODOPTIONS_RANDOMIZER_RULES_CUSTOM"));
            nullToggle.Pressed(this.MakeRulesetToggler("", syncRuleset));
            rulestoggles.Add("", nullToggle);
            pages[0].Add(nullToggle);
    
            var sortedrules = new List<RandoMetadataRuleset>(RandoModule.Instance.MetaConfig.Rulesets);
            sortedrules.Sort((a, b) => a.Name.CompareTo(b.Name));
            foreach (var ruleset in sortedrules) {
                var toggle = new TextMenuExt.ButtonExt(ruleset.LongName);
                toggle.Pressed(this.MakeRulesetToggler(ruleset.Name, syncRuleset));
                rulestoggles.Add(ruleset.Name, toggle);
                pages[0].Add(toggle);
            }

            var startbutton = new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_START"));
            startbutton.Pressed(() => {
                if (this.Entering) {
                    return;
                }

                void reenableMenu() {
                    this.BuilderThread = null;

                    startbutton.Label = Dialog.Clean("MODOPTIONS_RANDOMIZER_START");
                    updateHashText();
                    menu.DisableMovement = false;
                }

                if (this.BuilderThread == null) {
                    errortext.FadeVisible = false;
                    startbutton.Label = Dialog.Clean("MODOPTIONS_RANDOMIZER_CANCEL");
                    hashtext.Title += " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_GENERATING");
                    menu.DisableMovement = true;

                    this.BuilderThread = new Thread(() => {
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
                        
                        this.Entering = true;
                        RandoModule.StartMe = newArea;
                        while (RandoModule.StartMe != null) {
                            Thread.Sleep(10);
                        }
                        this.BuilderThread = null;
                        this.Entering = false;
                    });
                    this.BuilderThread.Start();
                } else {
                    this.BuilderThread.Abort();
                    reenableMenu();
                }
            });

            menu.Add(startbutton);
            menu.Add(hashtext);
            menu.Add(errortext);
            menu.Add(pageToggle);
            foreach (var page in pages) {
                foreach (var item in page) {
                    menu.Add(item);
                }
            }

            Scene.Add(menu);
            syncModel();

            menu.OnCancel = () => {
                if (this.Entering || this.BuilderThread != null) {
                    return;
                }
                
                // save settings
                RandoModule.Instance.SavedData.SavedSettings = Settings.Copy();
                RandoModule.Instance.SaveSettings();

                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            };

            menu.OnPause = () => {
                if (this.Entering || this.BuilderThread != null) {
                    return;
                }
                
                Audio.Play(SFX.ui_main_button_select);
                menu.Selection = 1;
                menu.Current.OnPressed();
            };

            menu.Selection = 1;
            return menu;
        }

        private Tuple<List<TextMenu.Item>, Action> MakeMapPicker(Action syncOutward) {
            var menu = new List<TextMenu.Item>();
            var toggleAll = new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_TOGGLEALL")).Pressed(() => {
                TextMenu.OnOff firstToggle = null;
                foreach (var item in menu) {
                    if (item is TextMenu.OnOff) {
                        firstToggle = item as TextMenu.OnOff;
                        break;
                    }
                }

                if (firstToggle == null) {
                    // ???
                    return;
                }

                var newValue = 1 - firstToggle.Index;
                foreach (var item in menu) {
                    if (item is TextMenu.OnOff toggle) {
                        toggle.Index = newValue;
                        toggle.OnValueChange(toggle.Values[newValue].Item2);
                    }
                }
            });

            menu.Add(toggleAll);
            
            var mapcountlbl = new TextMenuExt.SubHeaderExt(Settings.LevelCount.ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS")) {
                HeightExtra = -10f,
                Offset = new Vector2(30, -5),
            };
            menu.Add(mapcountlbl);

            void syncTotal() {
                mapcountlbl.Title = Settings.LevelCount.ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS");
            }

            void syncInner() {
                syncOutward();
                syncTotal();
            }
            
            Action AddAreaToggle(string name, AreaKey key) {
                var toggle = new TextMenu.OnOff(name, false);
                Action syncFunc = () => {
                    var on = Settings.MapIncluded(key);
                    toggle.Index = on ? 1 : 0;
                };
                var numLevels = RandoLogic.LevelCount[new RandoSettings.AreaKeyNotStupid(key)];
                menu.Add(toggle.Change(this.MakeChangeFunc(key, syncInner)));
                menu.Add(new TextMenuExt.SubHeaderExt(numLevels.ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS")) {
                    HeightExtra = -10f,
                    Offset = new Vector2(30, -5),
                });
                syncFunc();
                return syncFunc;
            }

            Action AddLevelSetToggle(string name, List<AreaKey> keys) {
                var toggle = new TextMenu.OnOff(name, false);
                Action syncFunc = () => {
                    var on = Settings.MapIncluded(keys[0]);
                    toggle.Index = on ? 1 : 0;
                };
                var numLevels = 0;
                foreach (AreaKey key in keys) {
                    numLevels += RandoLogic.LevelCount[new RandoSettings.AreaKeyNotStupid(key)];
                }
                menu.Add(toggle.Change(this.MakeChangeFunc(keys, syncInner)));
                menu.Add(new TextMenuExt.SubHeaderExt(numLevels.ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS")) {
                    HeightExtra = -10f,
                    Offset = new Vector2(30, -5),
                });
                syncFunc();
                return syncFunc;
            }

            Action AddLevelSetMenu(string levelSetID) {
                List<AreaKey> keys = RandoLogic.LevelSets[levelSetID];
                var syncFuncs = new List<Action>();
                menu.Add(new TextMenu.SubHeader(DialogExt.CleanLevelSet(keys[0].GetLevelSet())));
                foreach (var key in keys) {
                    var area = AreaData.Get(key);
                    var name = area.Name;
                    name = name.DialogCleanOrNull() ?? name.SpacedPascalCase();
                    if (key.Mode != AreaMode.Normal || (area.Mode.Length != 1 && area.Mode[1] != null)) {
                        name += " " + Char.ToString((char)('A' + (int)key.Mode));
                    }

                    syncFuncs.Add(AddAreaToggle(name, key));
                }

                return () => {
                    foreach (var a in syncFuncs) {
                        a();
                    }
                    syncTotal();
                };
            }

            var allSyncs = new List<Action>();

            // Create submenu for Celeste, campaigns, then other levelsets
            allSyncs.Add(AddLevelSetMenu("Celeste"));
            List<string> completedLevelSets = new List<string> { "Celeste" };

            var campaigns = RandoModule.Instance.MetaConfig.Campaigns;
            foreach (RandoMetadataCampaign campaign in campaigns) {
                menu.Add(new TextMenu.SubHeader(DialogExt.CleanLevelSet(campaign.Name)));
                foreach (RandoMetadataLevelSet levelSet in campaign.LevelSets) {
                    var name = levelSet.Name;
                    if (RandoLogic.LevelSets.TryGetValue(levelSet.ID, out var keys)) {
                        allSyncs.Add(AddLevelSetToggle(name, keys));
                        completedLevelSets.Add(levelSet.ID);
                    }
                }
            }

            foreach (string levelSet in RandoLogic.LevelSets.Keys) {
                if (!completedLevelSets.Contains(levelSet)) {
                    allSyncs.Add(AddLevelSetMenu(levelSet));
                }
            }
            
            // If Celeste is not the only levelset, Reset should turn all other levelsets off
            if (RandoLogic.LevelSets.Count > 1) {
                menu.Insert(2, new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_RESET")).Pressed(() => {
                    Settings.SetNormalMaps();
                    // this is a stupid way to do this
                    int levelsetIdx = -1;
                    foreach (var item in menu) {
                        if (item is TextMenu.SubHeader && !(item is TextMenuExt.SubHeaderExt)) {
                            levelsetIdx++;
                        } else if (item is TextMenu.OnOff toggle) {
                            toggle.Index = levelsetIdx == 0 ? 1 : 0;
                        }
                    }
                }));
            }

            Action finalSync = () => {
                foreach (var aa in allSyncs) {
                    aa();
                }
            };

            return Tuple.Create(menu, finalSync);
        }

        private Action<bool> MakeChangeFunc(AreaKey key, Action syncParent) {
            // I have no idea if this is necessary in c#. It's a weird edge case in closure behavior.
            // I would imagine it is but maybe that's me being a python dweeb
            return (on) => {
                if (on) {
                    this.Settings.EnableMap(key);
                } else {
                    this.Settings.DisableMap(key);
                }

                syncParent();
            };
        }

        private Action<bool> MakeChangeFunc(List<AreaKey> keys, Action syncParent) {
            return (on) => {
                if (on) {
                    foreach (AreaKey key in keys) {
                        this.Settings.EnableMap(key);
                    }
                } else {
                    foreach (AreaKey key in keys) {
                        this.Settings.DisableMap(key);
                    }
                }

                syncParent();
            };
        }

        private Action MakeRulesetToggler(string rules, Action syncParent) {
            return () => {
                this.Settings.Rules = rules;
                syncParent();
            };
        }
    }
}
