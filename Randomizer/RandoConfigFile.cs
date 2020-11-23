using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Celeste.Mod.Randomizer {
    public class RandoConfigFile {
        public List<RandoConfigRoom> ASide { get; set; }
        public List<RandoConfigRoom> BSide { get; set; }
        public List<RandoConfigRoom> CSide { get; set; }

        public static RandoConfigFile Load(AreaData area) {
            String fullPath = "Config/" + area.GetSID() + ".rando";
            Logger.Log("randomizer", $"Loading config from {fullPath}");
            if (!Everest.Content.TryGet(fullPath, out ModAsset asset)) {
                Logger.Log("randomizer", "...not found");
                return null;
            } else {
                using (StreamReader reader = new StreamReader(asset.Stream)) {
                    try {
                        return YamlHelper.Deserializer.Deserialize<RandoConfigFile>(reader);
                    } catch (YamlException e) {
                        throw new Exception($"Error parsing {area.GetSID()}.rando: {e.Message}");
                    }
                }
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

                    LevelData targetLvl = map.GetAt(hole.LowCoord(lvl.Bounds)) ?? map.GetAt(hole.HighCoord(lvl.Bounds));
                    if (targetLvl != null) {
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
            List<RandoConfigRoom> rooms;
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
        public String Room;
        public List<RandoConfigCollectable> Collectables = new List<RandoConfigCollectable>();
        public List<RandoConfigHole> Holes { get; set; } = new List<RandoConfigHole>();
        public List<RandoConfigRoom> Subrooms { get; set; }
        public List<RandoConfigInternalEdge> InternalEdges { get; set; }

        public bool End {
            get => this.ReqEnd != null;
            set => this.ReqEnd = value ? new RandoConfigReq() : null;
        }

        public RandoConfigReq ReqEnd { get; set; }
        public bool Hub { get; set; }
        public List<RandoConfigEdit> Tweaks { get; set; }
        public RandoConfigCoreMode Core { get; set; }
        public List<RandoConfigRectangle> ExtraSpace { get; set; }
        public float? Worth;
        public bool SpinnersShatter;
        public List<string> Flags;
    }

    public class RandoConfigRectangle {
        public int X, Y;
        public int Width, Height;
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
            get => null;

            set {
                this.ReqIn = value;
                this.ReqOut = value;
            }
        }
        public HoleKind Kind { get; set; }
        public int? Launch;
        public bool New;
        public RandoConfigHole Split;
    }

    public class RandoConfigCollectable {
        public int? Idx;
        public int? X;
        public int? Y;
        public bool MustFly;
    }

    public class RandoConfigInternalEdge {
        public String To { get; set; }
        public String Warp { get; set; }
        public RandoConfigReq ReqIn { get; set; }
        public RandoConfigReq ReqOut { get; set; }
        public RandoConfigReq ReqBoth {
            get => null;

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

        public int? Collectable;
        public bool CustomWarp;
    }

    public class RandoConfigReq {
        public List<RandoConfigReq> And;
        public List<RandoConfigReq> Or;

        public Difficulty Difficulty = Difficulty.Normal;
        public NumDashes? Dashes;
        public bool Key;
        public int? KeyholeID;
        public string Flag;
    }

    public class RandoConfigEdit {
        public String Name { get; set; }
        public int? ID { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public RandoConfigDecalType Decal { get; set; }
        public RandoConfigUpdate Update { get; set; }
    }

    public class RandoConfigUpdate {
        public bool Remove { get; set; }
        public bool Add { get; set; }
        public bool Default { get; set; }

        public float? X { get; set; }
        public float? Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public float? ScaleX { get; set; }
        public float? ScaleY { get; set; }
        public List<RandoConfigNode> Nodes { get; set; }
        public Dictionary<string, string> Values { get; set; }
        public string Tile;
    }

    public enum RandoConfigDecalType {
        None, FG, BG,
    }

    public class RandoConfigNode {
        public int Idx { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
    }

    public class RandoConfigCoreMode {
        private Session.CoreModes? left, right, up, down;
        public Session.CoreModes All = Session.CoreModes.None;

        public Session.CoreModes Left {
            get => left ?? All;
            set => left = value;
        }

        public Session.CoreModes Right {
            get => right ?? All;
            set => right = value;
        }

        public Session.CoreModes Up {
            get => up ?? All;
            set => up = value;
        }

        public Session.CoreModes Down {
            get => down ?? All;
            set => down = value;
        }
    }

    public class RandoMetadataFile {
        public List<string> CollectableNames = new List<string>();
        public List<RandoMetadataMusic> Music = new List<RandoMetadataMusic>();
        public List<RandoMetadataCampaign> Campaigns = new List<RandoMetadataCampaign>();

        [YamlIgnore] public Dictionary<string, RandoMetadataRuleset> RulesetsDict = new Dictionary<string, RandoMetadataRuleset>();

        public List<RandoMetadataRuleset> Rulesets {
            get => new List<RandoMetadataRuleset>(this.RulesetsDict.Values);
            set {
                foreach (var r in value) {
                    if (String.IsNullOrEmpty(r.Name)) {
                        throw new Exception("Rulesets must have Name specified");
                    }
                    if (this.RulesetsDict.ContainsKey(r.Name)) {
                        throw new Exception($"Ruleset name '{r.Name}' is duplicated");
                    }
                    this.RulesetsDict[r.Name] = r;
                }
            }
        }

        public void Add(RandoMetadataFile other) {
            this.CollectableNames.AddRange(other.CollectableNames);
            this.Music.AddRange(other.Music);
            this.Campaigns.AddRange(other.Campaigns);
            this.Rulesets = other.Rulesets; // rely on setter behavior to use this as an updater
        }
        
        public static RandoMetadataFile LoadAll() {
            var result = new RandoMetadataFile();

            Regex r = new Regex("^[^\\\\/]+:(/|\\\\).*$");
            foreach (var kv in Everest.Content.Map.Where(kv => !r.IsMatch(kv.Key) && Path.GetFileName(kv.Value.PathVirtual) == "rando" && kv.Value.Type == typeof(AssetTypeYaml))) {
                Logger.Log("randomizer", $"Found metadata {kv.Value.PathVirtual} in {kv.Value.Source.Name}");
                result.Add(Load(kv.Value));
            }
            return result;
        }
        
        private static RandoMetadataFile Load(ModAsset asset) {
            // do not catch errors, they should crash on load
            using (StreamReader reader = new StreamReader(asset.Stream)) {
                return YamlHelper.Deserializer.Deserialize<RandoMetadataFile>(reader);
            }
        }
    }

    public class RandoMetadataMusic {
        public string Name;
        private float weight = 1;

        public float Weight {
            get => this.weight;
            set => this.weight = (value >= 0 && value <= 3) ? value : 1f;
        }
    }

    public class RandoMetadataCampaign {
        public string Name;
        public List<RandoMetadataLevelSet> LevelSets;
    }

    public class RandoMetadataLevelSet {
        public string Name;
        public string ID;
    }

    public class RandoMetadataRuleset {
        public string Name;
        private string longName;

        public string LongName {
            get => this.longName ?? "Ruleset " + this.Name;
            set => this.longName = value;
        }

        public List<RandoSettings.AreaKeyNotStupid> EnabledMaps = null;
        public bool RepeatRooms = false;
        public bool EnterUnknown = false;
        public bool Variants = false;
        public ShineLights Lights = ShineLights.Hubs;
        public Darkness Darkness = Darkness.Never;
        
        public LogicType Algorithm = LogicType.Pathway;
        public MapLength Length = MapLength.Short;
        public NumDashes Dashes = NumDashes.One;
        public Difficulty Difficulty = Difficulty.Normal;
        public DifficultyEagerness DifficultyEagerness = DifficultyEagerness.Medium;
    }
}

