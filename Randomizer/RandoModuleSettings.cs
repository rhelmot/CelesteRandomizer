using System;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    public class RandoModuleSettings : EverestModuleSettings {
        [SettingIgnore]
        public Dictionary<uint, long> BestTimes { get; set; }

        [SettingIgnore]
        public Dictionary<Ruleset, RecordTuple> BestSetSeedTimes { get; set; }

        [SettingIgnore]
        public Dictionary<Ruleset, RecordTuple> BestRandomSeedTimes { get; set; }

        [SettingIgnore]
        public string CurrentVersion { get; set; }
    }

    public struct RecordTuple {
        public long Item1;
        public string Item2;
        
        public static RecordTuple Create(long a, string b) {
            return new RecordTuple {
                Item1 = a,
                Item2 = b,
            };
        }
    }

}