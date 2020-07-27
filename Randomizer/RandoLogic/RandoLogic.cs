using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        public static AreaKey GenerateMap(RandoSettings settings) {
            var lastarea = AreaData.Areas[AreaData.Areas.Count - 1];
            var newID = AreaData.Areas.Count;
            if (lastarea.GetLevelSet() == "randomizer") {
                newID--;
            }

            var newArea = new AreaData {
                IntroType = Player.IntroTypes.WakeUp,
                Interlude = false,
                Dreaming = false,
                ID = newID,
                Name = $"{settings.Seed}_{settings.Hash}",
                Mode = new ModeProperties[3] {
                    new ModeProperties {
                        Inventory = settings.Dashes == NumDashes.Zero ? PlayerInventory.Prologue : 
                                    settings.Dashes == NumDashes.One ? PlayerInventory.Default : 
                                                                        PlayerInventory.TheSummit,
                    }, null, null
                },
                Icon = AreaData.Areas[0].Icon,
                MountainIdle = AreaData.Areas[0].MountainIdle,
                MountainZoom = AreaData.Areas[0].MountainZoom,
                MountainState = AreaData.Areas[0].MountainState,
                MountainCursor = AreaData.Areas[0].MountainCursor,
                MountainSelect = AreaData.Areas[0].MountainSelect,
                MountainCursorScale = AreaData.Areas[0].MountainCursorScale,
            };
            newArea.SetMeta(new Meta.MapMeta {
                Modes = new Meta.MapMetaModeProperties[] {
                    new Meta.MapMetaModeProperties {
                        HeartIsEnd = true,
                    },
                    null, null
                }
            });
            newArea.SetSID($"randomizer/{newArea.Name}");
            if (lastarea.GetLevelSet() == "randomizer") {
                AreaData.Areas[AreaData.Areas.Count - 1] = newArea;
            } else {
                AreaData.Areas.Add(newArea);
            }

            var key = new AreaKey(newArea.ID);

            var r = new RandoLogic(settings, key);

            newArea.Wipe = r.PickWipe();
            newArea.CassetteSong = r.PickCassetteAudio();
            newArea.Mode[0].AudioState = new AudioState(r.PickMusicAudio(), r.PickAmbienceAudio());
            newArea.Mode[0].MapData = r.MakeMap();

            Logger.Log("randomizer", $"new area {newArea.GetSID()}");

            return key;
        }

        private Random Random;
        private List<StaticRoom> RemainingRooms;
        private AreaKey Key;
        private LinkedMap Map;
        private RandoSettings Settings;
        private Capabilities Caps;
        private Deque<RandoTask> Tasks = new Deque<RandoTask>();
        private Stack<RandoTask> CompletedTasks = new Stack<RandoTask>();

        private RandoLogic(RandoSettings settings, AreaKey key) {
            this.Random = new Random(settings.Seed);
            this.Settings = settings;
            this.RemainingRooms = new List<StaticRoom>();
            foreach (var room in RandoLogic.AllRooms) {
                if (settings.MapIncluded(room.Area)) {
                    this.RemainingRooms.Add(room);
                }
            }
            this.Key = key;

            this.Caps = new Capabilities {
                Dashes = settings.Dashes,
                PlayerSkill = settings.Difficulty,
                HasKey = true,
            };
        }

        private Action<Scene, bool, Action> PickWipe() {
            switch (this.Random.Next(10)) {
                case 0:
                default:
                    return (scene, wipeIn, onComplete) => new CurtainWipe(scene, wipeIn, onComplete);
                case 1:
                    return (scene, wipeIn, onComplete) => new AngledWipe(scene, wipeIn, onComplete);
                case 2:
                    return (scene, wipeIn, onComplete) => new DropWipe(scene, wipeIn, onComplete);
                case 3:
                    return (scene, wipeIn, onComplete) => new DreamWipe(scene, wipeIn, onComplete);
                case 4:
                    return (scene, wipeIn, onComplete) => new WindWipe(scene, wipeIn, onComplete);
                case 5:
                    return (scene, wipeIn, onComplete) => new FallWipe(scene, wipeIn, onComplete);
                case 6:
                    return (scene, wipeIn, onComplete) => new HeartWipe(scene, wipeIn, onComplete);
                case 7:
                    return (scene, wipeIn, onComplete) => new KeyDoorWipe(scene, wipeIn, onComplete);
                case 8:
                    return (scene, wipeIn, onComplete) => new MountainWipe(scene, wipeIn, onComplete);
                case 9:
                    return (scene, wipeIn, onComplete) => new StarfieldWipe(scene, wipeIn, onComplete);
            }
        }

        private string PickMusicAudio() {
            switch (this.Random.Next(41)) {
                case 0:
                case 1:
                case 2: // ;)
                default:
                    return "event:/music/lvl1/main";
                case 3:
                    return "event:/music/lvl0/intro";
                case 4:
                    return "event:/music/lvl2/beginning";
                case 5:
                    return "event:/music/remix/02_old_site";
                case 6:
                case 7:
                    return "event:/music/lvl2/mirror";
                case 8:
                    return "event:/music/lvl2/chase";
                case 9:
                    return "event:/music/lvl2/evil_madeline";
                case 10:
                case 11:
                    return "event:/music/lvl3/intro";
                case 12:
                    return "event:/music/lvl3/explore";
                case 13:
                case 14:
                    return "event:/music/lvl3/clean";
                case 15:
                    return "event:/music/lvl3/oshiro_chase";
                case 16:
                case 17:
                    return "event:/music/lvl4/main";
                case 18:
                    return "event:/music/lvl4/heavy_winds";
                case 19:
                case 20:
                    return "event:/music/lvl5/normal";
                case 21:
                    return "event:/music/lvl5/middle_temple";
                case 22:
                    return "event:/music/lvl5/mirror";
                case 23:
                case 24:
                    return "event:/music/lvl6/main";
                case 25:
                    return "event:/music/lvl6/starjump";
                case 26:
                case 27:
                    return "event:/music/lvl6/badeline_fight";
                case 28:
                    return "event:/music/lvl6/badeline_glitch";
                case 29:
                    return "event:/music/lvl6/madeline_and_theo";
                case 30:
                    return "event:/music/lvl7/main";
                case 31:
                    return "event:/music/lvl7/final_ascent";
                case 32:
                    return "event:/music/lvl8/main";
                case 33:
                    return "event:/music/remix/01_forsaken_city";
                case 34:
                    return "event:/music/remix/02_old_site";
                case 35:
                    return "event:/music/remix/03_resort";
                case 36:
                    return "event:/music/remix/04_cliffside";
                case 37:
                    return "event:/music/remix/05_mirror_temple";
                case 38:
                    return "event:/music/remix/06_reflection";
                case 39:
                    return "event:/music/remix/07_summit";
                case 40:
                    return "event:/music/remix/09_core";
        }
    }

        private string PickAmbienceAudio() {
            switch (this.Random.Next(16)) {
                default:
                case 0:
                    return "event:/env/amb/00_prologue";
                case 1:
                    return "event:/env/amb/01_main";
                case 2:
                    return "event:/env/amb/02_awake";
                case 3:
                    return "event:/env/amb/02_dream";
                case 4:
                    return "event:/env/amb/03_exterior";
                case 5:
                    return "event:/env/amb/03_interior";
                case 6:
                    return "event:/env/amb/03_pico8_closeup";
                case 7:
                    return "event:/env/amb/04_main";
                case 8:
                    return "event:/env/amb/05_interior_dark";
                case 9:
                    return "event:/env/amb/05_interior_main";
                case 10:
                    return "event:/env/amb/05_mirror_sequence";
                case 11:
                    return "event:/env/amb/06_lake";
                case 12:
                    return "event:/env/amb/06_main";
                case 13:
                    return "event:/env/amb/06_prehug";
                case 14:
                    return "event:/env/amb/09_main";
                case 15:
                    return "event:/env/amb/worldmap";

        }
    }

        private string PickCassetteAudio() {
            switch (this.Random.Next(9)) {
                case 0:
                    return "event:/music/cassette/01_forsaken_city";
                case 1:
                    return "event:/music/cassette/02_old_site";
                case 2:
                    return "event:/music/cassette/03_resort";
                case 3:
                    return "event:/music/cassette/04_cliffside";
                case 4:
                    return "event:/music/cassette/05_mirror_temple";
                case 5:
                    return "event:/music/cassette/06_reflection";
                case 6:
                    return "event:/music/cassette/07_summit";
                case 7:
                    return "event:/music/cassette/09_core";
                case 8:
                default:
                    return "event:/new_content/music/lvl10/cassette_rooms";
            }
        }

        private MapData MakeMap() {
            this.Map = new LinkedMap();

            switch (this.Settings.Algorithm) {
                case LogicType.Labyrinth:
                default:
                    this.Tasks.AddToFront(new TaskLabyrinthStart(this));
                    break;
                case LogicType.Pathway:
                    this.Tasks.AddToFront(new TaskPathwayStart(this));
                    break;
            }

            while (this.Tasks.Count != 0) {
                var nextTask = this.Tasks.RemoveFromFront();

                while (!nextTask.Next()) {
                    if (this.CompletedTasks.Count == 0) {
                        throw new Exception("Could not generate map");
                    }

                    this.Tasks.AddToFront(nextTask);
                    nextTask = this.CompletedTasks.Pop();
                    nextTask.Undo();
                }

                this.CompletedTasks.Push(nextTask);
            }

            var map = new MapData(this.Key);
            map.Levels = new List<LevelData>();

            this.Map.FillMap(map);
            this.SetMapBounds(map);
            this.SetForeground(map);
            this.SetBackground(map);
            return map;
        }

        private void SetMapBounds(MapData map) {
            int num1 = int.MaxValue;
            int num2 = int.MaxValue;
            int num3 = int.MinValue;
            int num4 = int.MinValue;
            foreach (LevelData level in map.Levels) {
                if (level.Bounds.Left < num1)
                    num1 = level.Bounds.Left;
                if (level.Bounds.Top < num2)
                    num2 = level.Bounds.Top;
                if (level.Bounds.Right > num3)
                    num3 = level.Bounds.Right;
                if (level.Bounds.Bottom > num4)
                    num4 = level.Bounds.Bottom;
            }

            map.Bounds = new Rectangle(num1, num2, num3 - num1, num4 - num2);
        }

        private void SetForeground(MapData map) {
            string fgName = null;
            bool needsWind = true;
            switch (this.Random.Next(15)) {
                case 0:
                    fgName = "stardust";
                    needsWind = false;
                    break;
                case 1:
                    fgName = "windsnow";
                    needsWind = false;
                    break;
                case 2:
                    fgName = "rain";
                    break;
                case 3:
                case 4:
                    fgName = "snowFg";
                    break;
                case 5:
                    fgName = "mirrorFg";
                    break;
                case 6:
                    fgName = "reflectionFg";
                    break;
                case 7:
                    fgName = "petals";
                    break;
                case 8:
                    fgName = "godrays";
                    break;
            }

            map.Foreground = new BinaryPacker.Element{ Children = new List<BinaryPacker.Element>() };
            if (fgName != null) {
                map.Foreground.Children.Add(new BinaryPacker.Element { Name = fgName });
            }
            if (needsWind) {
                fgName = this.Random.Next(2) == 0 ? "stardust" : "windsnow";
                map.Foreground.Children.Add(new BinaryPacker.Element {
                    Name = fgName,
                    Attributes = new Dictionary<string, object> {
                        {"only", string.Join(",", this.FindWindyLevels(map))}
                    }
                });
            }
        }

        private void SetBackground(MapData map) {
            string bgEffect = null;
            //string bgParallax = null;
            switch (this.Random.Next(6)) {
                case 0:
                    bgEffect = "snowBg";
                    break;
                case 1:
                    bgEffect = "dreamstars";
                    break;
                case 2:
                    bgEffect = "stars";
                    break;
                case 3:
                    bgEffect = "bossstarfield";
                    break;
                case 4:
                    bgEffect = "northernlights";
                    break;
                case 5:
                    bgEffect = "planets";
                    break;
            }
            map.Background = new BinaryPacker.Element { Children = new List<BinaryPacker.Element>() };
            if (bgEffect != null) {
                map.Background.Children.Add(new BinaryPacker.Element { Name = bgEffect });
            }
        }

        private IEnumerable<string> FindWindyLevels(MapData map) {
            foreach (var lvl in map.Levels) {
                if (lvl.WindPattern != WindController.Patterns.None) {
                    yield return lvl.Name;
                } else {
                    foreach (var trigger in lvl.Triggers) {
                        if (trigger.Name == "windTrigger") {
                            yield return lvl.Name;
                            break;
                        }
                    }
                }
            }
        }

        private List<StaticEdge> AvailableNewEdges(Capabilities capsIn, Capabilities capsOut, Func<StaticEdge, bool> filter=null) {
            var result = new List<StaticEdge>();

            foreach (var room in this.RemainingRooms) {
                foreach (var node in room.Nodes.Values) {
                    foreach (var edge in node.Edges) {
                        if (edge.HoleTarget == null) {
                            continue;
                        }
                        if (edge.HoleTarget.Kind == HoleKind.Unknown && !this.Settings.EnterUnknown) {
                            continue;
                        }
                        if (capsIn != null && !edge.ReqIn.Able(capsIn)) {
                            continue;
                        }
                        if (capsOut != null && !edge.ReqOut.Able(capsOut)) {
                            continue;
                        }
                        if (filter != null && !filter(edge)) {
                            continue;
                        }
                        result.Add(edge);
                    }
                }
            }

            result.Shuffle(this.Random);
            return result;
        }
    }
}
