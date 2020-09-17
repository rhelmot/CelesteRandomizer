﻿using System;
using System.Linq;
using System.Collections.Generic;
using Monocle;

namespace Celeste.Mod.Randomizer {
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
                this.AddNextTask(new TaskPathwayPickEdge(this.Logic, receipt.NewRoom.Nodes["main"]));

                return true;
            }
        }

        private class TaskPathwayPickEdge : RandoTask {
            private LinkedNode Node;
            private HashSet<StaticEdge> TriedEdges = new HashSet<StaticEdge>();

            public TaskPathwayPickEdge(RandoLogic logic, LinkedNode node) : base(logic) {
                // TODO: advance forward through any obligatory edges
                this.Node = node;
            }

            public override bool Next() {
                var closure = LinkedNodeSet.Closure(this.Node, this.Logic.Caps, null, true);
                var available = closure.UnlinkedEdges((UnlinkedEdge u) => !this.TriedEdges.Contains(u.Static) && (u.Static.HoleTarget == null || this.Logic.Map.HoleFree(this.Node.Room, u.Static.HoleTarget)));
                if (available.Count == 0) {
                    Logger.Log("randomizer", $"Failure: No edges out of {Node.Room.Static.Name}:{Node.Static.Name}");
                    return false;
                }

                var picked = available[this.Logic.Random.Next(available.Count)];
                this.AddNextTask(new TaskPathwayPickRoom(this.Logic, picked));
                this.TriedEdges.Add(picked.Static);

                var reqNeeded = LinkedNodeSet.TraversalRequires(this.Node, this.Logic.Caps.WithoutKey(), true, picked);
                if (reqNeeded is Possible) {
                    // nothing needed
                } else if (reqNeeded is KeyRequirement keyReq) {
                    Logger.Log("randomizer", $"Need to place a key from {Node.Room.Static.Name}:{Node.Static.Name} to get out of {picked.Static.HoleTarget}");
                    this.AddNextTask(new TaskPathwayPlaceKey(this.Logic, this.Node, keyReq.KeyholeID));
                } else {
                    throw new Exception("why does this happen? this should not happen");
                }

                return true;
            }
        }

        private class TaskPathwayPickRoom : RandoTask {
            private UnlinkedEdge Edge;
            private HashSet<StaticRoom> TriedRooms = new HashSet<StaticRoom>();
            private bool IsEnd;

            public TaskPathwayPickRoom(RandoLogic Logic, UnlinkedEdge edge) : base(Logic) {
                this.Edge = edge;

                float progress = (Logic.Map.Worth - PathwayMinimums[(int)Logic.Settings.Length]) / PathwayRanges[(int)Logic.Settings.Length];
                this.IsEnd = progress > Math.Sqrt(Logic.Random.NextFloat());
            }

            private ConnectAndMapReceipt WorkingPossibility() {
                var caps = this.Logic.Caps.WithoutKey(); // don't try to enter a door locked from the other side
                var possibilities = this.Logic.AvailableNewEdges(caps, null, e => RoomFilter(e.FromNode.ParentRoom));

                if (possibilities.Count == 0 && this.IsEnd) {
                    throw new GenerationError("No ending rooms available");
                }

                foreach (var edge in possibilities) {
                    var result = ConnectAndMapReceipt.Do(this.Logic, this.Edge, edge);
                    if (result != null) {
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
                    this.AddNextTask(new TaskPathwayPickEdge(this.Logic, newNode));
                }

                return true;
            }
        }

        private class TaskPathwayPlaceKey : RandoTask {
            private LinkedNode Node;
            private int Tries;
            private int KeyholeID;
            private LinkedNode OriginalNode;
            private bool InternalOnly;

            public TaskPathwayPlaceKey(RandoLogic logic, LinkedNode node, int keyholeID, LinkedNode originalNode=null, bool internalOnly=false, int tries=0) : base(logic) {
                // TODO: advance forward through any obligatory edges
                this.Node = node;
                this.InternalOnly = internalOnly;
                this.KeyholeID = keyholeID;
                this.OriginalNode = originalNode ?? node;
                this.Tries = tries;
            }

            public override bool Next() {
                if (this.Tries >= 5) {
                    Logger.Log("randomizer", $"Failure: took too many tries to place key from {Node.Room.Static.Name}:{Node.Static.Name}");
                    return false;
                }

                // each attempt we should back further away from the idea that we might add a new room
                int roll = this.Logic.Random.Next(5);
                bool extendingMap = roll > this.Tries;
                this.Tries++;

                var caps = this.Logic.Caps.WithoutKey();
                int maxSteps = 99999;
                if (!InternalOnly) {
                    maxSteps = this.Logic.Random.Next(6, 20);
                }
                var closure = LinkedNodeSet.Closure(this.Node, caps, caps, this.InternalOnly, maxSteps);
                closure.Shuffle(this.Logic.Random);

                if (!extendingMap) {
                    // just try to place a key
                    foreach (var spot in closure.UnlinkedCollectables()) {
                        if (spot.Static.MustFly) {
                            continue;
                        }
                        if (spot.Node.Room == this.OriginalNode.Room) {
                            // don't be boring!
                            continue;
                        }
                        this.AddReceipt(PlaceCollectableReceipt.Do(spot.Node, spot.Static, LinkedNode.LinkedCollectable.Key, false));
                        this.OriginalNode.Room.UsedKeyholes.Add(this.KeyholeID);
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
                        this.AddReceipt(PlaceCollectableReceipt.Do(spot.Node, spot.Static, LinkedNode.LinkedCollectable.Key, true));
                        this.OriginalNode.Room.UsedKeyholes.Add(this.KeyholeID);
                        return true;
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
                        this.AddNextTask(new TaskPathwayPlaceKey(this.Logic, mapped.EntryNode, this.KeyholeID, this.OriginalNode, true, this.Tries));
                        return true;
                    }
                }

                // try again
                return this.Next();
            }

            public override void Undo() {
                base.Undo();
                this.OriginalNode.Room.UsedKeyholes.Remove(this.KeyholeID);
            }
        }
    }
}
