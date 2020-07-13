using System;
using System.Collections.Generic;
using Monocle;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {

        private static readonly int[] LabyrinthMinimums = { 30, 50, 80, 120 };
        private static readonly int[] LabyrinthMaximums = { 50, 80, 120, 1000 };

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
            private int Goodwill;

            private HashSet<StaticEdge> TriedEdges = new HashSet<StaticEdge>();

            public TaskLabyrinthContinue(RandoLogic logic, UnlinkedEdge edge, int goodwill=5) : base(logic) {
                this.Edge = edge;
                this.Goodwill = goodwill;
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
                if (this.Goodwill <= 0) {
                    return false;
                }

                int minCount = LabyrinthMinimums[(int)this.Logic.Settings.Length];
                int maxCount = LabyrinthMaximums[(int)this.Logic.Settings.Length];
                double progress = (double)(Logic.Map.Count - minCount) / (double)(maxCount - minCount);
                if (progress > Math.Sqrt(Logic.Random.NextDouble())) {
                    this.Goodwill = 0; // if we need to backtrack go past this
                    return true;
                }

                var receipt = this.WorkingPossibility();
                if (receipt == null) {
                    this.Goodwill = 0; // if we need to backtrack go past this
                    return true; // never fail!
                }

                this.AddReceipt(receipt);
                this.TriedEdges.Add(receipt.Edge.CorrespondingEdge(this.Edge.Node));
                var targetNode = receipt.Edge.OtherNode(this.Edge.Node);
                var closure = LinkedNodeSet.Closure(targetNode, this.Logic.Caps, this.Logic.Caps, true);

                foreach (var newedge in closure.UnlinkedEdges()) {
                    this.AddNextTask(new TaskLabyrinthContinue(this.Logic, newedge, Math.Min(5, this.Goodwill + 1)));
                }
                this.Goodwill--;
                return true;
            }
        }

        private class TaskLabyrinthFinish : RandoTask {

            public TaskLabyrinthFinish(RandoLogic logic) : base(logic) {
            }

            public override bool Next() {
                int minCount = LabyrinthMinimums[(int)this.Logic.Settings.Length];
                if (Logic.Map.Count < minCount) {
                    return false;
                }
                return true;
            }
        }
    }
}
