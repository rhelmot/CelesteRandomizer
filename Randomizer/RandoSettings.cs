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

    public enum Ruleset {
        Custom,
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
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
        Normal,
        Hard,
        Expert,
        Perfect,
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

    public class RandoSettings {
        public SeedType SeedType;
        public string Seed = "achene";
        public Ruleset Rules;
        public bool RepeatRooms;
        public bool EnterUnknown;
        public bool SpawnGolden;
        public bool Variants = true;
        public bool RandomDecorations = true;
        public int EndlessLives = 3;
        public LogicType Algorithm;
        public MapLength Length;
        public NumDashes Dashes = NumDashes.One;
        public Difficulty Difficulty;
        public ShineLights Lights = ShineLights.Hubs;
        public Darkness Darkness = Darkness.Vanilla;
        [YamlIgnore]
        public HashSet<AreaKeyNotStupid> IncludedMaps = new HashSet<AreaKeyNotStupid>();
        [YamlIgnore]
        public int EndlessLevel;

        [YamlIgnore] public bool IsLabyrinth => this.Algorithm == LogicType.Labyrinth || (this.Algorithm == LogicType.Endless && this.EndlessLevel % 5 == 4);

        public List<AreaKeyNotStupid> IncludedMapsList {
            get => new List<AreaKeyNotStupid>(this.IncludedMaps);
            set => this.IncludedMaps = new HashSet<AreaKeyNotStupid>(value);
        }

        public void Enforce() {
            if (this.SeedType == SeedType.Random) {
                this.Seed = "";
                var r = new Random();
                for (int i = 0; i < 6; i++) {
                    var val = r.Next(36);
                    if (val < 10) {
                        this.Seed += ((char)('0' + val)).ToString();
                    } else {
                        this.Seed += ((char)('a' + val - 10)).ToString();
                    }
                }
            }
            switch (this.Rules) {
                case Ruleset.A:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Variants = false;
                    this.Algorithm = LogicType.Pathway;
                    this.Length = MapLength.Short;
                    this.Dashes = NumDashes.One;
                    this.Difficulty = Difficulty.Normal;
                    this.Lights = ShineLights.Hubs;
                    this.Darkness = Darkness.Never;
                    break;
                case Ruleset.B:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Variants = false;
                    this.Algorithm = LogicType.Pathway;
                    this.Length = MapLength.Medium;
                    this.Dashes = NumDashes.Two;
                    this.Difficulty = Difficulty.Normal;
                    this.Lights = ShineLights.Hubs;
                    this.Darkness = Darkness.Never;
                    break;
                case Ruleset.C:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Variants = false;
                    this.Algorithm = LogicType.Pathway;
                    this.Length = MapLength.Medium;
                    this.Dashes = NumDashes.One;
                    this.Difficulty = Difficulty.Expert;
                    this.Lights = ShineLights.Hubs;
                    this.Darkness = Darkness.Vanilla;
                    break;
                case Ruleset.D:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Variants = false;
                    this.Algorithm = LogicType.Pathway;
                    this.Length = MapLength.Long;
                    this.Dashes = NumDashes.Two;
                    this.Difficulty = Difficulty.Expert;
                    this.Lights = ShineLights.Hubs;
                    this.Darkness = Darkness.Vanilla;
                    break;
                case Ruleset.E:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Variants = false;
                    this.Algorithm = LogicType.Labyrinth;
                    this.Length = MapLength.Medium;
                    this.Dashes = NumDashes.One;
                    this.Difficulty = Difficulty.Normal;
                    this.Lights = ShineLights.Hubs;
                    this.Darkness = Darkness.Never;
                    break;
                case Ruleset.F:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Variants = false;
                    this.Algorithm = LogicType.Labyrinth;
                    this.Length = MapLength.Medium;
                    this.Dashes = NumDashes.Two;
                    this.Difficulty = Difficulty.Hard;
                    this.Lights = ShineLights.Hubs;
                    this.Darkness = Darkness.Never;
                    break;
                case Ruleset.G:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Variants = false;
                    this.Algorithm = LogicType.Endless;
                    this.Length = MapLength.Short;
                    this.Dashes = NumDashes.One;
                    this.Difficulty = Difficulty.Normal;
                    this.Lights = ShineLights.On;
                    this.Darkness = Darkness.Vanilla;
                    this.EndlessLives = 3;
                    break;
                case Ruleset.H:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Variants = false;
                    this.Algorithm = LogicType.Endless;
                    this.Length = MapLength.Short;
                    this.Dashes = NumDashes.One;
                    this.Difficulty = Difficulty.Hard;
                    this.Lights = ShineLights.On;
                    this.Darkness = Darkness.Vanilla;
                    this.EndlessLives = 5;
                    break;
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
            get {
                // djb2 impl
                uint h = 5381;
                foreach (var i in this.HashParts()) {
                    h = ((h << 5) + h) + i;
                }
                return h.ToString();
            }
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

                // djb2 impl
                uint h = 5381;
                foreach (var i in this.Seed) {
                    h = ((h << 5) + h) + i;
                }
                return h;
            }
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
