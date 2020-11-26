using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer {
    using FlagSet = Dictionary<string, FlagState>;
    
    
    public partial class RandoLogic {
        private Deque<RandoTask> Tasks = new Deque<RandoTask>();
        private Stack<RandoTask> CompletedTasks = new Stack<RandoTask>();

        private static readonly float[] PathwayMinimums = { 40, 80, 120, 180 };
        private static readonly float[] PathwayRanges = { 15, 30, 40, 80 };
        private static readonly float[] PathwayMaxRoom = { 6, 15, 10000, 10000, 10000 };
        private static readonly int[] MaxBacktracks = {100, 200, 500, 1000};

        private void GeneratePathway() {
            this.Tasks.AddToFront(new TaskPathwayStart(this));
            int backtracks = 0;

            while (this.Tasks.Count != 0) {
                var nextTask = this.Tasks.RemoveFromFront();
                nextTask.Reset();

                while (!nextTask.Next()) {
                    backtracks++;
                    if (backtracks > MaxBacktracks[(int) this.Settings.Length]) {
                        throw new RetryException();
                    }
                    if (this.CompletedTasks.Count == 0) {
                        throw new GenerationError("Could not generate map");
                    }

                    this.Tasks.AddToFront(nextTask);
                    nextTask = this.CompletedTasks.Pop();
                    nextTask.Undo();
                }

                this.CompletedTasks.Push(nextTask);
            }
        }
        private class TaskPathwayStart : RandoTask {
            private HashSet<StaticRoom> TriedRooms = new HashSet<StaticRoom>();

            public TaskPathwayStart(RandoLogic logic) : base(logic) {
            }

            private IEnumerable<StaticRoom> AvailableRooms() {
                foreach (var room in Logic.RemainingRooms) {
                    if (TriedRooms.Contains(room)) {
                        continue;
                    }
                    if (room.Worth > PathwayMaxRoom[(int)Logic.Settings.Length]) {
                        continue;
                    }
                    if (!(room.ReqEnd is Impossible)) {
                        continue;
                    }
                    yield return room;
                }
            }

            private StartRoomReceipt WorkingPossibility() {
                var available = new List<StaticRoom>(AvailableRooms());

                if (available.Count == 0) {
                    return null;
                }

                var picked = available[this.Logic.Random.Next(available.Count)];
                return StartRoomReceipt.Do(this.Logic, picked);
            }

            public override bool Next() {
                var receipt = this.WorkingPossibility();
                if (receipt == null) {
                    return false;
                }

                this.TriedRooms.Add(receipt.NewRoom.Static);
                this.AddReceipt(receipt);
                var newNode = receipt.NewRoom.Nodes["main"];
                var state = new FlagSet();
                foreach (var node in LinkedNodeSet.Closure(newNode, this.Logic.Caps.WithFlags(state), null, true).Nodes) {
                    foreach (var setter in node.Static.FlagSetters) {
                        TaskPathwayPickRoom.UpdateState(state, setter.Item1, setter.Item2);
                        newNode = node;
                    }
                }
                this.AddNextTask(new TaskPathwayPickEdge(this.Logic, newNode, state));
                this.AddLastTask(new TaskPathwayBerryOffshoot(this.Logic, newNode, state));

                return true;
            }
        }

        private class TaskPathwayPickEdge : RandoTask {
            private LinkedNode Node;
            private HashSet<StaticEdge> TriedEdges = new HashSet<StaticEdge>();
            private FlagSet State;

            public TaskPathwayPickEdge(RandoLogic logic, LinkedNode node, FlagSet state) : base(logic) {
                // TODO: advance forward through any obligatory edges
                this.Node = node;
                this.State = state;
            }

            public override bool Next() {
                var caps = this.Logic.Caps.WithoutFlags();
                var closure = LinkedNodeSet.Closure(this.Node, caps, null, true);
                var available = closure.UnlinkedEdges(u => !this.TriedEdges.Contains(u.Static) && (u.Static.HoleTarget == null || this.Logic.Map.HoleFree(this.Node.Room, u.Static.HoleTarget)));
                if (available.Count == 0) {
                    Logger.Log("randomizer", $"Failure: No edges out of {Node.Room.Static.Name}:{Node.Static.Name}");
                    return false;
                }

                // stochastic difficulty control
                var picked = available[this.Logic.Random.Next(available.Count)];
                var caps2 = caps.Copy();
                for (int i = 0; ; i++, picked = available[this.Logic.Random.Next(available.Count)]){
                    if (this.Logic.Settings.DifficultyEagerness == DifficultyEagerness.None || this.Logic.Settings.Difficulty == Difficulty.Easy) {
                        break;
                    }
                    if (i == 4) {
                        return false;
                    }
                    
                    // pick a lower difficulty level - if we have a higher eagerness it should be more likely we pick a difficulty closer to the current one
                    // to facilitate this we pick a [0,1] sample which biases toward zero as eagerness increases
                    var sample = this.Logic.Random.NextDouble();
                    switch (this.Logic.Settings.DifficultyEagerness) {
                        case DifficultyEagerness.Medium:
                            sample = Math.Pow(sample, 4);
                            break;
                        case DifficultyEagerness.High:
                            sample = Math.Pow(sample, 8);
                            break;
                    }

                    // the off-by-ones here are devious. we want to select a number of steps down which will always step at least once and may step down to one step below easy.
                    sample *= (int) this.Logic.Settings.Difficulty + 1;
                    //Logger.Log("DEBUG", $"Permissiveness sample: {(int) sample} / {(int) this.Logic.Settings.Difficulty}");
                    caps2.PlayerSkill = this.Logic.Settings.Difficulty - ((int) sample + 1);
                    if (caps2.PlayerSkill < 0) {
                        break;
                    }

                    // if the target edge is NOT present in the closure with the lower difficulty, it is hard enough. otherwise, it is too easy.
                    if (!LinkedNodeSet.Closure(this.Node, caps2, null, true).UnlinkedEdges().Contains(picked)) {
                        break;
                    }
                    //Logger.Log("DEBUG", "...rejecting edge, too easy");
                }
                    
                var state = new FlagSet(this.State);
                this.AddNextTask(new TaskPathwayPickRoom(this.Logic, picked, state));
                this.TriedEdges.Add(picked.Static);

                var reqNeeded = LinkedNodeSet.TraversalRequires(this.Node, this.Logic.Caps.WithoutKey().WithFlags(this.State), true, picked);
                this.HandleRequirements(reqNeeded, state, this.State);
                var reversible = LinkedNodeSet.Closure(this.Node, null, this.Logic.Caps.WithFlags(state), true).UnlinkedEdges().Contains(picked);
                if (!reversible) {
                    CrystallizeState(state);
                }
                // WE NEED TO DO TWO THINGS
                // 1) Update the flags state in the upcoming pickroom task based on the flags we decide we need to handle. CHECK
                // 2) check if this is traversable in reverse. if it's not, update the flags in the upcoming pickroom task. CHECK
                // 3) ummm forgot about this. if we happen to place a room with a switch reachable, update the state
                // BONUS 4) in the requirement satisfying task, add a reachability check to get back to startnode with the new flag set
                return true;
            }

            private void HandleRequirements(Requirement r, FlagSet mutState, FlagSet immState) {
                switch (r) {
                    case Possible _:
                        return;
                    case Conjunction rc: {
                        foreach (var sr in rc.Children) {
                            this.HandleRequirements(sr, mutState, immState);
                        }
                        break;
                    }
                    case Disjunction rd: {
                        // maybe this could be handled by having a TaskSatisfyDisjunction which tries to add the children one at a time?
                        var sr = rd.Children[this.Logic.Random.Next(rd.Children.Count)];
                        this.HandleRequirements(sr, mutState, immState);
                        break;
                    }
                    default: {
                        if (r is FlagRequirement fr) {
                            mutState[fr.Flag] = fr.Set ? FlagState.Set : FlagState.Unset;
                        }
                        this.AddNextTask(new TaskPathwaySatisfyRequirement(this.Logic, this.Node, r, immState));
                        break;
                    }
                }
            }

            private static void CrystallizeState(FlagSet state) {
                foreach (var kv in new FlagSet(state)) {
                    switch (kv.Value) {
                        case FlagState.Both:
                        case FlagState.SetToUnset:
                        case FlagState.UnsetToSet:
                            state[kv.Key] = FlagState.One;
                            break;
                    }
                }
            }
        }

        private class TaskPathwayPickRoom : RandoTask {
            private UnlinkedEdge Edge;
            private HashSet<StaticRoom> TriedRooms = new HashSet<StaticRoom>();
            private bool IsEnd;
            private FlagSet State;

            public TaskPathwayPickRoom(RandoLogic Logic, UnlinkedEdge edge, FlagSet state) : base(Logic) {
                this.Edge = edge;
                this.State = state;

                float progress = (Logic.Map.Worth - PathwayMinimums[(int)Logic.Settings.Length]) / PathwayRanges[(int)Logic.Settings.Length];
                this.IsEnd = progress > Math.Sqrt(Logic.Random.NextFloat());
            }

            private ConnectAndMapReceipt WorkingPossibility() {
                var caps = this.Logic.Caps.WithFlags(this.State).WithoutKey(); // don't try to enter a door locked from the other side
                var possibilities = this.Logic.AvailableNewEdges(caps, null, e => RoomFilter(e.FromNode.ParentRoom));

                if (possibilities.Count == 0 && this.IsEnd) {
                    throw new GenerationError("No ending rooms available");
                }

                foreach (var edge in possibilities) {
                    var result = ConnectAndMapReceipt.Do(this.Logic, this.Edge, edge);
                    if (result != null) {
                        var defaultBerry = this.Logic.Settings.Algorithm == LogicType.Endless ? LinkedCollectable.LifeBerry : LinkedCollectable.Strawberry;
                        var closure = LinkedNodeSet.Closure(result.EntryNode, caps, caps, true);
                        var seen = new HashSet<UnlinkedCollectable>();
                        foreach (var spot in closure.UnlinkedCollectables()) {
                            seen.Add(spot);
                            if (!spot.Static.MustFly && this.Logic.Random.Next(5) == 0) {
                                spot.Node.Collectables[spot.Static] = Tuple.Create(defaultBerry, false);
                            }
                        }

                        closure.Extend(caps, null, true);
                        foreach (var spot in closure.UnlinkedCollectables()) {
                            if (!seen.Contains(spot) && !spot.Static.MustFly && this.Logic.Random.Next(10) == 0) {
                                spot.Node.Collectables[spot.Static] = Tuple.Create(defaultBerry, true);
                            }
                        }
                        
                        return result;
                    }
                }

                return null;
            }

            private bool RoomFilter(StaticRoom room) {
                return !this.TriedRooms.Contains(room) && 
                    (this.IsEnd ? room.ReqEnd.Able(this.Logic.Caps) : room.ReqEnd is Impossible) && 
                    room.Worth <= PathwayMaxRoom[(int)Logic.Settings.Length + (this.IsEnd ? 1 : 0)];
            }
            

            private ConnectAndMapReceipt WorkingWarpPossibility() {
                var allrooms = new List<StaticRoom>(this.Logic.RemainingRooms.Where(RoomFilter));
                allrooms.Shuffle(this.Logic.Random);

                foreach (var room in allrooms) {
                    var result = ConnectAndMapReceipt.DoWarp(this.Logic, this.Edge, room);
                    if (result != null) {
                        return result;
                    }
                }
                
                return null;
            }

            public override bool Next() {
                var receipt = this.Edge.Static.CustomWarp ? this.WorkingWarpPossibility() : this.WorkingPossibility();
                if (receipt == null) {
                    Logger.Log("randomizer", $"Failure: could not find a room that fits on {Edge.Node.Room.Static.Name}:{Edge.Node.Static.Name}:{Edge.Static.HoleTarget}");
                    return false;
                }

                this.AddReceipt(receipt);
                this.TriedRooms.Add(receipt.NewRoom.Static);
                if (!this.IsEnd) {
                    var newNode = receipt.Edge.OtherNode(this.Edge.Node);
                    var state = new FlagSet(this.State);
                    foreach (var node in LinkedNodeSet.Closure(newNode, this.Logic.Caps.WithFlags(this.State), null, true).Nodes) {
                        foreach (var setter in node.Static.FlagSetters) {
                            UpdateState(state, setter.Item1, setter.Item2);
                            newNode = node;
                        }
                    }
                    this.AddNextTask(new TaskPathwayPickEdge(this.Logic, newNode, state));
                    this.AddLastTask(new TaskPathwayBerryOffshoot(this.Logic, newNode, state));
                }

                return true;
            }

            public static void UpdateState(FlagSet state, string name, bool set) {
                var orig = state.TryGetValue(name, out var x) ? x : FlagState.Unset;
                if (set) {
                    if (orig == FlagState.One || orig == FlagState.Unset) {
                        state[name] = FlagState.UnsetToSet;
                    } else if (orig == FlagState.SetToUnset) {
                        state[name] = FlagState.Both;
                    }
                }
                else {
                    if (orig == FlagState.One || orig == FlagState.Set) {
                        state[name] = FlagState.SetToUnset;
                    } else if (orig == FlagState.UnsetToSet) {
                        state[name] = FlagState.Both;
                    }
                }
            }
        }

        private class TaskPathwaySatisfyRequirement : RandoTask {
            private LinkedNode Node;
            private int BaseTries;
            private int Tries;
            private Requirement Req;
            private LinkedNode OriginalNode;
            private bool InternalOnly;
            private FlagSet State;

            public TaskPathwaySatisfyRequirement(RandoLogic logic, LinkedNode node, Requirement req, FlagSet state, LinkedNode originalNode=null, bool internalOnly=false, int tries=0) : base(logic) {
                this.Node = node;
                this.InternalOnly = internalOnly;
                this.Req = req;
                this.OriginalNode = originalNode ?? node;
                this.Tries = this.BaseTries = tries;
                this.State = state;
            }
            
            public override void Reset() {
                this.Tries = this.BaseTries;
            }

            public override bool Next() {
                if (this.Tries >= 5) {
                    Logger.Log("randomizer", $"Failure: took too many tries to satisfy {this.Req} from {Node.Room.Static.Name}:{Node.Static.Name}");
                    return false;
                }

                // each attempt we should back further away from the idea that we might add a new room
                int roll = this.Logic.Random.Next(5);
                bool extendingMap = roll > this.Tries;
                this.Tries++;

                var caps = this.Logic.Caps.WithoutKey().WithFlags(this.State);
                int maxSteps = 99999;
                if (!InternalOnly) {
                    maxSteps = this.Logic.Random.Next(1, 20);
                }
                var closure = LinkedNodeSet.Closure(this.Node, caps, caps, this.InternalOnly, maxSteps);
                closure.Shuffle(this.Logic.Random);

                if (this.Req is FlagRequirement fr) {
                    var curval = this.State.TryGetValue(fr.Flag, out var x) ? x : FlagState.Unset;
                    if (fr.Set && (curval == FlagState.Both || curval == FlagState.Set || curval == FlagState.UnsetToSet)) {
                        return true;
                    }
                    if (!fr.Set && (curval == FlagState.Both || curval == FlagState.Unset || curval == FlagState.SetToUnset)) {
                        return true;
                    }
                }

                if (!extendingMap) {
                    switch (this.Req) {
                        case KeyRequirement keyReq: {
                            // just try to place a key
                            foreach (var spot in closure.UnlinkedCollectables()) {
                                if (spot.Static.MustFly) {
                                    continue;
                                }
                                if (spot.Node.Room == this.OriginalNode.Room) {
                                    // don't be boring!
                                    continue;
                                }
                                this.AddReceipt(PlaceCollectableReceipt.Do(spot.Node, spot.Static, LinkedCollectable.Key, false, keyReq.KeyholeID, this.OriginalNode.Room));
                                return true;
                            }

                            // try again for things that we need to bubble back from
                            var newClosure = new LinkedNodeSet(closure);
                            newClosure.Extend(caps, null, true);
                            foreach (var spot in newClosure.UnlinkedCollectables()) {
                                if (spot.Static.MustFly) {
                                    continue;
                                }
                                if (spot.Node.Room == this.OriginalNode.Room) {
                                    // don't be boring!
                                    continue;
                                }
                                this.AddReceipt(PlaceCollectableReceipt.Do(spot.Node, spot.Static, LinkedCollectable.Key, true, keyReq.KeyholeID, this.OriginalNode.Room));
                                return true;
                            }

                            // third try: place a room which has a reachable spot
                            var appropriateNodes = this.Logic.RemainingRooms
                                .SelectMany(r => r.Nodes.Values)
                                .Where(n => n.Collectables.Count(c => !c.MustFly) != 0)
                                .ToList();
                            appropriateNodes.Shuffle(this.Logic.Random);
                            foreach (var n in appropriateNodes) {
                                // hack: make a linkednode for each staticnode so that the closure methods can work on it...
                                var edges = LinkedNodeSet
                                    .Closure(new LinkedRoom(n.ParentRoom, Vector2.Zero).Nodes[n.Name], caps, caps, true)
                                    .Shuffle(this.Logic.Random)
                                    .UnlinkedEdges()
                                    .Select(e => e.Static);
                                foreach (var edge in edges) {
                                    foreach (var startEdge in closure.UnlinkedEdges()) {
                                        var receipt = ConnectAndMapReceipt.Do(this.Logic, startEdge, edge, true);
                                        if (receipt != null) {
                                            this.AddReceipt(receipt);
                                            var cols = n.Collectables.Where(c => !c.MustFly).ToList();
                                            cols.Shuffle(this.Logic.Random);
                                            this.AddReceipt(PlaceCollectableReceipt.Do(receipt.NewRoom.Nodes[n.Name], cols[this.Logic.Random.Next(cols.Count)], LinkedCollectable.Key, false, keyReq.KeyholeID, this.OriginalNode.Room));
                                            return true;
                                        }
                                    }
                                }
                            }

                            break;
                        }
                        case FlagRequirement flagReq: {
                            var appropriateNodes = this.Logic.RemainingRooms
                                .SelectMany(r => r.Nodes.Values)
                                .Where(n => n.FlagSetters.Any(s => s.Item1 == flagReq.Flag && s.Item2 == flagReq.Set))
                                .ToList();
                            appropriateNodes.Shuffle(this.Logic.Random);
                            foreach (var n in appropriateNodes) {
                                // hack: make a linkednode for each staticnode so that the closure methods can work on it...
                                var edges = LinkedNodeSet
                                    .Closure(new LinkedRoom(n.ParentRoom, Vector2.Zero).Nodes[n.Name], caps, caps, true)
                                    .Shuffle(this.Logic.Random)
                                    .UnlinkedEdges()
                                    .Select(e => e.Static);
                                foreach (var edge in edges) {
                                    foreach (var startEdge in closure.UnlinkedEdges()) {
                                        var receipt = ConnectAndMapReceipt.Do(this.Logic, startEdge, edge, true);
                                        if (receipt != null) {
                                            this.AddReceipt(receipt);
                                            // TODO: check for reverse traversability back to orig node
                                            return true;
                                        }
                                    }
                                }
                            }
                            break;
                        }
                        default: {
                            throw new Exception($"Don't know how to satisfy {this.Req}. What?");
                        }
                    }
                }

                // see if we can find somewhere to extend the map
                foreach (var outEdge in closure.UnlinkedEdges()) {
                    if (!this.Logic.Map.HoleFree(outEdge.Node.Room, outEdge.Static.HoleTarget)) {
                        continue;
                    }
                    foreach (var toEdge in this.Logic.AvailableNewEdges(caps, caps, e => e.FromNode.ParentRoom.ReqEnd is Impossible)) {
                        var mapped = ConnectAndMapReceipt.Do(this.Logic, outEdge, toEdge, isBacktrack: true);
                        if (mapped == null) {
                            continue;
                        }

                        this.AddReceipt(mapped);
                        this.AddNextTask(new TaskPathwaySatisfyRequirement(this.Logic, mapped.EntryNode, this.Req, this.State, this.OriginalNode, true, this.Tries));
                        return true;
                    }
                }

                // if we failed to both extend the map or place the capability at the same time, we're fucked!
                if (!extendingMap) {
                    return false;
                }

                // try again
                return this.Next();
            }
        }

        private class TaskPathwayBerryOffshoot : RandoTask {
            public LinkedNode Node;
            private FlagSet State;
            
            public TaskPathwayBerryOffshoot(RandoLogic logic, LinkedNode node, FlagSet state) : base(logic) {
                this.Node = node;
                this.State = state;
            }
            public override bool Next() {
                var caps = this.Logic.Caps.WithFlags(this.State).WithoutKey();
                var closure = LinkedNodeSet.Closure(this.Node, caps, caps, true);
                foreach (var edge in closure.UnlinkedEdges()) {
                    if (!this.Logic.Map.HoleFree(this.Node.Room, edge.Static.HoleTarget)) {
                        continue;
                    }
                    if (this.Logic.Random.Next(5) != 0) {
                        continue;
                    }

                    var possibilities = this.Logic.AvailableNewEdges(caps, caps, e => e.FromNode.ParentRoom.Collectables.Count != 0);
                    foreach (var newEdge in possibilities) {
                        var receipt = ConnectAndMapReceipt.Do(this.Logic, edge, newEdge);
                        if (receipt == null) {
                            continue;
                        }

                        var closure2 = LinkedNodeSet.Closure(receipt.EntryNode, caps, caps, true);
                        var seen = new HashSet<UnlinkedCollectable>();
                        var options = new List<Tuple<UnlinkedCollectable, bool>>();
                        foreach (var spot in closure2.UnlinkedCollectables()) {
                            seen.Add(spot);
                            options.Add(Tuple.Create(spot, false));
                        }

                        closure2.Extend(caps, null, true);
                        foreach (var spot in closure2.UnlinkedCollectables()) {
                            if (seen.Contains(spot)) {
                                continue;
                            }
                            options.Add(Tuple.Create(spot, true));
                        }

                        if (options.Count == 0) {
                            receipt.Undo();
                            continue;
                        }

                        var pickedSpotTup = options[this.Logic.Random.Next(options.Count)];
                        var pickedSpot = pickedSpotTup.Item1;
                        var berry = pickedSpot.Static.MustFly ? LinkedCollectable.WingedStrawberry : this.Logic.Settings.Algorithm == LogicType.Endless ? LinkedCollectable.LifeBerry : LinkedCollectable.Strawberry;
                        pickedSpot.Node.Collectables[pickedSpot.Static] = Tuple.Create(berry, pickedSpotTup.Item2);
                        break;
                    }
                }

                return true;
            }
        }
    }
}
