using System;
using System.Linq;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Randomizer {
    public enum SeedType {
        Random,
        Custom,
        Last
    }

    public enum LogicType {
        Pathway,
        Labyrinth,
        Endless,
        Last
    }

    public enum MapLength {
        Short,
        Medium,
        Long,
        Enormous,
        Last
    }

    public enum NumDashes {
        Zero,
        One, 
        Two,
        Last
    }

    public enum Difficulty {
        Easy,
        Normal,
        Hard,
        Expert,
        Master,
        Perfect,
        Last
    }

    public enum DifficultyEagerness {
        None,
        Low,
        Medium,
        High,
        Last
    }

    public enum ShineLights {
        Off,
        Hubs,
        On,
        Last
    }

    public enum Darkness {
        Never,
        Vanilla,
        Always,
        Last,
    }

    public enum StrawberryDensity {
        None,
        Low,
        High,
        Last,
    }

    public class RandoSettings {
        public SeedType SeedType;
        public string Seed = "achene";
        public string Rules = "";
        [YamlIgnore]
        public RandoMetadataRuleset Ruleset {
            get {
                if (RandoModule.Instance.MetaConfig.RulesetsDict.TryGetValue(this.Rules, out var result)) {
                    return result;
                }
                this.Rules = "";
                return null;
            }
        }
        public bool RepeatRooms;
        public bool EnterUnknown;
        public bool SpawnGolden;
        public bool Variants = true;
        public bool RandomDecorations = true;
        public bool RandomColors = true;
        public bool RandomBackgrounds = true;
        public int EndlessLives = 3;
        public LogicType Algorithm;
        public MapLength Length;
        public NumDashes Dashes = NumDashes.One;
        public Difficulty Difficulty;
        public DifficultyEagerness DifficultyEagerness = DifficultyEagerness.Low;
        public ShineLights Lights = ShineLights.Hubs;
        public Darkness Darkness = Darkness.Vanilla;
        public StrawberryDensity Strawberries = StrawberryDensity.Low;
        [YamlIgnore]
        public HashSet<AreaKeyNotStupid> IncludedMaps = new HashSet<AreaKeyNotStupid>();
        [YamlIgnore]
        public int EndlessLevel;

        [YamlIgnore] public bool IsLabyrinth => this.Algorithm == LogicType.Labyrinth || (this.Algorithm == LogicType.Endless && this.EndlessLevel % 5 == 4);
        [YamlIgnore] public bool HasLives => this.Algorithm == LogicType.Endless && this.EndlessLives != 0;

        public List<AreaKeyNotStupid> IncludedMapsList {
            get => new List<AreaKeyNotStupid>(this.IncludedMaps);
            set => this.IncludedMaps = new HashSet<AreaKeyNotStupid>(value);
        }

        public void PruneMaps() {
            this.IncludedMaps.RemoveWhere(a => a.ID == -1);
        }

        public void Enforce() {
            if (this.SeedType == SeedType.Random) {
                this.Seed = "";
                var ra = new Random();
                for (int i = 0; i < 6; i++) {
                    var val = ra.Next(36);
                    if (val < 10) {
                        this.Seed += ((char)('0' + val)).ToString();
                    } else {
                        this.Seed += ((char)('a' + val - 10)).ToString();
                    }
                }
            }

            var r = this.Ruleset;
            if (r != null) {
                if (r.EnabledMaps == null) {
                    this.SetNormalMaps();
                } else {
                    this.IncludedMaps = new HashSet<AreaKeyNotStupid>(r.EnabledMaps);
                }

                this.RepeatRooms = r.RepeatRooms;
                this.EnterUnknown = r.EnterUnknown;
                this.Variants = r.Variants;
                this.Algorithm = r.Algorithm;
                this.Length = r.Length;
                this.Dashes = r.Dashes;
                this.Difficulty = r.Difficulty;
                this.DifficultyEagerness = r.DifficultyEagerness;
                this.Lights = r.Lights;
                this.Darkness = r.Darkness;
            }
        }

        public void SetNormalMaps() {
            this.DisableAllMaps();
            foreach (var key in RandoLogic.LevelSets["Celeste"]) {
                this.EnableMap(key);
            }
        }

        private IEnumerable<uint> HashParts() {
            yield return (uint)RandoModule.Instance.Metadata.Version.Major;
            yield return (uint)RandoModule.Instance.Metadata.Version.Minor;
            yield return (uint)RandoModule.Instance.Metadata.Version.Build;
            yield return this.IntSeed;
            yield return RepeatRooms ? 1u : 0u;
            yield return EnterUnknown ? 1u : 0u;
            yield return (uint)Algorithm;
            yield return (uint)Length;
            yield return (uint)Dashes;
            yield return (uint)Difficulty;
            yield return (uint)DifficultyEagerness;
            yield return (uint)Lights;
            yield return (uint)Darkness;

            var sortedMaps = new List<AreaKeyNotStupid>(IncludedMaps.Where(key => key.ID != -1));
            sortedMaps.Sort((AreaKeyNotStupid x, AreaKeyNotStupid y) => {
                var xs = x.Stupid.GetSID();
                var ys = y.Stupid.GetSID();
                var cmp1 = xs.CompareTo(ys);
                if (cmp1 != 0) {
                    return cmp1;
                }
                if (x.Mode < y.Mode) {
                    return -1;
                }
                if (x.Mode > y.Mode) {
                    return 1;
                }
                return 0;
            });
            foreach (var thing in sortedMaps) {
                yield return (uint)thing.Mode;
                foreach (var ch in thing.Stupid.GetSID()) {
                    yield return (uint)ch;
                }
                yield return 0u;
            }
        }

        [YamlIgnore]
        public string Hash {
            get => djb2(this.HashParts()).ToString();
        }

        [YamlIgnore]
        public uint IntSeed {
            get {
                var euniSeed = this.Seed.Length <= 10;
                foreach (var i in this.Seed) {
                    if (!Char.IsDigit(i)) {
                        euniSeed = false;
                        break;
                    }
                }
                if (euniSeed) {
                    var big = ulong.Parse(this.Seed);
                    if (big <= (ulong)uint.MaxValue) {
                        return (uint)big;
                    }
                }

                return djb2(this.Seed);
            }
        }

        public static uint djb2(IEnumerable<uint> parts) {
            uint h = 5381;
            foreach (var i in parts) {
                h = ((h << 5) + h) + i;
            }
            return h;
        }

        public static uint djb2(string parts) {
            return djb2(parts.Select(c => (uint) c));
        }

        [YamlIgnore]
        public int LevelCount {
            get {
                int sum = 0;
                foreach (var room in RandoLogic.AllRooms) {
                    if (this.MapIncluded(room.Area)) {
                        sum++;
                    }
                }
                return sum;
            }
        }

        public struct AreaKeyNotStupid {
            public string SID;
            public AreaMode Mode;

            [YamlIgnore]
            public int ID {
                get {
                    var data = AreaDataExt.Get(this.SID);
                    if (data == null) {
                        return -1;
                    }
                    return data.ID;
                }
            }

            public AreaKeyNotStupid(AreaKey Stupid) {
                this.SID = Stupid.GetSID();
                this.Mode = Stupid.Mode;
            }

            [YamlIgnore]
            public AreaKey Stupid {
                get {
                    return new AreaKey(this.ID, this.Mode);
                }
            }
        }

        public bool MapIncluded(AreaKey map) {
            return this.IncludedMaps.Contains(new AreaKeyNotStupid(map));
        }

        public void EnableMap(AreaKey map) {
            this.IncludedMaps.Add(new AreaKeyNotStupid(map));
        }

        public void DisableMap(AreaKey map) {
            this.IncludedMaps.Remove(new AreaKeyNotStupid(map));
        }

        public void DisableAllMaps() {
            this.IncludedMaps.Clear();
        }

        [YamlIgnore]
        public IEnumerable<AreaKey> EnabledMaps {
            get {
                var result = new List<AreaKey>();
                foreach (var area in this.IncludedMaps) {
                    result.Add(area.Stupid);
                }
                return result;
            }
        }
    }
}
