using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        public static AreaKey GenerateMap(RandoSettings settings) {
            var newArea = new AreaData {
                IntroType = Player.IntroTypes.WakeUp,
                Interlude = false,
                Dreaming = false,
                ID = AreaData.Areas.Count,
                Name = settings.Seed.ToString(),
                Mode = new ModeProperties[3] {
                    new ModeProperties {
                        Inventory = PlayerInventory.TheSummit,
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
            newArea.SetSID($"randomizer/{settings.Seed}");
            AreaData.Areas.Add(newArea);

            var key = new AreaKey(newArea.ID);

            var r = new RandoLogic(settings, key);

            newArea.Wipe = r.PickWipe();
            newArea.Mode[0].AudioState = r.PickAudio();
            newArea.Mode[0].MapData = r.MakeMap();

            Logger.Log("randomizer", $"new area {newArea.GetSID()}");

            return key;
        }

        private Random Random;
        private List<RandoRoom> RemainingRooms;
        private AreaKey Key;
        private MapData Map;
        private RandoSettings Settings;
        private int? Nonce;
        private Deque<RandoTask> Tasks = new Deque<RandoTask>();
        private Stack<RandoTask> CompletedTasks = new Stack<RandoTask>();

        private int? NextNonce {
            get {
                return this.Nonce == null ? null : this.Nonce++;
            }
        }

        private RandoLogic(RandoSettings settings, AreaKey key) {
            this.Random = new Random(settings.Seed);
            this.Settings = settings;
            this.RemainingRooms = new List<RandoRoom>();
            foreach (var room in RandoLogic.AllRooms) {
                if (settings.MapIncluded(room.Area)) {
                    this.RemainingRooms.Add(room);
                }
            }
            this.Key = key;

            if (this.Settings.RepeatRooms) {
                this.Nonce = 0;
            }
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
            this.Map = new MapData(this.Key);
            this.Map.Levels = new List<LevelData>();

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

            this.SetMapBounds();
            return this.Map;
        }

        private void SetMapBounds() {
            int num1 = int.MaxValue;
            int num2 = int.MaxValue;
            int num3 = int.MinValue;
            int num4 = int.MinValue;
            foreach (LevelData level in this.Map.Levels) {
                if (level.Bounds.Left < num1)
                    num1 = level.Bounds.Left;
                if (level.Bounds.Top < num2)
                    num2 = level.Bounds.Top;
                if (level.Bounds.Right > num3)
                    num3 = level.Bounds.Right;
                if (level.Bounds.Bottom > num4)
                    num4 = level.Bounds.Bottom;
            }

            this.Map.Bounds = new Rectangle(num1, num2, num3 - num1, num4 - num2);
        }
    }
}
