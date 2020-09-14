using System;
using System.Collections;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public class OuiMapPicker : Oui {
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

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && (Input.MenuCancel.Pressed || Input.Pause.Pressed)) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiRandoSettings>();
            }

            base.Update();
        }

        private void ReloadMenu() {
            menu = new TextMenu {
                new TextMenu.Header(Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_HEADER")),
                new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_TOGGLEALL")).Pressed(() => {
                    var items = menu.GetItems();
                    TextMenu.OnOff firstToggle = null;
                    foreach (var item in items) {
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
                    for (int i = 0; i < items.Count; i++) {
                        if (items[i] is TextMenu.OnOff toggle) {
                            toggle.Index = newValue;
                            toggle.OnValueChange(toggle.Values[newValue].Item2);
                        }
                    }
                }),
            };

            var lvlCount = new Dictionary<RandoSettings.AreaKeyNotStupid, int>();
            foreach (var room in RandoLogic.AllRooms) {
                var notstupid = new RandoSettings.AreaKeyNotStupid(room.Area);
                if (lvlCount.TryGetValue(notstupid, out int c)) {
                    lvlCount[notstupid] = c + 1;
                } else {
                    lvlCount[notstupid] = 1;
                }
            }

            // Create submenu for each level set
            foreach (RandoLogic.LevelSet levelSet in RandoLogic.LevelSets) {
                menu.Add(new TextMenu.SubHeader(DialogExt.CleanLevelSet(levelSet.name)));
                // Add custom groups first
                foreach (string group in levelSet.customGroupNames) {
                    var name = group.DialogCleanOrNull() ?? group.SpacedPascalCase();
                    var areaKeys = levelSet.customGroups[group];
                    var on = Settings.MapIncluded(areaKeys[0]);
                    var numLevels = 0;
                    foreach (AreaKey key in areaKeys) {
                        numLevels += lvlCount[new RandoSettings.AreaKeyNotStupid(key)];
                    }
                    menu.Add(new TextMenu.OnOff(name, on).Change(this.MakeChangeFunc(areaKeys)));
                    menu.Add(new TextMenuExt.SubHeaderExt(numLevels.ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS")) {
                        HeightExtra = -10f,
                        Offset = new Vector2(30, -5),
                    });
                }
                // Now add standalone areas
                foreach (AreaKey key in levelSet.ungroupedKeys) {
                    var area = AreaData.Get(key);
                    var mode = AreaData.GetMode(key);
                    var on = Settings.MapIncluded(key);
                    var name = area.Name;
                    name = name.DialogCleanOrNull() ?? name.SpacedPascalCase();
                    if (key.Mode != AreaMode.Normal || (area.Mode.Length != 1 && area.Mode[1] != null)) {
                        name += " " + Char.ToString((char)('A' + (int)key.Mode));
                    }
                    menu.Add(new TextMenu.OnOff(name, on).Change(this.MakeChangeFunc(key)));
                    menu.Add(new TextMenuExt.SubHeaderExt(lvlCount[new RandoSettings.AreaKeyNotStupid(key)].ToString() + " " + Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_LEVELS")) {
                        HeightExtra = -10f,
                        Offset = new Vector2(30, -5),
                    });
                }
            }

            if (RandoLogic.LevelSets.Count > 1) {
                menu.Insert(2, new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_MAPPICKER_RESET")).Pressed(() => {
                    Settings.SetNormalMaps();
                    // this is a stupid way to do this
                    int levelsetIdx = -1;
                    foreach (var item in menu.GetItems()) {
                        if (item is TextMenu.SubHeader && !(item is TextMenuExt.SubHeaderExt)) {
                            levelsetIdx++;
                        } else if (item is TextMenu.OnOff toggle) {
                            toggle.Index = levelsetIdx == 0 ? 1 : 0;
                        }
                    }
                }));
            }

            Scene.Add(menu);
        }

        private Action<bool> MakeChangeFunc(AreaKey key) {
            // I have no idea if this is necessary in c#. It's a weird edge case in closure behavior.
            // I would imagine it is but maybe that's me being a python dweeb
            return (on) => {
                if (on) {
                    Settings.EnableMap(key);
                } else {
                    Settings.DisableMap(key);
                }
            };
        }

        private Action<bool> MakeChangeFunc(List<AreaKey> keys)
        {
            return (on) => {
                if (on)
                {
                    foreach (AreaKey key in keys)
                    {
                        Settings.EnableMap(key);
                    }
                }
                else
                {
                    foreach (AreaKey key in keys)
                    {
                        Settings.DisableMap(key);
                    }
                }
            };
        }
    }
}
