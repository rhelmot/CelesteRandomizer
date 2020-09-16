using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {

        private static readonly int[] LabyrinthMinimums = { 15, 25, 50, 70 };
        private static readonly int[] LabyrinthMaximums = { 20, 40, 65, 90 };

        private List<UnlinkedEdge> PossibleContinuations = new List<UnlinkedEdge>();
        private List<Tuple<UnlinkedCollectable, bool>> PossibleCollectables = new List<Tuple<UnlinkedCollectable, bool>>();
        private List<Tuple<UnlinkedCollectable, bool>> PriorityCollectables = new List<Tuple<UnlinkedCollectable, bool>>();
        int StartingGemCount;

        private void GenerateLabyrinth() {
            this.Caps = this.Caps.WithoutKey();
            this.StartingGemCount = this.Settings.Length == MapLength.Short ? 3 : 0;

            void retry() {
                this.PossibleCollectables.Clear();
                this.PriorityCollectables.Clear();
                this.PossibleContinuations.Clear();
                this.ResetRooms();
                this.Map.Clear();
            }
            tryagain:

            foreach (var room in RandoLogic.AllRooms) {
                if (room.Name == "Celeste/6-Reflection/A/b-00") {
                    var lroom = new LabyrinthStartRoom(room);
                    this.Map.AddRoom(lroom);
                    this.RemainingRooms.Remove(room);
                    this.PossibleContinuations.AddRange(LinkedNodeSet.Closure(lroom.Nodes["main"], this.Caps, this.Caps, true).UnlinkedEdges());
                    break;
                }
            }

            while (this.PossibleContinuations.Count != 0) {
                //Logger.Log("DEBUG", $"status: rooms={this.Map.Count} queue={this.PossibleContinuations.Count}");
                int idx = this.Random.Next(this.PossibleContinuations.Count);
                var startEdge = this.PossibleContinuations[idx];
                this.PossibleContinuations.RemoveAt(idx);

                foreach (var toEdge in this.AvailableNewEdges(this.Caps, this.Caps,
                        (edge) => edge.FromNode.ParentRoom.ReqEnd is Impossible && edge.FromNode.ParentRoom.Name != "Celeste/7-Summit/A/g-00b")) {
                    var result = ConnectAndMapReceipt.Do(this, startEdge, toEdge);
                    if (result != null) {
                        var closure = LinkedNodeSet.Closure(result.EntryNode, this.Caps, this.Caps, true);
                        var ue = closure.UnlinkedEdges();
                        var uc = new List<Tuple<UnlinkedCollectable, bool>>();
                        var alreadySeen = new HashSet<UnlinkedCollectable>();
                        foreach (var c in closure.UnlinkedCollectables()) {
                            alreadySeen.Add(c);
                            uc.Add(Tuple.Create(c, false));
                        }
                        closure.Extend(this.Caps, null, true);
                        foreach (var c in closure.UnlinkedCollectables()) {
                            if (alreadySeen.Contains(c)) {
                                continue;
                            }
                            uc.Add(Tuple.Create(c, true));
                        }
                        if (ue.Count == 0 && uc.Count == 0) {
                            result.Undo();
                            continue;
                        } else if (ue.Count == 0) {
                            this.PriorityCollectables.AddRange(uc);
                        } else {
                            this.PossibleContinuations.AddRange(ue);
                            this.PossibleCollectables.AddRange(uc);
                        }
                        break;
                    }
                }

                if (this.Map.Count >= LabyrinthMaximums[(int)this.Settings.Length]) {
                    break;
                }
            }

            while (this.PossibleContinuations.Count != 0) {
                var startEdge = this.PossibleContinuations[this.PossibleContinuations.Count - 1];
                this.PossibleContinuations.RemoveAt(this.PossibleContinuations.Count - 1);

                var closure = LinkedNodeSet.Closure(startEdge.Node, this.Caps, this.Caps, true);
                var uc = new List<Tuple<UnlinkedCollectable, bool>>();
                var alreadySeen = new HashSet<UnlinkedCollectable>();
                foreach (var c in closure.UnlinkedCollectables()) {
                    alreadySeen.Add(c);
                    uc.Add(Tuple.Create(c, false));
                }
                closure.Extend(this.Caps, null, true);
                foreach (var c in closure.UnlinkedCollectables()) {
                    if (alreadySeen.Contains(c)) {
                        continue;
                    }
                    uc.Add(Tuple.Create(c, true));
                }

                if (uc.Count == 0) {
                    var edgeCount = 0;
                    foreach (var node in startEdge.Node.Room.Nodes.Values) {
                        edgeCount += node.Edges.Count;
                    }
                    if (edgeCount <= 1) {
                        foreach (var node in startEdge.Node.Room.Nodes.Values) {
                            foreach (var edge in node.Edges) {
                                var otherNode = edge.OtherNode(node);
                                var otherEdge = edge.OtherEdge(node);
                                otherNode.Edges.Remove(edge);
                                this.PossibleContinuations.Add(new UnlinkedEdge() { Static = otherEdge, Node = otherNode });
                            }
                        }
                        this.Map.RemoveRoom(startEdge.Node.Room);
                    }
                } else {
                    this.PriorityCollectables.AddRange(uc);
                    foreach (var c in uc) {
                        this.PossibleCollectables.Remove(c);
                    }
                }
            }

            if (this.Map.Count < LabyrinthMinimums[(int)this.Settings.Length]) {
                //Logger.Log("DEBUG", "retrying - too short");
                retry();
                goto tryagain;
            }

            if (this.PossibleCollectables.Count + this.PriorityCollectables.Count < (6 - this.StartingGemCount)) {
                //Logger.Log("DEBUG", "retrying - not enough spots");
                retry();
                goto tryagain;
            }

            for (var gem = LinkedNode.LinkedCollectable.Gem1 + this.StartingGemCount; gem <= LinkedNode.LinkedCollectable.Gem6; gem++) {
                var collection = this.PriorityCollectables.Count != 0 ? this.PriorityCollectables : this.PossibleCollectables;
                if (collection.Count == 0) {  // just in case
                    retry();
                    goto tryagain;
                }
                var idx = this.Random.Next(collection.Count);
                var spot = collection[idx].Item1;
                var autoBubble = collection[idx].Item2;
                collection.RemoveAt(idx);

                if (spot.Static.MustFly) {
                    spot.Node.Collectables[spot.Static] = Tuple.Create(LinkedNode.LinkedCollectable.WingedStrawberry, autoBubble);
                    gem--;
                } else {
                    spot.Node.Collectables[spot.Static] = Tuple.Create(gem, autoBubble);
                    //Logger.Log("DEBUG", $"Adding gem to {spot.Node.Room.Static.Name}");

                    if (collection == this.PriorityCollectables) {
                        for (int i = 0; i < collection.Count; i++) {
                            if (collection[i].Item1.Node.Room == spot.Node.Room) {
                                collection.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }
            }

            while (this.PriorityCollectables.Count != 0) {
                var spot = this.PriorityCollectables.Last().Item1;
                var autoBubble = this.PriorityCollectables.Last().Item2;
                this.PriorityCollectables.RemoveAt(this.PriorityCollectables.Count - 1);

                spot.Node.Collectables[spot.Static] = Tuple.Create(spot.Static.MustFly ? LinkedNode.LinkedCollectable.WingedStrawberry : LinkedNode.LinkedCollectable.Strawberry, autoBubble);
            }

            var targetCount = this.PossibleCollectables.Count / 3 * 2;
            this.PossibleCollectables.Shuffle(this.Random);
            while (this.PossibleCollectables.Count > targetCount) {
                var spot = this.PossibleCollectables.Last().Item1;
                var autoBubble = this.PossibleCollectables.Last().Item2;
                this.PossibleCollectables.RemoveAt(this.PossibleCollectables.Count - 1);

                spot.Node.Collectables[spot.Static] = Tuple.Create(spot.Static.MustFly ? LinkedNode.LinkedCollectable.WingedStrawberry : LinkedNode.LinkedCollectable.Strawberry, autoBubble);
            }
        }

        private class LabyrinthStartRoom : LinkedRoom {
            public LabyrinthStartRoom(StaticRoom room) : base(room, Vector2.Zero) { }

            public override LevelData Bake(int? nonce, RandoSettings settings, Random random) {
                var result = base.Bake(nonce, settings, random);

                int maxID = 0;
                EntityData granny = null;
                foreach (var e in result.Entities) {
                    maxID = Math.Max(maxID, e.ID);
                    if (e.Name == "npc") {
                        granny = e;
                    }
                }
                result.Entities.Remove(granny);

                result.Spawns.Insert(0, new Vector2(336, 144));

                result.Entities.Add(new EntityData {
                    Name = "summitGemManager",
                    ID = ++maxID,
                    Level = result,
                    Position = new Vector2(384, 136),
                    Nodes = new Vector2[] {
                        new Vector2(330,    168),
                        new Vector2(346+4,  156),
                        new Vector2(362+8,  168),
                        new Vector2(378+12, 156),
                        new Vector2(394+16, 168),
                        new Vector2(410+20, 156)
                    }
                });

                result.Entities.Add(new EntityData {
                    Name = "invisibleBarrier",
                    ID = ++maxID,
                    Level = result,
                    Position = new Vector2(392, 32),
                    Width = 48,
                    Height = 32
                });

                result.Entities.Add(new EntityData {
                    Name = "blackGem",
                    ID = ++maxID,
                    Level = result,
                    Position = new Vector2(416, 48),
                });

                foreach (var pos in new Vector2[] {
                    new Vector2(400, 56),
                    new Vector2(408, 64),
                    new Vector2(416, 72),
                    new Vector2(424, 64),
                    new Vector2(432, 64),
                    new Vector2(440, 56),
                    new Vector2(440, 48),
                    new Vector2(432, 40),
                    new Vector2(432, 32),
                }) {
                    result.Entities.Add(new EntityData {
                        Name = "spinner",
                        ID = ++maxID,
                        Level = result,
                        Position = pos
                    });
                }

                return result;
            }
        }
    }
}
