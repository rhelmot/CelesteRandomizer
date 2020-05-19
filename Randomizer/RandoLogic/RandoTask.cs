using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        private abstract class RandoTask {
            protected RandoLogic Logic;
            private int FrontCount;
            private int BackCount;
            private List<LevelData> AddedLevels = new List<LevelData>();
            private List<RandoRoom> AddedRandoLevels = new List<RandoRoom>();

            public RandoTask(RandoLogic logic) {
                this.Logic = logic;
                this.FrontCount = 0;
                this.BackCount = 0;
            }

            public abstract bool Next();

            protected void AddNextTask(RandoTask toPush) {
                this.Logic.Tasks.AddToFront(toPush);
                this.FrontCount++;
            }

            protected void AddLastTask(RandoTask toPush) {
                this.Logic.Tasks.AddToBack(toPush);
                this.BackCount++;
            }

            protected void AddLevel(RandoRoom randoLevel, LevelData level) {
                this.Logic.Map.Levels.Add(level);
                if (!this.Logic.Settings.RepeatRooms) {
                    this.Logic.RemainingRooms.Remove(randoLevel);
                }
                this.AddedLevels.Add(level);
                this.AddedRandoLevels.Add(randoLevel);
            }

            public void Undo() {
                while (this.FrontCount > 0) {
                    this.Logic.Tasks.RemoveFromFront();
                    this.FrontCount--;
                }

                while (this.BackCount > 0) {
                    this.Logic.Tasks.RemoveFromBack();
                    this.BackCount--;
                }

                foreach (var level in this.AddedLevels) {
                    this.Logic.Map.Levels.Remove(level);
                }
                this.AddedLevels.Clear();

                if (!this.Logic.Settings.RepeatRooms) {
                    foreach (var randoLevel in this.AddedRandoLevels) {
                        this.Logic.RemainingRooms.Add(randoLevel);
                    }
                }
                this.AddedRandoLevels.Clear();
            }

            protected List<ShuffleTuple> FindPossibilities(LevelData lvl, Hole startHole, Func<RandoRoom, Hole, bool> condition = null) {
                var possibilities = new List<ShuffleTuple>();

                // quick check: is it impossible to attach anything to this hole?
                Hole transplant = new Hole(lvl, startHole.Side, startHole.LowBound, startHole.HighBound, startHole.HighOpen);
                Vector2 push = transplant.Side.Unit() * (transplant.Side == ScreenDirection.Up || transplant.Side == ScreenDirection.Down ? 180 : 320);
                Vector2 pt1 = transplant.LowCoord + push;
                Vector2 pt2 = transplant.HighCoord + push;

                if ((this.Logic.Map.GetAt(pt1) ?? this.Logic.Map.GetAt(pt2)) == null) {
                    foreach (RandoRoom prospect in this.Logic.RemainingRooms) {
                        foreach (Hole prospectHole in prospect.Holes) {
                            if (prospectHole.Kind == HoleKind.None ||
                                    prospectHole.Kind == HoleKind.Out ||
                                    (prospectHole.Kind == HoleKind.Unknown && !this.Logic.Settings.EnterUnknown)) {
                                continue;
                            }
                            if (condition != null && !condition(prospect, prospectHole)) {
                                continue;
                            }
                            int offset = startHole.Compatible(prospectHole);
                            if (offset != Hole.INCOMPATIBLE) {
                                possibilities.Add(new ShuffleTuple(prospect, prospectHole, offset));
                            }
                        }
                    }
                }

                possibilities.Shuffle(this.Logic.Random);
                return possibilities;
            }

            protected ShuffleTuple FindWorkingPosiibility(List<ShuffleTuple> possibilities, LevelData lvl, Hole startHole) {
                LevelData cachedConflict = null;
                while (possibilities.Count != 0) {
                    var prospectTuple = possibilities[possibilities.Count - 1];
                    possibilities.RemoveAt(possibilities.Count - 1);
                    RandoRoom prospect = prospectTuple.Item1;
                    Hole prospectHole = prospectTuple.Item2;
                    int offset = prospectTuple.Item3;

                    Rectangle newLvlRect = prospect.QuickLinkAdjacent(lvl, startHole.Side, offset);
                    bool foundConflict = false;

                    if (cachedConflict != null && cachedConflict.Bounds.Intersects(newLvlRect)) {
                        foundConflict = true;
                    } else {
                        foreach (LevelData checkLvl in this.Logic.Map.Levels) {
                            if (checkLvl.Bounds.Intersects(newLvlRect)) {
                                cachedConflict = checkLvl;
                                foundConflict = true;
                                break;
                            }
                        }
                    }

                    if (!foundConflict) {
                        // it works!!!
                        return prospectTuple;
                    }
                }

                return null;
            }
        }
    }
}
