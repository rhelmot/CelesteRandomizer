using System;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        private class TaskPathwayStart : RandoTask {
            private HashSet<RandoRoom> TriedRooms = new HashSet<RandoRoom>();

            public TaskPathwayStart(RandoLogic logic) : base(logic) {
            }

            private IEnumerable<RandoRoom> AvailableRooms() {
                foreach (var room in Logic.RemainingRooms) {
                    if (!TriedRooms.Contains(room)) {
                        yield return room;
                    }
                }
            }

            public override bool Next() {
                var available = new List<RandoRoom>(AvailableRooms());

                if (available.Count == 0) {
                    return false;
                }

                var picked = available[this.Logic.Random.Next(available.Count)];
                var pickedLinked = picked.LinkStart(this.Logic.NextNonce);
                this.TriedRooms.Add(picked);
                this.AddLevel(picked, pickedLinked);
                this.AddNextTask(new TaskPathwayPickHole(this.Logic, picked, pickedLinked));

                return true;
            }
        }

        private class TaskPathwayPickHole : RandoTask {
            private RandoRoom Room;
            private LevelData Linked;
            private HashSet<Hole> TriedHoles = new HashSet<Hole>();

            public TaskPathwayPickHole(RandoLogic logic, RandoRoom room, LevelData linked) : base(logic) {
                this.Room = room;
                this.Linked = linked;
            }

            private IEnumerable<Hole> AvailableHoles() {
                foreach (var hole in this.Room.Holes) {
                    if (hole.Kind == HoleKind.Out || hole.Kind == HoleKind.InOut) {
                        if (!this.TriedHoles.Contains(hole)) {
                            yield return hole;
                        }
                    }
                }
            }

            public override bool Next() {
                List<Hole> available = new List<Hole>(this.AvailableHoles());
                if (available.Count == 0) {
                    return false;
                }

                var picked = available[this.Logic.Random.Next(available.Count)];
                this.TriedHoles.Add(picked);
                this.AddNextTask(new TaskPathwayPickRoom(this.Logic, this.Linked, picked));
                return true;
            }
        }

        private class TaskPathwayPickRoom : RandoTask {
            private LevelData Linked;
            private Hole Hole;
            private HashSet<RandoRoom> TriedRooms = new HashSet<RandoRoom>();
            private bool IsEnd;

            private static readonly int[] Minimums = { 15, 30, 50, 80 };
            private static readonly int[] Ranges = { 15, 30, 30, 70 };

            public TaskPathwayPickRoom(RandoLogic Logic, LevelData linked, Hole hole) : base(Logic) {
                this.Linked = linked;
                this.Hole = hole;

                double progress = (double)(Logic.Map.Levels.Count - Minimums[(int)Logic.Settings.Length]) / (double)Ranges[(int)Logic.Settings.Length];
                this.IsEnd = progress > Logic.Random.NextDouble();
            }

            private bool CheckApplicable(RandoRoom room, Hole hole) {
                if (room.End != this.IsEnd) {
                    return false;
                }
                return !TriedRooms.Contains(room); // overapproximation. good enough
                // maybe the better version is that we split room-pick and hole-pick into different tasks
            }

            public override bool Next() {
                var available = this.FindPossibilities(this.Linked, this.Hole, this.CheckApplicable);

                if (available.Count == 0) {
                    return false;
                }

                var picked = this.FindWorkingPosiibility(available, this.Linked, this.Hole);
                if (picked == null) {
                    return false;
                }

                var pickedLinked = picked.Item1.LinkAdjacent(this.Linked, this.Hole.Side, picked.Item3, this.Logic.NextNonce);
                this.TriedRooms.Add(picked.Item1);
                this.AddLevel(picked.Item1, pickedLinked);
                if (!this.IsEnd) {
                    this.AddNextTask(new TaskPathwayPickHole(this.Logic, picked.Item1, pickedLinked));
                }

                return true;
            }
        }
    }
}
