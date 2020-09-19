using System;
using System.Collections;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public class OuiRandoRecords : Oui {
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
                Selected && (Input.MenuCancel.Pressed)) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiRandoSettings>();
            }

            base.Update();
        }

        private void ReloadMenu() {
            menu = new TextMenu {
                new TextMenu.Header(Dialog.Clean("MODOPTIONS_RANDOMIZER_RECORDS_HEADER")),
            };

            menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_RANDOMIZER_RECORDS_RANDOM")));
            AddAllRecords(RandoModule.Instance.SavedData.BestRandomSeedTimes);
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_RANDOMIZER_RECORDS_SET")));
            AddAllRecords(RandoModule.Instance.SavedData.BestSetSeedTimes);

            Scene.Add(menu);
        }

        private void AddAllRecords(Dictionary<Ruleset, RecordTuple> records) {
            for (var i = Ruleset.A; i < Ruleset.Last; i++) {
                if (records.TryGetValue(i, out var record)) {
                    AddRecord(i, record);
                }
            }
        }

        private void AddRecord(Ruleset rules, RecordTuple record) {
            string formatted = (rules == Ruleset.G || rules == Ruleset.H) ? record.Item1.ToString() : Dialog.Time(record.Item1);
            menu.Add(new TextMenu.Button(Dialog.Clean("MODOPTIONS_RANDOMIZER_RULES_" + rules) + ": " + formatted + " (" + record.Item2 + ")").Pressed(() => {
                Settings.Rules = rules;
                Settings.SeedType = SeedType.Custom;
                Settings.Seed = record.Item2;
                Settings.Enforce();
                Overworld.Goto<OuiRandoSettings>();
            }));
        }
    }
}
