using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
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

    public class RandoSettings {
        public int Seed;
        public bool RepeatRooms;
        public bool EnterUnknown;
        public LogicType Algorithm;
        public MapLength Length;
        private HashSet<AreaKeyNotStupid> IncludedMaps = new HashSet<AreaKeyNotStupid>();

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
