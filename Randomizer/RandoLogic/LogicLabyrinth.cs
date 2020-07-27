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
                    if (room.End) {
                        continue;
                    }
                    if (TriedRooms.Contains(room)) {
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

                this.AddLastTask(new TaskLabyrinthFinish(this.Logic));

                var closure = LinkedNodeSet.Closure(receipt.NewRoom.Nodes["main"], this.Logic.Caps.WithoutKey(), this.Logic.Caps.WithoutKey(), true);
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

            private ConnectAndMapReceipt WorkingPossibility() {
                var possibilities = this.Logic.AvailableNewEdges(this.Logic.Caps.WithoutKey(), this.Logic.Caps.WithoutKey(), (StaticEdge edge) => !edge.FromNode.ParentRoom.End && !this.TriedEdges.Contains(edge));

                foreach (var edge in possibilities) {
                    var result = ConnectAndMapReceipt.Do(this.Logic, this.Edge, edge);
                    if (result != null) {
                        return result;
                    }
                }

                return null;
            }

            public override bool Next() {

                int minCount = LabyrinthMinimums[(int)this.Logic.Settings.Length];
                int maxCount = LabyrinthMaximums[(int)this.Logic.Settings.Length];
                double progress = (double)(Logic.Map.Count - minCount) / (double)(maxCount - minCount);
                Logger.Log("randomizer", $"Progress: {progress}");
                if (progress > Logic.Random.NextDouble()) {
                    Logger.Log("randomizer", "No need to proceed");
                    this.Goodwill = 0; // if we need to backtrack go past this
                    return true;
                }

                if (this.Goodwill <= 0) {
                    Logger.Log("randomizer", "Failure: ran out of goodwill");
                    return false;
                }

                var receipt = this.WorkingPossibility();
                if (receipt == null) {
                    Logger.Log("randomizer", "No working possibilities");
                    this.Goodwill = 0; // if we need to backtrack go past this
                    return true; // never fail!
                }

                this.AddReceipt(receipt);
                this.TriedEdges.Add(receipt.Edge.CorrespondingEdge(this.Edge.Node));
                var targetNode = receipt.Edge.OtherNode(this.Edge.Node);
                var closure = LinkedNodeSet.Closure(targetNode, this.Logic.Caps.WithoutKey(), this.Logic.Caps.WithoutKey(), true);

                var any = false;
                foreach (var newedge in closure.UnlinkedEdges()) {
                    any = true;
                    this.AddNextTask(new TaskLabyrinthContinue(this.Logic, newedge, Math.Min(5, this.Goodwill + 1)));
                }
                if (!any) {
                    this.Goodwill = 0;
                } else {
                    this.Goodwill--;
                }
                return true;
            }
        }

        private class TaskLabyrinthFinish : RandoTask {

            public TaskLabyrinthFinish(RandoLogic logic) : base(logic) {
            }

            public override bool Next() {
                int minCount = LabyrinthMinimums[(int)this.Logic.Settings.Length];
                if (Logic.Map.Count < minCount) {
                    Logger.Log("randomizer", "Failure: map is too short");
                    return false;
                }
                return true;
            }
        }
    }
}
