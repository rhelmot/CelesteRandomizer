using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        public static List<RandoRoom> AllRooms;

        public static List<Hole> FindHoles(LevelData data) {
            List<Hole> result = new List<Hole>();
            Regex regex = new Regex("\\r\\n|\\n\\r|\\n|\\r");
            string[] lines = regex.Split(data.Solids);

            // returns whether the given tile is clear
            bool lookup(int x, int y) {
                if (y >= lines.Length) {
                    return true;
                }
                string line = lines[y];
                if (x >= line.Length) {
                    return true;
                }
                return line[x] == '0';
            }

            int curHole = -1;
            bool clear;

            //Logger.Log("findholes", $"{data.TileBounds.Width} x {data.TileBounds.Height} => {data.Solids.Length}");
            //Logger.Log("findholes", $"\n{data.Solids}");

            for (int i = 0; i < data.TileBounds.Width; i++) {
                clear = lookup(i, 0);
                if (clear && curHole == -1) {
                    curHole = i;
                } else if (!clear && curHole != -1) {
                    result.Add(new Hole(data, ScreenDirection.Up, curHole, i - 1, false));
                    curHole = -1;
                }
            }
            if (curHole != -1) {
                result.Add(new Hole(data, ScreenDirection.Up, curHole, data.TileBounds.Width - 1, true));
            }

            curHole = -1;
            for (int i = 0; i < data.TileBounds.Height; i++) {
                clear = lookup(data.TileBounds.Width - 1, i);
                if (clear && curHole == -1) {
                    curHole = i;
                } else if (!clear && curHole != -1) {
                    result.Add(new Hole(data, ScreenDirection.Right, curHole, i - 1, false));
                    curHole = -1;
                }
            }
            if (curHole != -1) {
                result.Add(new Hole(data, ScreenDirection.Right, curHole, data.TileBounds.Height - 1, true));
            }

            curHole = -1;

            for (int i = 0; i < data.TileBounds.Height; i++) {
                clear = lookup(0, i);
                if (clear && curHole == -1) {
                    curHole = i;
                } else if (!clear && curHole != -1) {
                    result.Add(new Hole(data, ScreenDirection.Left, curHole, i - 1, false));
                    curHole = -1;
                }
            }
            if (curHole != -1) {
                result.Add(new Hole(data, ScreenDirection.Left, curHole, data.TileBounds.Height - 1, true));
            }

            curHole = -1;

            for (int i = 0; i < data.TileBounds.Width; i++) {
                clear = lookup(i, data.TileBounds.Height - 1);
                if (clear && curHole == -1) {
                    curHole = i;
                } else if (!clear && curHole != -1) {
                    result.Add(new Hole(data, ScreenDirection.Down, curHole, i - 1, false));
                    curHole = -1;
                }
            }
            if (curHole != -1) {
                result.Add(new Hole(data, ScreenDirection.Down, curHole, data.TileBounds.Width - 1, true));
            }

            return result;
        }

        private static List<RandoRoom> ProcessMap(MapData map, Dictionary<String, RandoConfigFileRoom> config) {
            var result = new List<RandoRoom>();
            String prefix = AreaData.Get(map.Area).GetSID() + "/" + (map.Area.Mode == AreaMode.Normal ? "A" : map.Area.Mode == AreaMode.BSide ? "B" : "C") + "/";

            foreach (LevelData level in map.Levels) {
                if (!config.TryGetValue(level.Name, out RandoConfigFileRoom roomConfig)) {
                    continue;
                }
                if (roomConfig == null || roomConfig.Holes == null) {
                    continue;
                }
                var holes = RandoLogic.FindHoles(level);
                var room = new RandoRoom(prefix, level, holes);
                room.End = roomConfig.End;
                result.Add(room);

                foreach (RandoConfigFileHole holeConfig in roomConfig.Holes) {
                    Hole matchedHole = null;
                    int remainingMatches = holeConfig.Idx;
                    foreach (Hole hole in holes) {
                        if (hole.Side == ScreenDirectionMethods.FromString(holeConfig.Side)) {
                            if (remainingMatches == 0) {
                                matchedHole = hole;
                                break;
                            } else {
                                remainingMatches--;
                            }
                        }
                    }

                    if (matchedHole == null) {
                        throw new Exception($"Could not find the hole identified by area:{map.Area} room:{roomConfig.Room} side:{holeConfig.Side} idx:{holeConfig.Idx}");
                    } else {
                        //Logger.Log("randomizer", $"Matching {roomConfig.Room} {holeConfig.Side} {holeConfig.Idx} to {matchedHole}");
                        matchedHole.Kind = HoleKindMethods.FromString(holeConfig.Kind);
                        if (holeConfig.LowBound != null) {
                            matchedHole.LowBound = (int)holeConfig.LowBound;
                        }
                        if (holeConfig.HighBound != null) {
                            matchedHole.HighBound = (int)holeConfig.HighBound;
                        }
                        if (holeConfig.HighOpen != null) {
                            matchedHole.HighOpen = (int)holeConfig.HighOpen;
                        }
                    }
                }
            }

            return result;
        }

        private static List<RandoRoom> ProcessArea(AreaData area, AreaMode? mode = null) {
            RandoConfigFile config = RandoConfigFile.Load(area);
            var result = new List<RandoRoom>();
            if (config == null) {
                return result;
            }

            for (int i = 0; i < area.Mode.Length; i++) {
                if (area.Mode[i] == null) {
                    continue;
                }
                if (mode != null && mode != (AreaMode?)i) {
                    continue;
                }
                var mapConfig = config.GetRoomMapping((AreaMode)i);
                if (mapConfig == null) {
                    continue;
                }

                result.AddRange(RandoLogic.ProcessMap(area.Mode[i].MapData, mapConfig));
            }

            return result;
        }

        public static void ProcessAreas() {
            if (RandoLogic.AllRooms != null) {
                return;
            }
            Logger.Log("randomizer", "Processing level data...");

            RandoLogic.AllRooms = new List<RandoRoom>();
            /*RandoLogic.AllRooms.AddRange(RandoLogic.ProcessArea(AreaData.Areas[1], AreaMode.BSide));
            return;/**/

            foreach (var area in AreaData.Areas) {
                RandoLogic.AllRooms.AddRange(RandoLogic.ProcessArea(area));
            }
        }


    }
}
