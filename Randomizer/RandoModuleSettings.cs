using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Randomizer {
    public class RandoModuleSettings : EverestModuleSettings {
        [SettingIgnore]
        public Dictionary<uint, long> BestTimes { get; set; }

        [SettingIgnore]
        public Dictionary<Ruleset, long> BestSetSeedTimes { get; set; }

        [SettingIgnore]
        public Dictionary<Ruleset, long> BestRandomSeedTimes { get; set; }

        [SettingIgnore]
        public string CurrentVersion { get; set; }
    }
}