﻿using System;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer {
    public class GenerationError : Exception {
        public GenerationError(string message) : base(message) {}
    }

    public class RetryException : Exception {
        public RetryException() : base("Too many backtracks") {}
    }

    public partial class RandoLogic {
        public static AreaKey GenerateMap(RandoSettings settings) {
            var lastarea = AreaData.Areas[AreaData.Areas.Count - 1];
            var newID = AreaData.Areas.Count;
            bool secondVerseSameAsTheFirst = lastarea.GetSID().StartsWith("randomizer/");
            if (secondVerseSameAsTheFirst) {
                newID--;
            }

            var newArea = new AreaData {
                IntroType = Player.IntroTypes.WakeUp,
                Interlude = false,
                Dreaming = false,
                ID = newID,
                Name = $"{settings.Seed}_{settings.EndlessLevel}_{settings.Hash}",
                Mode = new ModeProperties[3] {
                    new ModeProperties {
                        Inventory = settings.Dashes == NumDashes.Zero ? new PlayerInventory(0, true, false, false) :
                                    settings.Dashes == NumDashes.One ?  new PlayerInventory(1, true, false, false) : 
                                                                        new PlayerInventory(2, true, false, false),
                    }, null, null
                },
                Icon = AreaData.Areas[0].Icon,
                MountainIdle = AreaData.Areas[0].MountainIdle,
                MountainZoom = AreaData.Areas[0].MountainZoom,
                MountainState = AreaData.Areas[0].MountainState,
                MountainCursor = new Vector3(0, 100000, 0),  // ???
                MountainSelect = AreaData.Areas[0].MountainSelect,
                MountainCursorScale = AreaData.Areas[0].MountainCursorScale,
            };
            newArea.SetMeta(new Meta.MapMeta {
                Modes = new Meta.MapMetaModeProperties[] {
                    new Meta.MapMetaModeProperties {
                        HeartIsEnd = true,
                        //SeekerSlowdown = true,  // this doesn't do anything
                    },
                    null, null
                }
            });
            newArea.OnLevelBegin = (level) => {
                level.Add(new SeekerEffectsController());
            };
            var dyn = new DynData<AreaData>(newArea);
            dyn.Set<RandoSettings>("RandoSettings", settings.Copy());

            newArea.SetSID($"randomizer/{newArea.Name}");
            if (secondVerseSameAsTheFirst) {
                AreaData.Areas[AreaData.Areas.Count - 1] = newArea;
                // invalidate the MapEditor area key cache, as it will erroniously see a cache hit
                typeof(Editor.MapEditor).GetField("area", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, AreaKey.None);
            } else {
                // avert race condition
                RandoModule.AreaHandoff = newArea;
                while (RandoModule.AreaHandoff != null) {
                    Thread.Sleep(10);
                }
            }

            var key = new AreaKey(newArea.ID);

            int tryNum = 0;
            while (true) {
                try {
                    var r = new RandoLogic(settings, key, tryNum);

                    newArea.Mode[0].MapData = r.MakeMap();
                    newArea.Wipe = r.PickWipe();
                    newArea.CompleteScreenName = r.PickCompleteScreen();
                    newArea.CassetteSong = r.PickCassetteAudio();
                    newArea.Mode[0].AudioState = new AudioState(r.PickMusicAudio(), r.PickAmbienceAudio());
                    r.RandomizeDialog();
                    break;
                } catch (RetryException) {
                    tryNum++;
                    if (tryNum >= 500) {
                        throw new GenerationError("Cannot create map with these settings");
                    }
                    Logger.Log("randomizer", $"Too many retries ({tryNum}), starting again");
                }
            }

            Logger.Log("randomizer", $"new area {newArea.GetSID()}");

            return key;
        }

        private Random Random;
        private List<StaticRoom> RemainingRooms = new List<StaticRoom>();
        private AreaKey Key;
        private LinkedMap Map;
        private RandoSettings Settings;
        private Capabilities Caps;
        public static Dictionary<string, string> RandomDialogMappings = new Dictionary<string, string>();

        private void ResetRooms() {
            this.RemainingRooms.Clear();
            foreach (var room in RandoLogic.AllRooms) {
                if (this.Settings.MapIncluded(room.Area)) {
                    this.RemainingRooms.Add(room);
                }
            }
        }

        private RandoLogic(RandoSettings settings, AreaKey key, int tryNum) {
            this.Random = new Random((int)settings.IntSeed);
            for (int i = 0; i < settings.EndlessLevel; i++) {
                this.Random = new Random(this.Random.Next());
            }
            for (int i = 0; i < tryNum; i++) {
                this.Random.Next();
                this.Random = new Random(this.Random.Next());
            }
            this.Settings = settings;
            this.Key = key;
            this.ResetRooms();
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
            float totalWeight = RandoModule.Instance.MetaConfig.Music.Select(t => t.Weight).Sum();
            float weighAt = this.Random.NextFloat(totalWeight);
            float soFar = 0f;
            foreach (var track in RandoModule.Instance.MetaConfig.Music) {
                soFar += track.Weight;
                if (weighAt < soFar) {
                    return track.Name;
                }
            }
            // this should be unreachable. but just in case:
            return RandoModule.Instance.MetaConfig.Music.Last().Name;
        }

        private string PickAmbienceAudio() {
            return "event:/env/amb/04_main"; // only way to get wind effects?
            /*
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
            */           
        }

        private string PickCassetteAudio() {
            switch (this.Random.Next(8)) {
                case 0:
                    return SFX.cas_01_forsaken_city;
                case 1:
                    return SFX.cas_02_old_site;
                case 2:
                    return SFX.cas_03_resort;
                case 3:
                    return SFX.cas_04_cliffside;
                case 4:
                    return SFX.cas_05_mirror_temple;
                case 5:
                    return SFX.cas_06_reflection;
                case 6:
                    return SFX.cas_07_summit;
                case 7:
                default:
                    return SFX.cas_08_core;
            }
        }

        private string PickCompleteScreen() {
            uint seed = this.Settings.IntSeed;
            tryagain:
            // ensure different rulesets of the same seed have different end screens
            switch ((seed + (int)this.Settings.Rules) % 8) {
                case 0:
                    return AreaData.Areas[1].CompleteScreenName;
                case 1:
                    return AreaData.Areas[2].CompleteScreenName;
                case 2:
                    return AreaData.Areas[3].CompleteScreenName;
                case 3:
                    return AreaData.Areas[4].CompleteScreenName;
                case 4:
                    return AreaData.Areas[5].CompleteScreenName;
                case 5:
                    return AreaData.Areas[6].CompleteScreenName;
                case 6:
                    return AreaData.Areas[7].CompleteScreenName;
                case 7:
                default:
                    if (this.Settings.Algorithm == LogicType.Endless) {
                        seed = (uint)new Random((int)seed).Next();
                        goto tryagain;
                    }
                    return AreaData.Areas[9].CompleteScreenName;
            }
        }

        private MapData MakeMap() {
            this.Map = new LinkedMap();
            switch (this.Settings.Algorithm) {
                case LogicType.Labyrinth:
                    this.GenerateLabyrinth();
                    break;
                case LogicType.Pathway:
                case LogicType.Endless:
                    this.GeneratePathway();
                    break;
            }

            var map = new MapData(this.Key);
            typeof(MapData).GetField("DashlessGoldenberries").SetValue(map, new List<EntityData>());
            map.Levels = new List<LevelData>();
            this.Map.FillMap(map, this.Settings, this.Random);
            this.SetMapBounds(map);
            this.SetForeground(map);
            this.SetBackground(map);
            this.SetPoem();
            this.SpawnGolden(map);
            this.SetDarkness(map);

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
            if (!this.Settings.RandomDecorations) {
                fgName = null;
                needsWind = true;
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
            // this cutscene hardcodes a reference to a windsnow fg
            // the level should only ever be last on the list, right?
            if (fgName != "windsnow" && map.Levels[map.Levels.Count - 1].Name.StartsWith("Celeste/4-GoldenRidge/A/d-10")) {
                map.Foreground.Children.Add(new BinaryPacker.Element {
                    Name = "windsnow",
                    Attributes = new Dictionary<string, object> {
                       {"only", map.Levels[map.Levels.Count - 1].Name }
                    }
                });
            }
        }

        private Color RandomColor(Func<Color, bool> filter=null) {
            var possibleColors = new List<Color>(typeof(Color).GetProperties(BindingFlags.Static | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(Color))
                .Select(p => (Color)p.GetValue(null))
                .Where(c => c.A == 255)
                .Where(filter ?? (c => true)));
            return possibleColors[this.Random.Next(possibleColors.Count)];
        }

        private void SetBackground(MapData map) {
            map.BackgroundColor = RandomColor();
            
            map.Background = new BinaryPacker.Element { Children = new List<BinaryPacker.Element>() };
            int totalCoverage = 0;
            for (int i = 0; i < 5; i++) {
                string parallax = null;
                int coverage = 0;
                switch (this.Random.Next(15)) {
                    case 0:
                    default:
                        parallax = "bgs/07/bg0";
                        coverage = 180;
                        break;
                    case 1:
                        parallax = "bgs/07/00/bg1";
                        coverage = 180 - 162;
                        break;
                    case 2:
                        parallax = "bgs/07/00/bg2";
                        coverage = 180 - 152;
                        break;
                    case 3:
                        parallax = "bgs/07/01/bg1";
                        coverage = 180 - 130;
                        break;
                    case 4:
                        parallax = "bgs/07/01/bg2";
                        coverage = 180 - 168;
                        break;
                    case 5:
                        parallax = "bgs/07/01/bg3";
                        coverage = 180 - 147;
                        break;
                    case 6:
                        parallax = "bgs/07/02/bg1";
                        coverage = 180 - 129;
                        break;
                    case 7:
                        parallax = "bgs/07/02/bg2";
                        coverage = 180 - 135;
                        break;
                    case 8:
                        parallax = "bgs/07/02/bg3";
                        coverage = 180 - 156;
                        break;
                    case 9:
                        parallax = "bgs/06/plateau/bg0";
                        coverage = 180;
                        break;
                    case 10:
                        parallax = "bgs/06/bg0";
                        coverage = 180;
                        break;
                    case 11:
                        parallax = "bgs/06/bg1";
                        coverage = 0;
                        break;
                    case 12:
                        parallax = "bgs/06/bg2";
                        coverage = 0;
                        break;
                    case 13:
                        parallax = "bgs/05/temple/bg3";
                        coverage = 0;
                        break;
                    case 14:
                        parallax = "bgs/05/mirror/bg4";
                        coverage = 0;
                        break;
                }

                switch (parallax) {
                    case "bgs/06/bg1":
                    case "bgs/06/bg2":
                    case "bgs/05/temple/bg3":
                        var color2 = this.RandomColor(c => (int) c.R + (int) c.G + (int) c.B > 128*3);
                        map.Background.Children.Insert(0, new BinaryPacker.Element { Name = "parallax", Attributes = new Dictionary<string, object> {
                            {"texture", "bgs/06/fx0"},
                            {"blendMode", "additive"},
                            {"loopx", true}, {"loopy", true},
                            {"scrolly", 0.3f},
                            {"scrollx", 0.3f},
                            {"color", $"{color2.R:X2}{color2.G:X2}{color2.B:X2}"},
                            {"alpha", 0.15f},
                        }});
                        break;
                }
                var color = this.RandomColor();
                map.Background.Children.Insert(0, new BinaryPacker.Element { Name = "parallax", Attributes = new Dictionary<string, object> {
                    {"texture", parallax},
                    {"blendMode", "alphablend"},
                    {"loopx", true},
                    {"scrolly", 0f},
                    {"scrollx", parallax == "bgs/06/plateau/bg0" ? 0f : new [] {0.3f, 0.25f, 0.2f, 0.1f, 0f}[i]},
                    {"y", coverage == 0 ? this.Random.Next(180) : -totalCoverage},
                    {"color", $"{color.R:X2}{color.G:X2}{color.B:X2}"},
                }});

                totalCoverage += coverage;
                if (coverage >= 180) {
                    break;
                }
            }

            string bgEffect = null;
            switch (this.Random.Next(15)) {
                case 0:
                    // old site starfield
                    bgEffect = "stars";
                    break;
                case 1:
                    // starjump aurora
                    bgEffect = "northernlights";
                    break;
                case 2:
                case 3:
                case 4:
                    // like the city fg but in the bg
                    bgEffect = "snowBg";
                    break;
                case 5:
                case 6:
                case 7:
                    // Unused square bubbling effect
                    bgEffect = "dreamstars";
                    break;
                case 8:
                case 9:
                case 10:
                    // the purple and green stars from farewell
                    bgEffect = "planets";
                    break;
                case 11:
                case 12:
                case 13:
                    bgEffect = "starfield";
                    break;
                case 14:
                    bgEffect = "blackhole";
                    break;
            }

            if (!this.Settings.RandomDecorations) {
                bgEffect = "stars";
            }
            
            if (bgEffect != null) {
                map.Background.Children.Add(new BinaryPacker.Element { Name = bgEffect, Attributes = new Dictionary<string, object> {
                    {"scrollx", 0.3f},
                    {"scrolly", 0.3f},
                }});
                if (bgEffect == "planets" || bgEffect == "starfield") {
                    map.Background.Children.Add(new BinaryPacker.Element { Name = bgEffect, Attributes = new Dictionary<string, object> {
                        {"scrollx", 0.4f},
                        {"scrolly", 0.4f},
                        {"speed", 1.75f},
                    }});
                }
            }

            // starjump cutscene requires a northernlights bg
            // TODO maybe only do one bg for all instances of the room?
            if (bgEffect != "northernlights") {
                foreach (var room in map.Levels) {
                    if (room.Name.StartsWith("Celeste/6-Reflection/A/start")) {
                        map.Background.Children.Add(new BinaryPacker.Element {
                            Name = "northernlights",
                            Attributes = new Dictionary<string, object> {
                                { "only", room.Name }
                            }
                        });
                    }
                }
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

        private void SetPoem() {
            string poem;
            if (this.Random.Next(100) == 0) {
                poem = Dialog.Clean($"RANDOHEART_FIXED_{Random.Next(int.Parse(Dialog.Clean("RANDOHEART_FIXED_COUNT")))}");
            } else {
                int nounidx = Random.Next(int.Parse(Dialog.Clean("RANDOHEART_NOUN_COUNT")));
                var adjidx = Random.Next(int.Parse(Dialog.Clean("RANDOHEART_ADJ_COUNT")));
                string noun = Dialog.Clean($"RANDOHEART_NOUN_{nounidx}");
                string adj;
                if (Dialog.Clean("RANDOHEART_GENDER") == "true") { // TODO less restrictive check
                    string gender = Dialog.Clean($"RANDOHEART_NOUN_{nounidx}_GENDER");
                    adj = Dialog.Clean($"RANDOHEART_ADJ_{adjidx}_{gender}");
                } else {
                    adj = Dialog.Clean($"RANDOHEART_ADJ_{adjidx}");
                }
                poem = string.Format(Dialog.Get("RANDOHEART_ADJ_NOUN"), adj, noun);
            }
            var key = this.Key.GetSID().DialogKeyify() + "_A";
            AreaData.Get(this.Key).Mode[0].PoemID = key;
            Dialog.Language.Dialog["POEM_" + key] = poem;
            Dialog.Language.Cleaned["POEM_" + key] = poem;
        }

        private void SpawnGolden(MapData map) {
            if (Settings.SpawnGolden) {
                var lvl = map.GetAt(Vector2.Zero);
                var maxid = 0;
                foreach (var e in lvl.Entities) {
                    maxid = Math.Max(maxid, e.ID);
                }
                lvl.Entities.Add(new EntityData {
                    Level = lvl,
                    Name = "goldenBerry",
                    Position = lvl.Spawns[0],
                    ID = ++maxid,
                });
            }
        }
        private void SetDarkness(MapData map) {
            if (Settings.Darkness == Darkness.Vanilla) {
                return;
            }
            var dark = Settings.Darkness == Darkness.Always;
            foreach (var room in map.Levels) {
                room.Dark = dark;
            }
        }

        private void RandomizeDialog() {
            // If the random is used later, we don't want the number of mods to affect anything but the dialog
            Random dialogRandom = new Random(Random.Next());
            RandomDialogMappings.Clear();
            List<string> dialogIDs = new List<string>(Dialog.Language.Dialog.Keys);
            // Don't randomize poem names with the rest of the dialog
            dialogIDs.RemoveAll((id) => id.StartsWith("POEM_", StringComparison.InvariantCultureIgnoreCase));
            List<string> sortedDialogIDs = new List<string>(dialogIDs);
            sortedDialogIDs.Sort((s1, s2) => Dialog.Get(s1).Length.CompareTo(Dialog.Get(s2).Length));

            for(int i = 0; i < dialogIDs.Count; i++) {
                int rand = dialogRandom.Next(-40, 40);
                RandomDialogMappings[sortedDialogIDs[i].ToLower()] = sortedDialogIDs[Calc.Clamp(i + (rand == 0 ? 1 : rand), 0, sortedDialogIDs.Count-1)].ToLower();
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
