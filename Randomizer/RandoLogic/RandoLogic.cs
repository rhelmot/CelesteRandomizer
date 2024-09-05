using System;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer
{
    public class GenerationError : Exception
    {
        public GenerationError(string message) : base(message) { }
    }

    public class RetryException : Exception
    {
        public RetryException() : base("Too many backtracks") { }
    }

    public partial class RandoLogic
    {
        public static AreaKey GenerateMap(RandoSettings settings)
        {
            StringWriter builder = new StringWriter();
            builder.Write("Generating map with settings:\n");
            YamlHelper.Serializer.Serialize(builder, settings);
            Logger.Log("randomizer", builder.ToString());
            LazyReload(settings.EnabledMaps);

            var newID = AreaData.Areas.Count;
            if (AreaData.Areas.Last().SID.StartsWith("randomizer/"))
            {
                newID--;
            }

            var newArea = new AreaData
            {
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
            newArea.Meta = new Meta.MapMeta
            {
                Modes = new Meta.MapMetaModeProperties[] {
                    new Meta.MapMetaModeProperties {
                        HeartIsEnd = true,
                        //SeekerSlowdown = true,  // this doesn't do anything
                    },
                    null, null
                }
            };
            newArea.OnLevelBegin = (level) =>
            {
                level.Add(new SeekerEffectsController());
            };
            var dyn = new DynData<AreaData>(newArea);
            dyn.Set<RandoSettings>("RandoSettings", settings.Copy());

            newArea.SID = $"randomizer/{newArea.Name}";

            // avert race condition
            RandoModule.AreaHandoff = newArea;
            while (RandoModule.AreaHandoff != null)
            {
                Thread.Sleep(10);
            }

            // invalidate the MapEditor area key cache, as it will erroniously see a cache hit
            typeof(Editor.MapEditor).GetField("area", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, AreaKey.None);

            var key = new AreaKey(newArea.ID);

            int tryNum = 0;
            while (true)
            {
                try
                {
                    var r = new RandoLogic(settings, key, tryNum);

                    newArea.Mode[0].MapData = r.MakeMap();
                    newArea.Wipe = r.PickWipe();
                    newArea.CompleteScreenName = r.PickCompleteScreen();
                    newArea.CassetteSong = r.PickCassetteAudio();
                    newArea.Mode[0].AudioState = r.PickAudioState();
                    if (settings.RandomColors)
                    {
                        newArea.BloomBase = (float)Math.Pow(r.Random.NextFloat(), 5) * r.Random.NextFloat();
                        newArea.DarknessAlpha = r.Random.NextFloat() * (float)Math.Pow(r.Random.NextFloat(), 0.5) * (float)Math.Pow(r.Random.NextFloat(), 2) * 0.35f;
                        newArea.ColorGrade = r.PickColorGrade();
                    }
                    r.RandomizeDialog();
                    break;
                }
                catch (RetryException)
                {
                    tryNum++;
                    if (tryNum >= 500)
                    {
                        throw new GenerationError("Cannot create map with these settings");
                    }
                    Logger.Log("randomizer", $"Too many retries ({tryNum}), starting again");
                }
            }

            Logger.Log("randomizer", $"new area {newArea.SID}");

            return key;
        }

        private Random Random;
        private List<StaticRoom> RemainingRooms = new List<StaticRoom>();
        private AreaKey Key;
        private LinkedMap Map;
        private RandoSettings Settings;
        private Capabilities Caps;
        public static Dictionary<string, string> RandomDialogMappings = new Dictionary<string, string>();

        private void ResetRooms()
        {
            this.RemainingRooms.Clear();
            foreach (var room in RandoLogic.AllRooms)
            {
                if (this.Settings.MapIncluded(room.Area))
                {
                    this.RemainingRooms.Add(room);
                }
            }

            this.RemainingRooms.Sort((x, y) =>
            {
                var z = String.Compare(x.Area.SID, y.Area.SID, comparisonType: StringComparison.Ordinal);
                if (z != 0)
                {
                    return z;
                }

                z = x.Area.Mode.CompareTo(y.Area.Mode);
                if (z != 0)
                {
                    return z;
                }

                z = String.Compare(x.Level.Name, y.Level.Name, comparisonType: StringComparison.Ordinal);
                return z;
            });
        }

        private RandoLogic(RandoSettings settings, AreaKey key, int tryNum)
        {
            this.Random = new Random((int)settings.IntSeed);
            for (int i = 0; i < settings.EndlessLevel; i++)
            {
                this.Random = new Random(this.Random.Next());
            }
            for (int i = 0; i < tryNum; i++)
            {
                this.Random.Next();
                this.Random = new Random(this.Random.Next());
            }
            this.Settings = settings;
            this.Key = key;
            this.ResetRooms();
            this.Caps = new Capabilities
            {
                Dashes = settings.Dashes,
                PlayerSkill = settings.Difficulty,
                HasKey = true,
                Flags = new Dictionary<string, FlagState>(),
            };
        }

        private Action<Scene, bool, Action> PickWipe()
        {
            return (scene, wipeIn, onComplete) =>
            {
                switch (new Random().Next(10))
                {
                    case 0:
                        new CurtainWipe(scene, wipeIn, onComplete);
                        break;
                    case 1:
                        new AngledWipe(scene, wipeIn, onComplete);
                        break;
                    case 2:
                        new DropWipe(scene, wipeIn, onComplete);
                        break;
                    case 3:
                        new DreamWipe(scene, wipeIn, onComplete);
                        break;
                    case 4:
                        new WindWipe(scene, wipeIn, onComplete);
                        break;
                    case 5:
                        new FallWipe(scene, wipeIn, onComplete);
                        break;
                    case 6:
                        new HeartWipe(scene, wipeIn, onComplete);
                        break;
                    case 7:
                        new KeyDoorWipe(scene, wipeIn, onComplete);
                        break;
                    case 8:
                        new MountainWipe(scene, wipeIn, onComplete);
                        break;
                    case 9:
                        new StarfieldWipe(scene, wipeIn, onComplete);
                        break;
                }
            };
        }

        private AudioState PickAudioState()
        {
            var result = new AudioState();
            result.Ambience.Event = "event:/env/amb/04_main"; // only way to get wind effects?

            float totalWeight = RandoModule.Instance.MetaConfig.Music.Select(t => t.Weight).Sum();
            float weighAt = this.Random.NextFloat(totalWeight);
            float soFar = 0f;
            foreach (var track in RandoModule.Instance.MetaConfig.Music)
            {
                soFar += track.Weight;
                if (weighAt < soFar)
                {
                    result.Music.Event = track.Name;
                    foreach (var param in track.Parameters)
                    {
                        result.Music.Param(param.Key, param.Value);
                    }
                    break;
                }
            }

            return result;
        }

        private string PickCassetteAudio()
        {
            switch (this.Random.Next(8))
            {
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

        private string PickCompleteScreen()
        {
            uint seed = this.Settings.IntSeed;
        tryagain:
            // ensure different rulesets of the same seed have different end screens
            var rulesInt = 0;
            foreach (var ch in this.Settings.Rules)
            {
                rulesInt += ch;
            }
            switch ((seed + rulesInt) % 8)
            {
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
                    if (this.Settings.Algorithm == LogicType.Endless)
                    {
                        seed = (uint)new Random((int)seed).Next();
                        goto tryagain;
                    }
                    return AreaData.Areas[9].CompleteScreenName;
            }
        }

        private string PickColorGrade()
        {
            if (this.Random.Next(10) != 0)
            {
                return "none";
            }

            return this.Random.Choose("cold", "credits", "feelingdown", "golden", "hot", "oldsite", "panicattack", "reflection", "templevoid");
        }

        private MapData MakeMap()
        {
            this.Map = new LinkedMap();
            if (this.Settings.IsLabyrinth)
            {
                this.GenerateLabyrinth();
            }
            else
            {
                this.GeneratePathway();
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
            this.PlaceTheoPhone(map);

            return map;
        }

        private void SetMapBounds(MapData map)
        {
            int num1 = int.MaxValue;
            int num2 = int.MaxValue;
            int num3 = int.MinValue;
            int num4 = int.MinValue;
            foreach (LevelData level in map.Levels)
            {
                if (level.Bounds.Left < num1)
                    num1 = level.Bounds.Left;
                if (level.Bounds.Top < num2)
                    num2 = level.Bounds.Top;
                if (level.Bounds.Right > num3)
                    num3 = level.Bounds.Right;
                if (level.Bounds.Bottom > num4)
                    num4 = level.Bounds.Bottom;
            }

            map.Bounds = new Rectangle(num1 - 50, num2 - 50, num3 - num1 + 100, num4 - num2 + 100);
        }

        private void SetForeground(MapData map)
        {
            map.Foreground = new BinaryPacker.Element { Children = new List<BinaryPacker.Element>() };
            var effect = this.Random.Choose(RandoModule.Instance.MetaConfig.FgEffects);
            if (!this.Settings.RandomBackgrounds)
            {
                effect = RandoModule.Instance.MetaConfig.FgEffects.First(x => x.Effect == "stardust");
            }
            map.Foreground.Children.AddRange(this.Styleground(effect));

            var windyOnly = string.Join(",", this.FindWindyLevels(map));
            if (!effect.ProvidesWind && !string.IsNullOrEmpty(windyOnly))
            {
                map.Foreground.Children.Add(new BinaryPacker.Element
                {
                    Name = "stardust",
                    Attributes = new Dictionary<string, object> {
                        {"only", windyOnly}
                    }
                });
            }

            // this cutscene hardcodes a reference to a windsnow fg
            // the level should only ever be last on the list, right?
            if (effect.Effect != "windsnow" && map.Levels.Where(lvl => lvl.Name.StartsWith("Celeste/4-GoldenRidge/A/d-10")).Any())
            {
                // There is a miniscule chance of this level being second to last due to fake end
                var idx = map.Levels.FindIndex(lvl => lvl.Name.StartsWith("Celeste/4-GoldenRidge/A/d-10"));
                map.Foreground.Children.Add(new BinaryPacker.Element
                {
                    Name = "windsnow",
                    Attributes = new Dictionary<string, object> {
                       {"only", map.Levels[idx].Name }
                    }
                });
            }
        }

        private Color RandomColor(Func<Color, bool> filter = null)
        {
            var possibleColors = new List<Color>(typeof(Color).GetProperties(BindingFlags.Static | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(Color))
                .Select(p => (Color)p.GetValue(null))
                .Where(c => c.A == 255)
                .Where(filter ?? (c => true)));
            return possibleColors[this.Random.Next(possibleColors.Count)];
        }

        private IEnumerable<BinaryPacker.Element> Styleground(RandoMetadataBackground element,
                float scrollX = 0.3f, float scrollY = 0.3f, int y = 0)
        {

            var color = this.RandomColor();
            if (!string.IsNullOrEmpty(element.Texture))
            {
                yield return new BinaryPacker.Element
                {
                    Name = "parallax",
                    Attributes = new Dictionary<string, object> {
                        {"texture", element.Texture},
                        {"blendMode", element.BlendMode},
                        {"loopx", element.LoopX}, {"loopy", element.LoopY},
                        {"flipx", element.FlipX}, {"flipy", element.FlipY},
                        {"speedx", element.SpeedX}, {"speedy", element.SpeedY},
                        {"scrollx", scrollX * element.ScrollFactorY}, {"scrolly", scrollY * element.ScrollFactorY},
                        {"x", element.OffX}, {"y", y + element.OffY},
                        {"color", $"{color.R:X2}{color.G:X2}{color.B:X2}"},
                        {"alpha", element.Alpha},
                    }
                };
            }
            else if (!string.IsNullOrEmpty(element.Effect))
            {
                yield return new BinaryPacker.Element
                {
                    Name = element.Effect,
                    Attributes = new Dictionary<string, object> {
                        {"scrollx", scrollX * element.ScrollFactorY}, {"scrolly", scrollY * element.ScrollFactorY},
                        {"color", $"{color.R:X2}{color.G:X2}{color.B:X2}"},
                    },
                };
            }
            else
            {
                throw new Exception("Config error: styleground without Texture or Effect");
            }

            if (element.NeedsColor)
            {
                var color2 = this.RandomColor(c => (int)c.R + (int)c.G + (int)c.B > 128 * 3);
                yield return new BinaryPacker.Element
                {
                    Name = "parallax",
                    Attributes = new Dictionary<string, object> {
                        {"texture", "bgs/06/fx0"},
                        {"blendMode", "additive"},
                        {"loopx", true}, {"loopy", true},
                        {"scrolly", 0.3f},
                        {"scrollx", 0.3f},
                        {"color", $"{color2.R:X2}{color2.G:X2}{color2.B:X2}"},
                        {"alpha", 0.15f},
                    }
                };
            }

            if (element.AndThen != null)
            {
                foreach (var e in this.Styleground(element.AndThen, scrollX, scrollY, y))
                {
                    yield return e;
                }
            }
        }

        private void SetBackground(MapData map)
        {
            map.BackgroundColor = this.RandomColor();

            int maxY = map.Bounds.Bottom - 180;

            map.Background = new BinaryPacker.Element { Children = new List<BinaryPacker.Element>() };
            const int layers = 5;
            for (int i = 0; i < layers; i++)
            {
                var picked = this.Random.Choose(RandoModule.Instance.MetaConfig.Backgrounds);

                if (picked.CoverTop != 0 && !picked.LoopY)
                {
                    i--;
                    continue;
                }

                if (picked.Opaque && this.Random.Next(layers) > i)
                {
                    // bias not picking opaque backgrounds until we've already added a few
                    i--;
                    continue;
                }

                float scrollX = new[] { 0.3f, 0.25f, 0.2f, 0.1f, 0.05f }[i];
                float scrollY = new[] { 0.1f, 0.05f, 0.03f, 0.02f, 0.01f }[i];
                int y = 0;
                if (picked.Texture != null && !(picked.Opaque && !picked.LoopY))
                {
                    int height = GFX.Game[picked.Texture].Height;
                    y = (int)Math.Ceiling(maxY * scrollY + 180 - height);

                    if (picked.CoverBottom != 0)
                    {
                        maxY = (int)((180 - height + picked.CoverBottom - y) / -scrollY);
                    }
                }

                map.Background.Children.InsertRange(0, this.Styleground(picked, scrollX, scrollY, y));

                if (picked.Opaque)
                {
                    break;
                }
            }

            var effect = this.Random.Choose(RandoModule.Instance.MetaConfig.BgEffects);
            map.Background.Children.AddRange(this.Styleground(effect));

            if (!this.Settings.RandomBackgrounds)
            {
                map.Background.Children = new List<BinaryPacker.Element> {
                    new BinaryPacker.Element {
                        Name = "parallax",
                        Attributes = new Dictionary<string, object> {
                            {"texture", "bgs/10/sky"},
                            {"loopx", true}, {"loopy", true},
                            {"scrollx", 0.3f}, {"scrolly", 0.3f},
                        }
                    }
                };
            }

            // starjump cutscene requires a northernlights bg
            var lightsOnly = string.Join(",", map.Levels.Where(room => room.Name.StartsWith("Celeste/6-Reflection/A/start")));
            if (map.Background.Children.All(elem => elem.Name != "northernlights") && !string.IsNullOrEmpty("lightsOnly"))
            {
                map.Background.Children.Insert(0, new BinaryPacker.Element
                {
                    Name = "northernlights",
                    Attributes = new Dictionary<string, object> {
                        {"only", lightsOnly}
                    }
                });
            }
        }

        private IEnumerable<string> FindWindyLevels(MapData map)
        {
            foreach (var lvl in map.Levels)
            {
                if (lvl.WindPattern != WindController.Patterns.None)
                {
                    yield return lvl.Name;
                }
                else
                {
                    foreach (var trigger in lvl.Triggers)
                    {
                        if (trigger.Name == "windTrigger")
                        {
                            yield return lvl.Name;
                            break;
                        }
                    }
                }
            }
        }

        private void SetPoem()
        {
            string poem;
            if (this.Random.Next(100) == 0)
            {
                poem = Dialog.Clean($"RANDOHEART_FIXED_{Random.Next(int.Parse(Dialog.Clean("RANDOHEART_FIXED_COUNT")))}");
            }
            else
            {
                int nounidx = Random.Next(int.Parse(Dialog.Clean("RANDOHEART_NOUN_COUNT")));
                var adjidx = Random.Next(int.Parse(Dialog.Clean("RANDOHEART_ADJ_COUNT")));
                string noun = Dialog.Clean($"RANDOHEART_NOUN_{nounidx}");
                string adj;
                if (Dialog.Clean("RANDOHEART_GENDER") == "true")
                { // TODO less restrictive check
                    string gender = Dialog.Clean($"RANDOHEART_NOUN_{nounidx}_GENDER");
                    adj = Dialog.Clean($"RANDOHEART_ADJ_{adjidx}_{gender}");
                }
                else
                {
                    adj = Dialog.Clean($"RANDOHEART_ADJ_{adjidx}");
                }
                poem = string.Format(Dialog.Get("RANDOHEART_ADJ_NOUN"), adj, noun);
            }
            var key = this.Key.GetSID().DialogKeyify() + "_A";
            AreaData.Get(this.Key).Mode[0].PoemID = key;
            Dialog.Language.Dialog["POEM_" + key] = poem;
            Dialog.Language.Cleaned["POEM_" + key] = poem;
        }

        private void SpawnGolden(MapData map)
        {
            if (Settings.SpawnGolden)
            {
                var lvl = map.GetAt(Vector2.Zero);
                var maxid = 0;
                foreach (var e in lvl.Entities)
                {
                    maxid = Math.Max(maxid, e.ID);
                }
                lvl.Entities.Add(new EntityData
                {
                    Level = lvl,
                    Name = "goldenBerry",
                    Position = lvl.Spawns[0],
                    ID = ++maxid,
                });
            }
        }
        private void SetDarkness(MapData map)
        {
            if (Settings.Darkness == Darkness.Vanilla)
            {
                return;
            }
            var dark = Settings.Darkness == Darkness.Always;
            foreach (var room in map.Levels)
            {
                room.Dark = dark;
            }
        }

        private void PlaceTheoPhone(MapData map)
        {
            while (true)
            {
                //Logger.Log("DEBUG", "Trying to place phone...");
                var lvl = this.Random.Choose(map.Levels);
                var regex = new Regex("\\r\\n|\\n\\r|\\n|\\r");
                var lines = new List<string>(regex.Split(lvl.Solids));
                char at(int xx, int yy) => yy >= lines.Count ? '0' : xx >= lines[yy].Length ? '0' : lines[yy][xx];
                var height = lines.Count;
                var width = lines.Select(j => j.Length).Max();
                var found = false;
                int x = 0, y = 0;
                for (int i = 0; i < 20 && !found; i++)
                {
                    x = this.Random.Next(width);
                    y = this.Random.Next(height);
                    var ch = at(x, y);
                    var dir = at(x, y) == '0' ? 1 : -1;
                    for (y += dir; y >= 0 && y < height; y += dir)
                    {
                        var ch2 = at(x, y);
                        var edge = (ch == '0') != (ch2 == '0');
                        if (edge)
                        {
                            if (dir == -1)
                            {
                                y++;
                            }
                            var BehindEnt = lvl.Entities.Where(e =>
                            {
                                // entities that don't have these fields default to 0,
                                // but to be certain the player can see the phone, check one tile over
                                var entWidth = e.Width + 8;
                                var entHeight = e.Height + 8;

                                return e.Position.X / 8 + entWidth / 8 >= x && e.Position.X / 8 - 1 <= x && e.Position.Y / 8 + entHeight / 8 >= y && e.Position.Y / 8 - 1 <= y;
                            }).Any();
                            var InsideRoof = lvl.FgDecals.Where(fg =>
                            {
                                if (fg.Scale.X < 0)
                                {
                                    return (fg.Position.X) / 8 >= x && (fg.Position.X + 8 * fg.Scale.X) / 8 <= x && (fg.Position.Y + 4) / 8 == y;
                                }
                                return (fg.Position.X) / 8 <= x && (fg.Position.X + 8 * fg.Scale.X) / 8 >= x && (fg.Position.Y + 4) / 8 == y;
                            }).Any();
                            if (at(x + 1, y - 1) == '0' && at(x + 1, y) != '0' && !BehindEnt && !InsideRoof)
                            {
                                found = true;
                            }
                            break;
                        }
                    }
                }

                if (!found)
                {
                    continue;
                }
                var maxid = 0;
                foreach (var e in lvl.Entities)
                {
                    maxid = Math.Max(maxid, e.ID);
                }
                Logger.Log("Randomizer", $"Adding phone at {lvl.Name} {x}x{y}");
                lvl.Entities.Add(new EntityData
                {
                    Level = lvl,
                    Name = "randomizer/TheoPhone",
                    Position = new Vector2(x * 8f + 4f, y * 8f),
                    ID = ++maxid,
                });
                break;
            }
        }

        private void RandomizeDialog()
        {
            RandomDialogMappings.Clear();
            // Shuffle spoken and nonspoken dialog separately for better results
            List<string> spokenDialogIDs = GetSortedDialogIDs(spoken: true);
            List<string> nonspokenDialogIDs = GetSortedDialogIDs(spoken: false);
            List<string> shuffledSpokenDialogIDs = ShuffleDialogIDsByLength(spokenDialogIDs);
            List<string> shuffledNonspokenDialogIDs = ShuffleDialogIDsByLength(nonspokenDialogIDs);
            for (int i = 0; i < spokenDialogIDs.Count; i++)
            {
                RandomDialogMappings[spokenDialogIDs[i].ToLower()] = shuffledSpokenDialogIDs[i].ToLower();
            }
            for (int i = 0; i < nonspokenDialogIDs.Count; i++)
            {
                RandomDialogMappings[nonspokenDialogIDs[i].ToLower()] = shuffledNonspokenDialogIDs[i].ToLower();
            }
        }

        private List<string> GetSortedDialogIDs(bool spoken)
        {
            List<string> dialogIDs = new List<string>(Dialog.Language.Dialog.Keys);
            // Don't touch poem names or mad lib templates
            dialogIDs.RemoveAll((id) => id.StartsWith("POEM_", StringComparison.InvariantCultureIgnoreCase));
            dialogIDs.RemoveAll((id) => id.StartsWith("RANDO_", StringComparison.InvariantCultureIgnoreCase));
            dialogIDs.RemoveAll((id) => id.StartsWith("RANDOHEART_", StringComparison.InvariantCultureIgnoreCase));
            // Don't touch anything which is madlib-randomized
            dialogIDs.RemoveAll((id) => Dialog.Has("RANDO_" + id));
            // Quick, naive way to distinguish spoken dialog
            if (spoken)
            {
                dialogIDs.RemoveAll((id) => !Dialog.Get(id).Contains("portrait"));
            }
            else
            {
                dialogIDs.RemoveAll((id) => Dialog.Get(id).Contains("portrait"));
            }
            dialogIDs.Sort((s1, s2) => Dialog.Get(s1).Length.CompareTo(Dialog.Get(s2).Length));
            return dialogIDs;
        }

        // The idea here is to have our results be random, but reasonably close to the same length
        // ~10% of the list gets shuffled with itself at a time (irregular segment at the end)
        static List<string> ShuffleDialogIDsByLength(List<string> sortedIDs)
        {
            Random rng = new Random();
            List<string> shuffledIDs = new List<string>(sortedIDs);
            int segments = 10;
            int segSize = shuffledIDs.Count / segments;
            for (int i = shuffledIDs.Count - 1; i > 0; i--)
            {
                int start = (i / segSize) * segSize;
                int swapIndex = rng.Next(start, i + 1);
                string tmp = shuffledIDs[i];
                shuffledIDs[i] = shuffledIDs[swapIndex];
                shuffledIDs[swapIndex] = tmp;
            }
            return shuffledIDs;
        }

        private List<StaticEdge> AvailableNewEdges(Capabilities capsIn, Capabilities capsOut, Func<StaticEdge, bool> filter = null)
        {
            var result = new List<StaticEdge>();

            foreach (var room in this.RemainingRooms)
            {
                foreach (var node in room.Nodes.Values)
                {
                    foreach (var edge in node.Edges)
                    {
                        if (edge.HoleTarget == null)
                        {
                            continue;
                        }
                        if (edge.HoleTarget.Kind == HoleKind.Unknown && !this.Settings.EnterUnknown)
                        {
                            continue;
                        }
                        if (capsIn != null && !edge.ReqIn.Able(capsIn))
                        {
                            continue;
                        }
                        if (capsOut != null && !edge.ReqOut.Able(capsOut))
                        {
                            continue;
                        }
                        if (filter != null && !filter(edge))
                        {
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
