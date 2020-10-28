using System;
using System.Collections;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public class OuiRandoRecords : GenericOui {
        protected override bool IsDeeperThan(Oui other) {
            // there is nothing deeper than us
            return true;
        }

        protected override Entity ReloadMenu() {
            var menu = new TextMenu {
                new TextMenu.Header(Dialog.Clean("MODOPTIONS_RANDOMIZER_RECORDS_HEADER")),
            };

            menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_RANDOMIZER_RECORDS_RANDOM")));
            this.AddAllRecords(menu, RandoModule.Instance.SavedData.BestRandomSeedTimes);
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_RANDOMIZER_RECORDS_SET")));
            this.AddAllRecords(menu, RandoModule.Instance.SavedData.BestSetSeedTimes);

            menu.OnCancel = () => {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiRandoSettings>();
            };

            return menu;
        }

        private void AddAllRecords(TextMenu menu, Dictionary<string, RecordTuple> records) {
            foreach (var ruleset in RandoModule.Instance.MetaConfig.Rulesets) {
                if (records.TryGetValue(ruleset.Name, out var record)) {
                    AddRecord(menu, ruleset.LongName, record, ruleset.Algorithm == LogicType.Endless);
                }
            }
        }

        private void AddRecord(TextMenu menu, string rules, RecordTuple record, bool isEndless) {
            string formatted = isEndless ? record.Item1.ToString() : Dialog.Time(record.Item1);
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
