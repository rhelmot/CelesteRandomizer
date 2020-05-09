using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer {

    public enum ScreenDirection {
        Up, Down, Left, Right
    }

    public static class ScreenDirectionMethods {
        public static Vector2 Unit(this ScreenDirection self) {
            switch (self) {
                case ScreenDirection.Up:
                    return -Vector2.UnitY;
                case ScreenDirection.Down:
                    return Vector2.UnitY;
                case ScreenDirection.Left:
                    return -Vector2.UnitX;
                case ScreenDirection.Right:
                    return Vector2.UnitX;
                default:
                    return Vector2.Zero;
            }
        }

        public static ScreenDirection RotCW(this ScreenDirection self) {
            switch (self) {
                case ScreenDirection.Up:
                    return ScreenDirection.Right;
                case ScreenDirection.Down:
                    return ScreenDirection.Left;
                case ScreenDirection.Left:
                    return ScreenDirection.Up;
                case ScreenDirection.Right:
                    return ScreenDirection.Down;
                default:
                    return ScreenDirection.Up;
            }
        }

        public static ScreenDirection FromString(String str) {
            switch (str.ToLower()) {
                case "up":
                    return ScreenDirection.Up;
                case "down":
                    return ScreenDirection.Down;
                case "left":
                    return ScreenDirection.Left;
                case "right":
                    return ScreenDirection.Right;
                default:
                    throw new Exception("Bad ScreenDirection " + str);
            }
        }
    }

    public enum HoleKind {
        None, In, Out, InOut, Unknown
    }

    public static class HoleKindMethods {
        public static HoleKind FromString(String str) {
            switch (str.ToLower()) {
                case "none":
                    return HoleKind.None;
                case "in":
                    return HoleKind.In;
                case "out":
                    return HoleKind.Out;
                case "inout":
                    return HoleKind.InOut;
                default:
                    throw new Exception("Bad hole kind " + str);
            }
        }
    }

    public class Hole {
        public LevelData Level;
        public ScreenDirection Side;
        public HoleKind Kind = HoleKind.Unknown;
        public int LowBound;
        public int HighBound;
        public bool HighOpen;

        public bool LowOpen {
            get {
                return this.LowBound == 0;
            }
        }

        public bool BothOpen {
            get {
                return this.HighOpen && this.LowOpen;
            }
        }

        public bool HalfOpen {
            get {
                return this.LowOpen ^ this.HighOpen;
            }
        }

        public bool Closed {
            get {
                return !this.LowOpen && !this.HighOpen;
            }
        }

        public int Size {
            get {
                return this.HighBound - this.LowBound + 1;
            }
        }

        public ScreenDirection AlongDir {
            get {
                switch (this.Side) {
                    case ScreenDirection.Up:
                    case ScreenDirection.Down:
                        return ScreenDirection.Right;
                    case ScreenDirection.Left:
                    case ScreenDirection.Right:
                    default:
                        return ScreenDirection.Down;
                }
            }
        }

        public Vector2 LowCoord {
            get {
                Vector2 corner;
                switch (this.Side) {
                    case ScreenDirection.Up:
                        corner = new Vector2(Level.Bounds.Left + 4, Level.Bounds.Top - 5);
                        break;
                    case ScreenDirection.Left:
                        corner = new Vector2(Level.Bounds.Left - 5, Level.Bounds.Top + 4);
                        break;
                    case ScreenDirection.Down:
                        corner = new Vector2(Level.Bounds.Left + 4, Level.Bounds.Bottom + 4);
                        break;
                    case ScreenDirection.Right:
                    default:
                        corner = new Vector2(Level.Bounds.Right + 4, Level.Bounds.Top + 4);
                        break;
                }
                return corner + this.AlongDir.Unit() * this.LowBound * 8;
            }
        }

        public Vector2 HighCoord {
            get {
                return this.LowCoord + this.AlongDir.Unit() * 8 * (this.Size - 1);
            }
        }

        public Hole(LevelData Level, ScreenDirection Side, int LowBound, int HighBound, bool HighOpen) {
            this.Level = Level;
            this.Side = Side;
            this.LowBound = LowBound;
            this.HighBound = HighBound;
            this.HighOpen = HighOpen;
        }

        public override String ToString() {
            if (this.BothOpen) {
                return $"{this.Side} (-inf, inf)";
            } else if (this.LowOpen) {
                return $"{this.Side} (-inf, {this.HighBound}]";
            } else if (this.HighOpen) {
                return $"{this.Side} [{this.LowBound}, inf)";
            } else {
                return $"{this.Side} [{this.LowBound}, {this.HighBound}]";
            }
        }

        // negative MIN_INT is still MIN_INT
        public const int INCOMPATIBLE = -0x80000000;

        public int Compatible(Hole other) {
            int alignLow() {
                return this.LowBound - other.LowBound;
            }
            int alignHigh() {
                return this.HighBound - other.HighBound;
            }

            if (other.Side == ScreenDirection.Up && this.Side != ScreenDirection.Up && this.Side != ScreenDirection.Left) {
                return -other.Compatible(this);
            }
            if (other.Side == ScreenDirection.Left && this.Side != ScreenDirection.Left && this.Side != ScreenDirection.Up) {
                return -other.Compatible(this);
            }

            if (this.Side == ScreenDirection.Up && other.Side == ScreenDirection.Down) {
                // Vertical transitions
                if (this.BothOpen || other.BothOpen) {
                    // if either is open on both ends, they must line up perfectly
                    if (this.BothOpen && other.BothOpen && this.Size == other.Size) {
                        return 0;
                    }
                } else if (this.HalfOpen || other.HalfOpen) {
                    // if either is half-open, they must be the same half open
                    if (this.LowOpen == other.LowOpen) {
                        return this.LowOpen ? alignHigh() : alignLow();
                    }
                } else {
                    // Only remaining option is both closed. they must be the same size
                    if (this.Size == other.Size) {
                        return alignLow();
                    }
                }
            }
            if (this.Side == ScreenDirection.Left && other.Side == ScreenDirection.Right) {
                // Horizontal transitions
                if (this.HighOpen || other.HighOpen) {
                    // if either is open on the bottom, they both must be open on the bottom and we align the death planes
                    // this is kind of a questionable choice all around tbh
                    // maybe additionally restrict that sizes must be the same?
                    if (this.HighOpen && other.HighOpen) {
                        return alignHigh();
                    }
                } else if (this.LowOpen || other.LowOpen) {
                    // if either is open on the top, the other must also be open on the top OR it must be sufficiently tall
                    if (this.LowOpen && other.LowOpen) {
                        return alignHigh();
                    } else if (this.Closed && this.Size > 3) {
                        return alignHigh();
                    } else if (other.Closed && other.Size > 3) {
                        return alignHigh();
                    }
                } else {
                    // only remaining option is both closed. they must be the same size OR sufficiently tall
                    if (this.Size == other.Size) {
                        return alignHigh();
                    } else if (this.Size > 3 && other.Size > 3) {
                        return alignHigh();
                    }
                }
            }
            return INCOMPATIBLE;
        }
    }

    public class RandoRoom {
        public LevelData Level;
        public List<Hole> Holes;
        public readonly String Name;

        public RandoRoom(String prefix, LevelData Level, List<Hole> Holes) {
            this.Name = prefix + Level.Name;
            this.Level = Level;
            this.Holes = Holes;
        }

        private LevelData LevelCopy(Vector2 newPosition) {
            var result = this.Level.Copy();
            result.Name = this.Name;
            result.Position = newPosition;
            return result;
        }

        public LevelData LinkStart() {
            return this.LevelCopy(Vector2.Zero);
        }

        public LevelData LinkAdjacent(LevelData against, ScreenDirection side, int offset) {
            return this.LevelCopy(this.NewPosition(against, side, offset));
        }

        public Rectangle QuickLinkAdjacent(LevelData against, ScreenDirection side, int offset) {
            var pos = this.NewPosition(against, side, offset);
            return new Rectangle((int)pos.X, (int)pos.Y, this.Level.Bounds.Width, this.Level.Bounds.Height);
        }

        private Vector2 NewPosition(LevelData against, ScreenDirection side, int offset) {
            int roundUp(int inp) {
                while (inp % 8 != 0) {
                    inp++;
                }
                return inp;
            }
            switch (side) {
                case ScreenDirection.Up:
                    return against.Position + new Vector2(offset * 8, -roundUp(this.Level.Bounds.Height));
                case ScreenDirection.Down:
                    return against.Position + new Vector2(offset * 8, roundUp(against.Bounds.Height));
                case ScreenDirection.Left:
                    return against.Position + new Vector2(-roundUp(this.Level.Bounds.Width), offset * 8);
                case ScreenDirection.Right:
                default:
                    return against.Position + new Vector2(roundUp(against.Bounds.Width), offset * 8);
            }
        }
    }

    public class RandoLogic {
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
                        throw new Exception($"Could not find the hole identified by room:{roomConfig.Room} side:{holeConfig.Side} idx:{holeConfig.Idx}");
                    } else {
                        //Logger.Log("randomizer", $"Matching {roomConfig.Room} {holeConfig.Side} {holeConfig.Idx} to {matchedHole}");
                        matchedHole.Kind = HoleKindMethods.FromString(holeConfig.Kind);
                    }
                }
            }

            return result;
        }

        private static List<RandoRoom> ProcessArea(AreaData area, AreaMode? mode=null) {
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
            RandoLogic.AllRooms = new List<RandoRoom>();
            /*RandoLogic.AllRooms.AddRange(RandoLogic.ProcessArea(AreaData.Areas[1], AreaMode.Normal));
            return;*/

            foreach (var area in AreaData.Areas) {
                RandoLogic.AllRooms.AddRange(RandoLogic.ProcessArea(area));
            }
        }

        public static AreaKey GenerateMap(int seed, bool repeatRooms) {
            var newArea = new AreaData {
                IntroType = Player.IntroTypes.WakeUp,
                Icon = AreaData.Areas[0].Icon,
                Interlude = false,
                Dreaming = false,
                ID = AreaData.Areas.Count,
                Name = seed.ToString(),
                Mode = new ModeProperties[3] {
                    new ModeProperties {
                        Inventory = PlayerInventory.Default,
                    }, null, null
                },
            };
            newArea.SetSID($"randomizer/{seed}");
            AreaData.Areas.Add(newArea);

            var key = new AreaKey(newArea.ID);

            var r = new RandoLogic(seed, repeatRooms, key);

            newArea.Wipe = r.PickWipe();
            newArea.Mode[0].AudioState = r.PickAudio();
            newArea.Mode[0].MapData = r.MakeMap();

            Logger.Log("randomizer", $"new area {newArea.GetSID()}");
            Logger.Log("randomizer", $"load seed {newArea.Mode[0].MapData.LoadSeed}");

            return key;
        }

        private Random Random;
        private bool RepeatRooms;
        private List<RandoRoom> RemainingRooms;
        private AreaKey Key;

        private RandoLogic(int seed, bool repeatRooms, AreaKey key) {
            this.Random = new Random(seed);
            this.RepeatRooms = repeatRooms;
            this.RemainingRooms = new List<RandoRoom>(RandoLogic.AllRooms);
            this.Key = key;
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

        private AudioState PickAudio() {
            switch (this.Random.Next(3)) {
                case 0:
                case 1:
                default:
                    return new AudioState("event:/music/lvl1/main", "event:/env/amb/01_main");
                case 2:
                    return new AudioState("event:/music/remix/01_forsaken_city", "event:/env/amb/01_main");
                    // TODO more music. music changes?
            }
        }

        private class QueueTuple {
            public LevelData Item1;
            public Hole Item2;

            public QueueTuple(LevelData Item1, Hole Item2) {
                this.Item1 = Item1;
                this.Item2 = Item2;
            }
        }

        private class ShuffleTuple {
            public RandoRoom Item1;
            public Hole Item2;
            public int Item3;

            public ShuffleTuple (RandoRoom Item1, Hole Item2, int Item3) {
                this.Item1 = Item1;
                this.Item2 = Item2;
                this.Item3 = Item3;
            }
        }

        private MapData MakeMap() {
            MapData result = new MapData(this.Key);
            result.Levels = new List<LevelData>();
            var queue = new List<QueueTuple>();

            void addLevel(RandoRoom rRoom, LevelData cRoom, Hole fromHole) {
                if (!this.RepeatRooms) {
                    this.RemainingRooms.Remove(rRoom);
                }

                result.Levels.Add(cRoom);
                foreach (Hole hole in rRoom.Holes) {
                    if (hole != fromHole && (hole.Kind == HoleKind.InOut || hole.Kind == HoleKind.Out)) {
                        queue.Add(new QueueTuple(cRoom, hole));
                    }
                }
            }

            RandoRoom startRoom = this.RemainingRooms[this.Random.Next(this.RemainingRooms.Count)];
            LevelData startLinked = startRoom.LinkStart();
            addLevel(startRoom, startLinked, null);

            while (queue.Count != 0) {
                var entry = queue[0];
                queue.RemoveAt(0);
                LevelData lvl = entry.Item1;
                Hole startHole = entry.Item2;
                var possibilities = new List<ShuffleTuple>();

                // quick check: is it impossible to attach anything to this hole?
                Hole transplant = new Hole(lvl, startHole.Side, startHole.LowBound, startHole.HighBound, startHole.HighOpen);
                Vector2 push = transplant.Side.Unit() * (transplant.Side == ScreenDirection.Up || transplant.Side == ScreenDirection.Down ? 180 : 320);
                Vector2 pt1 = transplant.LowCoord + push;
                Vector2 pt2 = transplant.HighCoord + push;

                if ((result.GetAt(pt1) ?? result.GetAt(pt2)) == null) {
                    foreach (RandoRoom prospect in this.RemainingRooms) {
                        foreach (Hole prospectHole in prospect.Holes) {
                            if (prospectHole.Kind == HoleKind.None || prospectHole.Kind == HoleKind.Out) {
                                continue;
                            }
                            int offset = startHole.Compatible(prospectHole);
                            if (offset != Hole.INCOMPATIBLE) {
                                possibilities.Add(new ShuffleTuple(prospect, prospectHole, offset));
                            }
                        }
                    }
                }

                possibilities.Shuffle(this.Random);

                bool foundWorking = false;
                LevelData cachedConflict = null;
                foreach (var prospectTuple in possibilities) {
                    RandoRoom prospect = prospectTuple.Item1;
                    Hole prospectHole = prospectTuple.Item2;
                    int offset = prospectTuple.Item3;

                    Rectangle newLvlRect = prospect.QuickLinkAdjacent(lvl, startHole.Side, offset);
                    bool foundConflict = false;

                    if (cachedConflict != null && cachedConflict.Bounds.Intersects(newLvlRect)) {
                        foundConflict = true;
                    } else {
                        foreach (LevelData checkLvl in result.Levels) {
                            if (checkLvl.Bounds.Intersects(newLvlRect)) {
                                cachedConflict = checkLvl;
                                foundConflict = true;
                                break;
                            }
                        }
                    }

                    if (!foundConflict) {
                        // it works!!!
                        Logger.Log("randomizer", $"Attached {lvl.Name} {startHole} to {prospect.Name} {prospectHole}");
                        addLevel(prospect, prospect.LinkAdjacent(lvl, startHole.Side, offset), prospectHole);
                        foundWorking = true;
                        break;
                    }
                }

                if (!foundWorking) {
                    Logger.Log("randomizer", $"Failed to attach room to {lvl.Name} {startHole}");
                }
            }

            SetMapBounds(result);
            return result;
        }

        private static void SetMapBounds(MapData result) {
            int num1 = int.MaxValue;
            int num2 = int.MaxValue;
            int num3 = int.MinValue;
            int num4 = int.MinValue;
            foreach (LevelData level in result.Levels) {
                if (level.Bounds.Left < num1)
                    num1 = level.Bounds.Left;
                if (level.Bounds.Top < num2)
                    num2 = level.Bounds.Top;
                if (level.Bounds.Right > num3)
                    num3 = level.Bounds.Right;
                if (level.Bounds.Bottom > num4)
                    num4 = level.Bounds.Bottom;
            }

            result.Bounds = new Rectangle(num1, num2, num3 - num1, num4 - num2);
        }

        private void Use(RandoRoom room) {

        }
    }
}
