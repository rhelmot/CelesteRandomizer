using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Celeste.Mod.Randomizer
{
    public partial class RandoLogic
    {
        public static List<StaticRoom> AllRooms = new List<StaticRoom>();
        public static Dictionary<string, List<AreaKey>> LevelSets = new Dictionary<string, List<AreaKey>>();
        public static Dictionary<RandoSettings.AreaKeyNotStupid, int> LevelCount = new Dictionary<RandoSettings.AreaKeyNotStupid, int>();
        public static HashSet<RandoSettings.AreaKeyNotStupid> LazyLoaded = new HashSet<RandoSettings.AreaKeyNotStupid>();

        public static List<Hole> FindHoles(LevelData data)
        {
            List<Hole> result = new List<Hole>();
            Regex regex = new Regex("\\r\\n|\\n\\r|\\n|\\r");
            string[] lines = regex.Split(data.Solids);

            // returns whether the given tile is clear
            bool lookup(int x, int y)
            {
                if (y >= lines.Length)
                {
                    return true;
                }
                string line = lines[y];
                if (x >= line.Length)
                {
                    return true;
                }
                return line[x] == '0';
            }

            int curHole = -1;
            bool clear;

            //Logger.Log("findholes", $"{data.TileBounds.Width} x {data.TileBounds.Height} => {data.Solids.Length}");
            //Logger.Log("findholes", $"\n{data.Solids}");

            for (int i = 0; i < data.TileBounds.Width; i++)
            {
                clear = lookup(i, 0);
                if (clear && curHole == -1)
                {
                    curHole = i;
                }
                else if (!clear && curHole != -1)
                {
                    result.Add(new Hole(ScreenDirection.Up, curHole, i - 1, false));
                    curHole = -1;
                }
            }
            if (curHole != -1)
            {
                result.Add(new Hole(ScreenDirection.Up, curHole, data.TileBounds.Width - 1, true));
            }

            curHole = -1;
            for (int i = 0; i < data.TileBounds.Height; i++)
            {
                clear = lookup(data.TileBounds.Width - 1, i);
                if (clear && curHole == -1)
                {
                    curHole = i;
                }
                else if (!clear && curHole != -1)
                {
                    result.Add(new Hole(ScreenDirection.Right, curHole, i - 1, false));
                    curHole = -1;
                }
            }
            if (curHole != -1)
            {
                result.Add(new Hole(ScreenDirection.Right, curHole, data.TileBounds.Height - 1, true));
            }

            curHole = -1;

            for (int i = 0; i < data.TileBounds.Height; i++)
            {
                clear = lookup(0, i);
                if (clear && curHole == -1)
                {
                    curHole = i;
                }
                else if (!clear && curHole != -1)
                {
                    result.Add(new Hole(ScreenDirection.Left, curHole, i - 1, false));
                    curHole = -1;
                }
            }
            if (curHole != -1)
            {
                result.Add(new Hole(ScreenDirection.Left, curHole, data.TileBounds.Height - 1, true));
            }

            curHole = -1;

            for (int i = 0; i < data.TileBounds.Width; i++)
            {
                clear = lookup(i, data.TileBounds.Height - 1);
                if (clear && curHole == -1)
                {
                    curHole = i;
                }
                else if (!clear && curHole != -1)
                {
                    result.Add(new Hole(ScreenDirection.Down, curHole, i - 1, false));
                    curHole = -1;
                }
            }
            if (curHole != -1)
            {
                result.Add(new Hole(ScreenDirection.Down, curHole, data.TileBounds.Width - 1, true));
            }

            return result;
        }

        private static List<StaticRoom> ProcessMap(MapData map, Dictionary<String, RandoConfigRoom> config)
        {
            var result = new List<StaticRoom>();
            var resultMap = new Dictionary<string, StaticRoom>();

            foreach (LevelData level in map.Levels)
            {
                if (level.Dummy)
                {
                    continue;
                }
                if (!config.TryGetValue(level.Name, out RandoConfigRoom roomConfig))
                {
                    continue;
                }
                if (roomConfig == null)
                {
                    continue;
                }
                var holes = RandoLogic.FindHoles(level);
                var room = new StaticRoom(map.Area, roomConfig, level, holes);
                result.Add(room);
                resultMap[level.Name] = room;
            }

            foreach (var room in result)
            {
                room.ProcessWarps(resultMap);
            }

            return result;
        }

        private static void ProcessArea(AreaData area)
        {
            RandoConfigFile config = RandoConfigFile.LoadAll(area, RandoModule.Instance.SavedData.LazyLoading);
            if (config == null)
            {
                return;
            }

            for (int i = 0; i < area.Mode.Length; i++)
            {
                if (area.Mode[i] == null)
                {
                    continue;
                }
                var mapConfig = config.GetRoomMapping((AreaMode)i);
                if (mapConfig == null)
                {
                    continue;
                }

                // Mark map as available
                // Use SID (minus level name) for levelsets to avoid collisions
                AreaKey key = new AreaKey(area.ID, (AreaMode)i);
                string SID = key.GetSID();
                string levelSetID = SID.Substring(0, SID.LastIndexOf('/'));
                if (RandoLogic.LevelSets.TryGetValue(levelSetID, out var keyList))
                {
                    keyList.Add(key);
                }
                else
                {
                    RandoLogic.LevelSets.Add(levelSetID, new List<AreaKey> { key });
                }

                if (mapConfig.Count == 0)
                {
                    LazyLoaded.Add(new RandoSettings.AreaKeyNotStupid(key));
                }
                else
                {
                    RandoLogic.AllRooms.AddRange(RandoLogic.ProcessMap(area.Mode[i].MapData, mapConfig));
                }

            }
        }

        private static void LazyReload(IEnumerable<AreaKey> keys)
        {
            foreach (var key in keys)
            {
                var notstupid = new RandoSettings.AreaKeyNotStupid(key);
                if (LazyLoaded.Contains(notstupid))
                {
                    LazyLoaded.Remove(notstupid);
                    var mapping = RandoConfigFile.LazyReload(key);
                    RandoLogic.AllRooms.AddRange(RandoLogic.ProcessMap(AreaData.Get(key).Mode[(int)key.Mode].MapData, mapping));
                }
            }
            CountRooms();
        }

        private static void CountRooms()
        {
            LevelCount = new Dictionary<RandoSettings.AreaKeyNotStupid, int>();
            foreach (var room in RandoLogic.AllRooms)
            {
                var notstupid = new RandoSettings.AreaKeyNotStupid(room.Area);
                if (RandoLogic.LevelCount.TryGetValue(notstupid, out int c))
                {
                    RandoLogic.LevelCount[notstupid] = c + 1;
                }
                else
                {
                    RandoLogic.LevelCount[notstupid] = 1;
                }
            }

            foreach (var notstupid in LazyLoaded)
            {
                var stupid = notstupid.Stupid;
                var count = AreaData.Get(stupid).Mode[(int)stupid.Mode].MapData.Levels.Count;
                if (RandoLogic.LevelCount.TryGetValue(notstupid, out int c))
                {
                    RandoLogic.LevelCount[notstupid] = c + count;
                }
                else
                {
                    RandoLogic.LevelCount[notstupid] = count;
                }
            }
        }

        public static void ProcessAreas()
        {
            if (RandoLogic.AllRooms.Count != 0)
            {
                return;
            }
            Logger.Log("randomizer", "Processing level data...");

            foreach (var area in AreaData.Areas)
            {
                RandoLogic.ProcessArea(area);
            }

            RandoLogic.CountRooms();
        }
    }
}
