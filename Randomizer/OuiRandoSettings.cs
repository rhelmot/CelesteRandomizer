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

        private int seed;
        private const int MAX_SEED_DIGITS = 6;

        public OuiRandoSettings() {
            var r = new Random();
            seed = r.Next((int)Math.Pow(10, MAX_SEED_DIGITS));
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
            new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_SEED") + ": " + seed.ToString(MAX_SEED_DIGITS)).Pressed(() => {
                    Audio.Play(SFX.ui_main_savefile_rename_start);
                    menu.SceneAs<Overworld>().Goto<UI.OuiNumberEntry>().Init<OuiRandoSettings>(
                        seed,
                        (v) => this.seed = (int)v,
                        MAX_SEED_DIGITS,
                        false,
                        false);
                }),
                    

            new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_START")).Pressed(() => {
                    RandoLogic.ProcessAreas();

                    AreaKey newArea = RandoLogic.GenerateMap(seed, false);
                    Audio.SetMusic((string) null, true, true);
                    Audio.SetAmbience((string) null, true);
                    Audio.Play("event:/ui/main/savefile_begin");
                    SaveData.InitializeDebugMode();

                    LevelEnter.Go(new Session(newArea, null, null), true);

                    /*foreach (AreaData area in AreaData.Areas) {
                        Logger.Log("randomizer", $"Skeleton for {area.GetSID()}");
                        RandoConfigFile.YamlSkeleton(area);

                    }*/
                })
            };

            Scene.Add(menu);
        }
    }
}
