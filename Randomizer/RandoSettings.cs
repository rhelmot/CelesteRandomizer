using System;
using System.Collections.Generic;

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
        Last
    }

    public enum LogicType {
        Pathway,
        Labyrinth,
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

    public class RandoSettings {
        public SeedType SeedType;
        public string Seed = "achene";
        public Ruleset Rules;
        public bool RepeatRooms;
        public bool EnterUnknown;
        public bool SpawnGolden;
        public LogicType Algorithm;
        public MapLength Length;
        public NumDashes Dashes = NumDashes.One;
        public Difficulty Difficulty;
        private HashSet<AreaKeyNotStupid> IncludedMaps = new HashSet<AreaKeyNotStupid>();

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
                    this.Algorithm = LogicType.Pathway;
                    this.Length = MapLength.Short;
                    this.Dashes = NumDashes.One;
                    this.Difficulty = Difficulty.Normal;
                    break;
                case Ruleset.B:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Algorithm = LogicType.Pathway;
                    this.Length = MapLength.Medium;
                    this.Dashes = NumDashes.Two;
                    this.Difficulty = Difficulty.Normal;
                    break;
                case Ruleset.C:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Algorithm = LogicType.Pathway;
                    this.Length = MapLength.Medium;
                    this.Dashes = NumDashes.One;
                    this.Difficulty = Difficulty.Expert;
                    break;
                case Ruleset.D:
                    this.SetNormalMaps();
                    this.RepeatRooms = false;
                    this.EnterUnknown = false;
                    this.Algorithm = LogicType.Pathway;
                    this.Length = MapLength.Long;
                    this.Dashes = NumDashes.Two;
                    this.Difficulty = Difficulty.Expert;
                    break;
            }
        }

        public void SetNormalMaps() {
            foreach (var key in RandoLogic.AvailableAreas) {
                if (key.GetLevelSet() == "Celeste") {
                    this.EnableMap(key);
                }
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

            var sortedMaps = new List<AreaKeyNotStupid>(IncludedMaps);
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

        public uint IntSeed {
            get {
                // djb2 impl
                uint h = 5381;
                foreach (var i in this.Seed) {
                    h = ((h << 5) + h) + i;
                }
                return h;
            }
        }

        public int LevelCount {
            get {
                int sum = 0;
                foreach (var key in this.IncludedMaps) {
                    var map = AreaData.GetMode(key.Stupid);
                    sum += map.MapData.LevelCount;
                }
                return sum;
            }
        }

        private struct AreaKeyNotStupid {
            public int ID;
            public AreaMode Mode;

            public AreaKeyNotStupid(int ID, AreaMode Mode) {
                this.ID = ID;
                this.Mode = Mode;
            }

            public AreaKeyNotStupid(AreaKey Stupid) {
                this.ID = Stupid.ID; 
                this.Mode = Stupid.Mode;
            }

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
