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

        public OuiRandoSettings() {
        }

        public override IEnumerator Enter(Oui from) {
            ReloadMenu();
            menu.Visible = Visible = true;
            menu.Focused = false;

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
                Selected && Input.MenuCancel.Pressed) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            }

            base.Update();
        }

        private void ReloadMenu() {
            menu = new TextMenu {
                new TextMenu.Header(Dialog.Clean("MODOPTIONS_RANDOMIZER_HEADER")),
                new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_START")).Pressed(() => {
                    Audio.SetMusic((string) null, true, true);
                    Audio.SetAmbience((string) null, true);
                    Audio.Play("event:/ui/main/savefile_begin");
                    SaveData.InitializeDebugMode();

                    /*AreaData newarea = AreaData.Areas[1].Copy();
                    newarea.ID = AreaData.Areas.Count;
                    newarea.SetSID("randomizer/random");
                    AreaData.Areas.Add(newarea);
                    newarea.Mode[1] = null;
                    newarea.Mode[2] = null;

                    AreaKey newkey = new AreaKey(newarea.ID);
                    Logger.Log("randomizer", $"new AreaKey({newarea.ID}).ID = {newkey.ID}");
                    newarea.Mode[0].MapData.Area = newkey;
                    //newarea.Mode[0].MapData.Data = newarea;

                    // test: swap rooms 2 and 3
                    LevelData lvl2 = newarea.Mode[0].MapData.Get("2");
                    LevelData lvl3 = newarea.Mode[0].MapData.Get("3");
                    Vector2 tmp = lvl2.Position;
                    lvl2.Position = lvl3.Position;
                    lvl3.Position = tmp;

                    Logger.Log("randomizer", "loading area " + newarea.ID.ToString() + " with key " + newkey.ToString());
                    Logger.Log("randomizer", "AreaData.Areas has " + AreaData.Areas.Count.ToString() + " elements");

                    LevelEnter.Go(new Session(newkey, (string) null, (AreaStats) null), true);*/
                    
                    Logger.Log("randomizer", "Processing level data...");
                    RandoLogic.ProcessAreas();
                    AreaKey newArea = RandoLogic.GenerateMap(1238, false);
                    LevelEnter.Go(new Session(newArea, null, null), true);
                })
            };

            Scene.Add(menu);
        }
    }
}
