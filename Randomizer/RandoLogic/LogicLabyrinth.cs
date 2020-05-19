using System;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        private class TaskLabyrinthStart : RandoTask {
            private HashSet<RandoRoom> TriedRooms;

            public TaskLabyrinthStart(RandoLogic logic) : base(logic) {
                TriedRooms = new HashSet<RandoRoom>();
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

                foreach (var hole in picked.Holes) {
                    if (hole.Kind == HoleKind.Out || hole.Kind == HoleKind.InOut) {
                        this.AddLastTask(new TaskLabyrinthContinue(this.Logic, pickedLinked, hole));
                    }
                }
                return true;
            }
        }

        private class TaskLabyrinthContinue : RandoTask {
            private LevelData Room;
            private Hole Hole;

            private HashSet<RandoRoom> TriedRooms = new HashSet<RandoRoom>();
            private RandoRoom CurrentRoom;
            private HashSet<Hole> TriedHoles = new HashSet<Hole>();

            public TaskLabyrinthContinue(RandoLogic logic, LevelData room, Hole hole) : base(logic) {
                this.Room = room;
                this.Hole = hole;
            }

            private bool CheckApplicable(RandoRoom room, Hole hole) {
                if (CurrentRoom == null) {
                    return !TriedRooms.Contains(room);
                } else {
                    if (CurrentRoom != room) {
                        return false;
                    }
                    return !TriedHoles.Contains(hole);
                }
            }

            public override bool Next() {
                var available = this.FindPossibilities(Room, Hole, this.CheckApplicable);

                bool Retry() {
                    if (this.CurrentRoom == null) {
                        return true; // never return false, simply allow failures to be failures
                    }

                    this.TriedRooms.Add(this.CurrentRoom);
                    this.TriedHoles.Clear();
                    this.CurrentRoom = null;
                    return this.Next();
                }

                if (available.Count == 0) {
                    return Retry();
                }

                var picked = this.FindWorkingPosiibility(available, this.Room, this.Hole);
                if (picked == null) {
                    return Retry();
                }

                var linked = picked.Item1.LinkAdjacent(this.Room, this.Hole.Side, picked.Item3, this.Logic.NextNonce);

                this.CurrentRoom = picked.Item1;
                this.TriedHoles.Add(picked.Item2);

                this.AddLevel(picked.Item1, linked);
                foreach (var hole in picked.Item1.Holes) {
                    if (hole == picked.Item2) {
                        continue;
                    }
                    if (hole.Kind == HoleKind.Out || hole.Kind == HoleKind.InOut) {
                        AddLastTask(new TaskLabyrinthContinue(Logic, linked, hole));
                    }
                }
                return true;
            }
        }
    }
}
