using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Randomizer {
    public class RandoConfigFile {
        public List<RandoConfigFileRoom> ASide { get; set; }
        public List<RandoConfigFileRoom> BSide { get; set; }
        public List<RandoConfigFileRoom> CSide { get; set; }

        public static RandoConfigFile Load(AreaData area) {
            String fullpath = "Config/" + area.GetSID() + ".rando";
            if (!Everest.Content.TryGet(fullpath, out ModAsset asset)) {
                return null;
            } else {
                return asset.Deserialize<RandoConfigFile>();
            }
        }

        public static void YamlSkeleton(MapData map) {
            foreach (LevelData lvl in map.Levels) {
                List<Hole> holes = RandoLogic.FindHoles(lvl);
                if (holes.Count > 0) {
                    Logger.Log("randomizer", $"  - Room: \"{lvl.Name}\"");
                    Logger.Log("randomizer", "    Holes:");
                }
                ScreenDirection lastDirection = ScreenDirection.Up;
                int holeIdx = -1;
                foreach (Hole hole in holes) {
                    if (hole.Side == lastDirection) {
                        holeIdx++;
                    } else {
                        holeIdx = 0;
                        lastDirection = hole.Side;
                    }

                    LevelData targetlvl = map.GetAt(hole.LowCoord) ?? map.GetAt(hole.HighCoord);
                    if (targetlvl != null) {
                        Logger.Log("randomizer", $"    - Side: {hole.Side}");
                        Logger.Log("randomizer", $"      Idx: {holeIdx}");
                        Logger.Log("randomizer", "      Kind: inout");
                    }
                }
            }
        }

        public static void YamlSkeleton(AreaData area) {
            if (area.Mode[0] != null) {
                Logger.Log("randomizer", "ASide:");
                YamlSkeleton(area.Mode[0].MapData);
            }
            if (area.Mode[1] != null) {
                Logger.Log("randomizer", "BSide:");
                YamlSkeleton(area.Mode[1].MapData);
            }
            if (area.Mode[2] != null) {
                Logger.Log("randomizer", "CSide:");
                YamlSkeleton(area.Mode[2].MapData);
            }
        }

        public Dictionary<String, RandoConfigFileRoom> GetRoomMapping(AreaMode mode) {
            List<RandoConfigFileRoom> rooms = null;
            switch (mode) {
                case AreaMode.Normal:
                default:
                    rooms = this.ASide;
                    break;
                case AreaMode.BSide:
                    rooms = this.BSide;
                    break;
                case AreaMode.CSide:
                    rooms = this.CSide;
                    break;
            }

            if (rooms == null) {
                return null;
            }

            var result = new Dictionary<String, RandoConfigFileRoom>();
            foreach (RandoConfigFileRoom room in rooms) {
                result.Add(room.Room, room);
            }

            return result;
        }
    }

    public class RandoConfigFileRoom {
        public String Room { get; set; }
        public List<RandoConfigFileHole> Holes { get; set; }
        public List<RandoConfigFileRoom> Subrooms { get; set; }
    }

    public class RandoConfigFileHole {
        public String Side { get; set; }
        public int Idx { get; set; }
        public String Kind { get; set; }
    }
}
