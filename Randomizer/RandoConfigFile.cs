using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Randomizer {
    public class RandoConfigFile {
        public List<RandoConfigRoom> ASide { get; set; }
        public List<RandoConfigRoom> BSide { get; set; }
        public List<RandoConfigRoom> CSide { get; set; }

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

                    LevelData targetlvl = map.GetAt(hole.LowCoord(lvl.Bounds)) ?? map.GetAt(hole.HighCoord(lvl.Bounds));
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
            if (area.Mode.Length > 1 && area.Mode[1] != null) {
                Logger.Log("randomizer", "BSide:");
                YamlSkeleton(area.Mode[1].MapData);
            }
            if (area.Mode.Length > 2 && area.Mode[2] != null) {
                Logger.Log("randomizer", "CSide:");
                YamlSkeleton(area.Mode[2].MapData);
            }
        }

        public Dictionary<String, RandoConfigRoom> GetRoomMapping(AreaMode mode) {
            List<RandoConfigRoom> rooms = null;
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

            var result = new Dictionary<String, RandoConfigRoom>();
            foreach (RandoConfigRoom room in rooms) {
                result.Add(room.Room, room);
            }

            return result;
        }
    }

    public class RandoConfigRoom {
        public String Room { get; set; }
        public List<RandoConfigHole> Holes { get; set; }
        public List<RandoConfigRoom> Subrooms { get; set; }
        public List<RandoConfigInternalEdge> InternalEdges { get; set; }
        public bool End { get; set; }
        public List<RandoConfigEdit> Tweaks { get; set; }
    }

    public class RandoConfigHole {
        public ScreenDirection Side { get; set; }
        public int Idx { get; set; }
        public int? LowBound { get; set; }
        public int? HighBound { get; set; }
        public bool? HighOpen { get; set; }

        public RandoConfigReq ReqIn { get; set; }
        public RandoConfigReq ReqOut { get; set; }
        public RandoConfigReq ReqBoth {
            get {
                return null;
            }

            set {
                this.ReqIn = value;
                this.ReqOut = value;
            }
        }
        public HoleKind Kind { get; set; }
    }

    public class RandoConfigInternalEdge {
        public String To { get; set; }
        public RandoConfigReq ReqIn { get; set; }
        public RandoConfigReq ReqOut { get; set; }
        public RandoConfigReq ReqBoth {
            get {
                return null;
            }

            set {
                this.ReqIn = value;
                this.ReqOut = value;
            }
        }

        public enum SplitKind {
            TopToBottom,
            BottomToTop,
            LeftToRight,
            RightToLeft,
        }

        public SplitKind? Split;
    }

    public class RandoConfigReq {
        public List<RandoConfigReq> And { get; set; }
        public List<RandoConfigReq> Or { get; set; }

        public Difficulty Difficulty { get; set; }
        public NumDashes? Dashes { get; set; }
        public bool Key;
    }

    public class RandoConfigEdit {
        public String Name { get; set; }
        public int? ID { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public RandoConfigUpdate Update { get; set; }
    }

    public class RandoConfigUpdate {
        public bool Remove { get; set; }
        public bool Add { get; set; }

        public float? X { get; set; }
        public float? Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
