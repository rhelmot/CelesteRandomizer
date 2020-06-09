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

            private IEnumerable<StaticEdge> AvailableEdges() {
                foreach (var node in this.Node.Closure(this.Logic.Caps, false, true)) {
                    foreach (var edge in node.UnlinkedEdges(this.Logic.Caps, false)) {
                        if (!this.TriedEdges.Contains(edge) && this.Logic.Map.HoleFree(this.Node.Room, edge.HoleTarget)) {
                            yield return edge;
                        }
                    }
                }
            }

            public override bool Next() {
                var available = new List<StaticEdge>(this.AvailableEdges());
                if (available.Count == 0) {
                    return false;
                }

                var picked = available[this.Logic.Random.Next(available.Count)];
                this.TriedEdges.Add(picked);
                // TODO what if we picked an edge from a node from a different room? can't do that yet but still
                // we should have a tuple-class
                this.AddNextTask(new TaskPathwayPickRoom(this.Logic, this.Node.Room.Nodes[picked.FromNode.Name], picked));
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
                var possibilities = new List<StaticEdge>(this.AvailableEdges());
                possibilities.Shuffle(this.Logic.Random);

                foreach (var edge in possibilities) {
                    if (!edge.ReqIn.Able(this.Logic.Caps)) {
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
    }
}
