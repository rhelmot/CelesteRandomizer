using System;
using System.Collections.Generic;
using Monocle;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        private class TaskLabyrinthStart : RandoTask {
            private HashSet<StaticRoom> TriedRooms = new HashSet<StaticRoom>();

            public TaskLabyrinthStart(RandoLogic logic) : base(logic) {
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

                this.AddLastTask(new TaskLabyrinthFinish(this.Logic));

                var closure = LinkedNodeSet.Closure(receipt.NewRoom.Nodes["main"], this.Logic.Caps, this.Logic.Caps, true);
                var node = receipt.NewRoom.Nodes["main"];
                foreach (var edge in closure.UnlinkedEdges()) {
                    this.AddNextTask(new TaskLabyrinthContinue(this.Logic, edge));
                }
                return true;
            }
        }

        private class TaskLabyrinthContinue : RandoTask {
            private UnlinkedEdge Edge;

            private HashSet<StaticEdge> TriedEdges = new HashSet<StaticEdge>();

            public TaskLabyrinthContinue(RandoLogic logic, UnlinkedEdge edge) : base(logic) {
                this.Edge = edge;
            }

            private IEnumerable<StaticEdge> AvailableEdges() {
                foreach (var room in this.Logic.RemainingRooms) {
                    foreach (var node in room.Nodes.Values) {
                        foreach (var edge in node.Edges) {
                            if (edge.HoleTarget == null || TriedEdges.Contains(edge)) {
                                continue;
                            }

                            yield return edge;
                        }
                    }
                }
            }

            private ConnectAndMapReceipt WorkingPossibility() {
                var possibilities = new List<StaticEdge>(this.AvailableEdges());
                possibilities.Shuffle(this.Logic.Random);

                foreach (var edge in possibilities) {
                    if (!edge.ReqIn.Able(this.Logic.Caps) || !edge.ReqOut.Able(this.Logic.Caps)) {
                        continue;
                    }

                    var result = ConnectAndMapReceipt.Do(this.Logic, this.Edge, edge);
                    if (result != null) {
                        return result;
                    }
                }

                return null;
            }

            public override bool Next() {
                if (this.TriedEdges.Count > 5) {
                    return false;
                }

                var receipt = this.WorkingPossibility();
                if (receipt == null) {
                    return true; // never fail!
                }

                this.AddReceipt(receipt);
                this.TriedEdges.Add(receipt.Edge.CorrespondingEdge(this.Edge.Node));
                var targetNode = receipt.Edge.OtherNode(this.Edge.Node);
                var closure = LinkedNodeSet.Closure(targetNode, this.Logic.Caps, this.Logic.Caps, true);

                foreach (var newedge in closure.UnlinkedEdges()) {
                    this.AddNextTask(new TaskLabyrinthContinue(this.Logic, newedge));
                }
                return true;
            }
        }

        private class TaskLabyrinthFinish : RandoTask {
            public TaskLabyrinthFinish(RandoLogic logic) : base(logic) {
            }

            public override bool Next() {
                if (Logic.Map.Count < 10) {
                    return false;
                }
                return true;
            }
        }
    }
}
