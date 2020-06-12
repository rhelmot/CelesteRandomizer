using System;
using System.Collections.Generic;
using Monocle;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        private class TaskPathwayStart : RandoTask {
            private HashSet<StaticRoom> TriedRooms = new HashSet<StaticRoom>();

            public TaskPathwayStart(RandoLogic logic) : base(logic) {
            }

            private IEnumerable<StaticRoom> AvailableRooms() {
                foreach (var room in Logic.RemainingRooms) {
                    if (!TriedRooms.Contains(room)) {
                        yield return room;
                    }
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

            private IEnumerable<StaticEdge> AvailableEdges(bool hasKey=true) {
                var caps = this.Logic.Caps;
                if (!hasKey) {
                    caps = caps.WithoutKey();
                }
                foreach (var node in this.Node.Closure(caps, false, true)) {
                    foreach (var edge in node.UnlinkedEdges(caps, false)) {
                        if (!this.TriedEdges.Contains(edge) && this.Logic.Map.HoleFree(this.Node.Room, edge.HoleTarget)) {
                            yield return edge;
                        }
                    }
                }
            }

            public override bool Next() {
                var available = new List<StaticEdge>(this.AvailableEdges());
                if (available.Count == 0) {
                    Logger.Log("randomizer", $"Failure: No edges out of {Node.Room.Static.Name}:{Node.Static.Name}");
                    return false;
                }

                var picked = available[this.Logic.Random.Next(available.Count)];
                var needKey = false;
                if (!picked.ReqOut.Able(this.Logic.Caps.WithoutKey())) {
                    needKey = true;
                } else {
                    needKey = true;
                    foreach (var edge in this.AvailableEdges(false)) {
                        if (edge == picked) {
                            needKey = false;
                            break;
                        }
                    }
                }

                this.TriedEdges.Add(picked);
                // TODO what if we picked an edge from a node from a different room? can't do that yet but still
                // we should have a tuple-class
                this.AddNextTask(new TaskPathwayPickRoom(this.Logic, this.Node.Room.Nodes[picked.FromNode.Name], picked));
                if (needKey) {
                    Logger.Log("randomizer", $"Need to place a key from {Node.Room.Static.Name}:{Node.Static.Name} to get out of {picked.HoleTarget}");
                    this.AddNextTask(new TaskPathwayPlaceKey(this.Logic, this.Node));
                }
                return true;
            }
        }

        private class TaskPathwayPickRoom : RandoTask {
            private LinkedNode Node;
            private StaticEdge Edge;
            private HashSet<StaticRoom> TriedRooms = new HashSet<StaticRoom>();
            private bool IsEnd;

            private static readonly int[] Minimums = { 15, 30, 50, 80 };
            private static readonly int[] Ranges = { 15, 30, 30, 70 };

            public TaskPathwayPickRoom(RandoLogic Logic, LinkedNode node, StaticEdge edge) : base(Logic) {
                this.Node = node;
                this.Edge = edge;

                double progress = (double)(Logic.Map.Count - Minimums[(int)Logic.Settings.Length]) / (double)Ranges[(int)Logic.Settings.Length];
                this.IsEnd = progress > Math.Sqrt(Logic.Random.NextDouble());
            }

            // this function does the cheap checks that can be applied to every room/node/edge
            // in order to produce the list that gets shuffled
            private IEnumerable<StaticEdge> AvailableEdges() {
                foreach (var room in this.Logic.RemainingRooms) {
                    if (TriedRooms.Contains(room) || this.IsEnd != room.End) {
                        continue;
                    }

                    foreach (var node in room.Nodes.Values) {
                        foreach (var edge in node.Edges) {
                            yield return edge;
                        }
                    }
                }
            }

            // this function calls the previous function and does complicated checks
            // in order to produce a receipt for having made the connection
            private ConnectAndMapReceipt WorkingPossibility() {
                var caps = this.Logic.Caps.WithoutKey(); // don't try to enter a locked door
                var possibilities = new List<StaticEdge>(this.AvailableEdges());
                possibilities.Shuffle(this.Logic.Random);

                foreach (var edge in possibilities) {
                    if (!edge.ReqIn.Able(caps)) {
                        continue;
                    }

                    var result = ConnectAndMapReceipt.Do(this.Logic, this.Node.Room, this.Edge, edge);
                    if (result != null) {
                        return result;
                    }
                }

                return null;
            }

            public override bool Next() {
                var receipt = this.WorkingPossibility();
                if (receipt == null) {
                    Logger.Log("randomizer", $"Failure: could not find a room that fits on {Node.Room.Static.Name}:{Node.Static.Name}:{Edge.HoleTarget}");
                    return false;
                }

                this.AddReceipt(receipt);
                this.TriedRooms.Add(receipt.NewRoom.Static);
                if (!this.IsEnd) {
                    var newNode = receipt.Edge.OtherNode(this.Node);
                    this.AddNextTask(new TaskPathwayPickEdge(this.Logic, newNode));
                }

                return true;
            }
        }

        private class TaskPathwayPlaceKey : RandoTask {
            private LinkedNode Node;
            private int Tries;
            private bool InternalOnly;

            public TaskPathwayPlaceKey(RandoLogic logic, LinkedNode node, bool internalOnly=false, int tries=0) : base(logic) {
                // TODO: advance forward through any obligatory edges
                this.Node = node;
                this.InternalOnly = internalOnly;
                this.Tries = tries;
            }

            // this function does the cheap checks that can be applied to every room/node/edge
            // in order to produce the list that gets shuffled
            private IEnumerable<StaticEdge> AvailableNewEdges() {
                foreach (var room in this.Logic.RemainingRooms) {
                    if (room.End) {
                        continue;
                    }

                    foreach (var node in room.Nodes.Values) {
                        foreach (var edge in node.Edges) {
                            yield return edge;
                        }
                    }
                }
            }

            private IEnumerable<StaticEdge> AvailableOutEdges(LinkedNode node) {
                foreach (var edge in node.UnlinkedEdges(this.Logic.Caps.WithoutKey(), true)) {
                    if (this.Logic.Map.HoleFree(this.Node.Room, edge.HoleTarget)) {
                        yield return edge;
                    }
                }
            }

            // this function calls the previous function and does complicated checks
            // in order to produce a receipt for having made the connection
            private ConnectAndMapReceipt WorkingPossibility(LinkedNode node, StaticEdge fromEdge) {
                var possibilities = new List<StaticEdge>(this.AvailableNewEdges());
                possibilities.Shuffle(this.Logic.Random);
                var capsNoKey = this.Logic.Caps.WithoutKey();

                foreach (var edge in possibilities) {
                    if (!edge.ReqIn.Able(capsNoKey) || !edge.ReqOut.Able(capsNoKey)) {
                        continue;
                    }

                    var result = ConnectAndMapReceipt.Do(this.Logic, node.Room, fromEdge, edge);
                    if (result != null) {
                        return result;
                    }
                }

                return null;
            }

            public override bool Next() {
                if (this.Tries >= 5) {
                    Logger.Log("randomizer", $"Failure: took too many tries to place key from {Node.Room.Static.Name}:{Node.Static.Name}");
                    return false;
                }

                // each attempt we should back further away from the idea that we might add a new room
                bool extendingMap = this.Logic.Random.Next(5) > this.Tries + 1;
                this.Tries++;

                var closure = new List<LinkedNode>(this.Node.Closure(this.Logic.Caps.WithoutKey(), true, this.InternalOnly));
                closure.Shuffle(this.Logic.Random);

                if (!extendingMap) {
                    foreach (var node in closure) {
                        var cols = new List<StaticCollectable>(node.UnlinkedCollectables());
                        cols.Shuffle(this.Logic.Random);
                        foreach (var spot in cols) {
                            this.AddReceipt(PlaceCollectableReceipt.Do(node, spot, LinkedNode.LinkedCollectable.Key));
                            return true;
                        }
                    }
                }

                // if we can find somewhere to extend the map
                foreach (var node in closure) {
                    var outEdges = new List<StaticEdge>(this.AvailableOutEdges(node));
                    outEdges.Shuffle(this.Logic.Random);
                    foreach (var edge in outEdges) {
                        var mapped = this.WorkingPossibility(node, edge);
                        if (mapped != null) {
                            this.AddReceipt(mapped);
                            this.AddNextTask(new TaskPathwayPlaceKey(this.Logic, mapped.EntryNode, true, this.Tries));
                            return true;
                        }
                    }
                }

                // try again
                return this.Next();
            }
        }
    }
}
