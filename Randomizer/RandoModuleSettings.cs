using System;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    public class RandoModuleSettings : EverestModuleSettings {
        [SettingIgnore]
        public Dictionary<uint, long> BestTimes { get; set; }

        [SettingIgnore]
        public Dictionary<Ruleset, Tuple<long, string>> BestSetSeedTimes { get; set; }

        [SettingIgnore]
        public Dictionary<Ruleset, Tuple<long, string>> BestRandomSeedTimes { get; set; }

        [SettingIgnore]
        public string CurrentVersion { get; set; }
    }
}