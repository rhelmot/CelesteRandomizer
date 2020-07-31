using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public class StaticRoom {
        public AreaKey Area;
        public LevelData Level;
        public readonly string Name;
        public readonly bool End;
        private List<RandoConfigEdit> Tweaks;
        public Dictionary<string, StaticNode> Nodes;

        public List<Hole> Holes;
        public List<StaticCollectable> Collectables;

        public StaticRoom(AreaKey Area, RandoConfigRoom config, LevelData Level, List<Hole> Holes) {
            this.Area = Area;
            this.Level = Level;
            this.Holes = Holes;

            this.Name = AreaData.Get(Area).GetSID() + "/" + (Area.Mode == AreaMode.Normal ? "A" : Area.Mode == AreaMode.BSide ? "B" : "C") + "/" + Level.Name;
            this.End = config.End;
            this.Tweaks = config.Tweaks ?? new List<RandoConfigEdit>();

            this.Collectables = new List<StaticCollectable>();
            foreach (var entity in Level.Entities) {
                switch (entity.Name.ToLower()) {
                    case "strawberry":
                    case "key":
                        this.Collectables.Add(new StaticCollectable {
                            Position = entity.Position,
                            MustFly = false,
                        });
                        break;
                }
            }
            this.Collectables.Sort((StaticCollectable a, StaticCollectable b) => {
                if (a.Position.Y > b.Position.Y) {
                    return 1;
                } else if (a.Position.Y < b.Position.Y) {
                    return -1;
                } else if (a.Position.X > b.Position.X) {
                    return 1;
                } else if (a.Position.X < b.Position.X) {
                    return -1;
                } else {
                    return 0;
                }
            });

            this.Nodes = new Dictionary<string, StaticNode>() {
                { "main", new StaticNode() {
                    Name = "main",
                    ParentRoom = this
                } }
            };
            foreach (var subroom in config.Subrooms ?? new List<RandoConfigRoom>()) {
                if (subroom.Room == null || this.Nodes.ContainsKey(subroom.Room)) {
                    throw new Exception($"Invalid subroom name in {this.Area} {this.Name}");
                }
                this.Nodes.Add(subroom.Room, new StaticNode() { Name = subroom.Room, ParentRoom = this });
            }

            this.ProcessSubroom(this.Nodes["main"], config);
            foreach (var subroom in config.Subrooms ?? new List<RandoConfigRoom>()) {
                this.ProcessSubroom(this.Nodes[subroom.Room], subroom);
            }

            // assign unmarked holes
            foreach (var uhole in this.Holes) {
                if (uhole.Kind != HoleKind.Unknown) {
                    continue;
                }

                var bestDist = 10000f;
                StaticNode bestNode = null;
                var lowPos = uhole.LowCoord(this.Level.Bounds);
                var highPos = uhole.HighCoord(this.Level.Bounds);
                foreach (var node in this.Nodes.Values) {
                    foreach (var edge in node.Edges) {
                        if (edge.HoleTarget == null) {
                            continue;
                        }
                        if (edge.HoleTarget == uhole) {
                            bestNode = null;
                            goto doublebreak;
                        }

                        var pos = edge.HoleTarget.LowCoord(this.Level.Bounds);
                        var dist = (pos - lowPos).Length();
                        if (dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }

                        dist = (pos - highPos).Length();
                        if (dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }

                        pos = edge.HoleTarget.HighCoord(this.Level.Bounds);
                        dist = (pos - lowPos).Length();
                        if (dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }

                        dist = (pos - highPos).Length();
                        if (dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }
                    }
                }

                doublebreak:
                if (bestNode != null) {
                    bestNode.Edges.Add(new StaticEdge {
                        FromNode = bestNode,
                        HoleTarget = uhole,
                        ReqIn = this.ProcessReqs(null, uhole, false),
                        ReqOut = this.ProcessReqs(null, uhole, true),
                    });
                }
            }

            // assign unmarked collectables
            foreach (var c in this.Collectables) {
                if (c.ParentNode != null) {
                    continue;
                }

                var bestDist = 1000f;
                StaticNode bestNode = null;
                foreach (var node in this.Nodes.Values) {
                    foreach (var edge in node.Edges) {
                        if (edge.HoleTarget == null) {
                            continue;
                        }

                        var pos = edge.HoleTarget.LowCoord(new Rectangle(0, 0, this.Level.Bounds.Width, this.Level.Bounds.Height));
                        var dist = (pos - c.Position).Length();
                        if (dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }

                        pos = edge.HoleTarget.HighCoord(new Rectangle(0, 0, this.Level.Bounds.Width, this.Level.Bounds.Height));
                        dist = (pos - c.Position).Length();
                        if (dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }
                    }
                }

                if (bestNode != null) {
                    c.ParentNode = bestNode;
                    bestNode.Collectables.Add(c);
                }
            }
        }

        private void ProcessSubroom(StaticNode node, RandoConfigRoom config) {
            foreach (RandoConfigHole holeConfig in config.Holes ?? new List<RandoConfigHole>()) {
                Hole matchedHole = null;
                int remainingMatches = holeConfig.Idx;
                foreach (Hole hole in this.Holes) {
                    if (hole.Side == holeConfig.Side) {
                        if (remainingMatches == 0) {
                            matchedHole = hole;
                            break;
                        } else {
                            remainingMatches--;
                        }
                    }
                }

                if (matchedHole == null) {
                    throw new Exception($"Could not find the hole identified by area:{this.Area} room:{config.Room} side:{holeConfig.Side} idx:{holeConfig.Idx}");
                }

                //Logger.Log("randomizer", $"Matching {roomConfig.Room} {holeConfig.Side} {holeConfig.Idx} to {matchedHole}");
                matchedHole.Kind = holeConfig.Kind;
                if (holeConfig.LowBound != null) {
                    matchedHole.LowBound = (int)holeConfig.LowBound;
                }
                if (holeConfig.HighBound != null) {
                    matchedHole.HighBound = (int)holeConfig.HighBound;
                }
                if (holeConfig.HighOpen != null) {
                    matchedHole.HighOpen = (bool)holeConfig.HighOpen;
                }

                if (holeConfig.Kind != HoleKind.None) {
                    node.Edges.Add(new StaticEdge() {
                        FromNode = node,
                        HoleTarget = matchedHole,
                        ReqIn = this.ProcessReqs(holeConfig.ReqIn, matchedHole, false),
                        ReqOut = this.ProcessReqs(holeConfig.ReqOut, matchedHole, true)
                    });
                }
            }

            foreach (var edge in config.InternalEdges ?? new List<RandoConfigInternalEdge>()) {
                StaticNode toNode;
                if (edge.To != null) {
                    toNode = this.Nodes[edge.To];
                } else if (edge.Split != null) {
                    if (node.Edges.Count != 2) {
                        throw new Exception($"Cannot split: must have exactly two edges ({this.Name} {node.Name})");
                    }

                    toNode = new StaticNode() {
                        Name = node.Name + "_autosplit",
                        ParentRoom = node.ParentRoom
                    };
                    if (node.ParentRoom.Nodes.ContainsKey(toNode.Name)) {
                        throw new Exception("You may only autosplit a room once");
                    }
                    node.ParentRoom.Nodes[toNode.Name] = toNode;

                    bool firstMain;
                    var first = node.Edges[0].HoleTarget;
                    var second = node.Edges[1].HoleTarget;
                    switch (edge.Split) {
                        case RandoConfigInternalEdge.SplitKind.BottomToTop:
                            firstMain = first.Side == ScreenDirection.Down || second.Side == ScreenDirection.Up ||
                                (first.Side != ScreenDirection.Up && second.Side != ScreenDirection.Down && first.HighBound > second.HighBound);
                            break;
                        case RandoConfigInternalEdge.SplitKind.TopToBottom:
                            firstMain = first.Side == ScreenDirection.Up || second.Side == ScreenDirection.Down ||
                                (first.Side != ScreenDirection.Down && second.Side != ScreenDirection.Up && first.LowBound < second.LowBound);
                            break;
                        case RandoConfigInternalEdge.SplitKind.RightToLeft:
                            firstMain = first.Side == ScreenDirection.Right || second.Side == ScreenDirection.Left ||
                                (first.Side != ScreenDirection.Left && second.Side != ScreenDirection.Right && first.HighBound > second.HighBound);
                            break;
                        case RandoConfigInternalEdge.SplitKind.LeftToRight:
                        default:
                            firstMain = first.Side == ScreenDirection.Left || second.Side == ScreenDirection.Right ||
                                (first.Side != ScreenDirection.Right && second.Side != ScreenDirection.Left && first.LowBound < second.LowBound);
                            break;
                    }

                    var secondary = firstMain ? node.Edges[1] : node.Edges[0];
                    node.Edges.Remove(secondary);
                    toNode.Edges.Add(secondary);
                    secondary.FromNode = toNode;
                } else if (edge.Collectable != null) {
                    toNode = new StaticNode() {
                        Name = node.Name + "_coll" + edge.Collectable.ToString(),
                        ParentRoom = node.ParentRoom
                    };
                    if (node.ParentRoom.Nodes.ContainsKey(toNode.Name)) {
                        throw new Exception("You may only autosplit a room once");
                    }
                    node.ParentRoom.Nodes[toNode.Name] = toNode;

                    var thing = this.Collectables[edge.Collectable.Value];
                    if (thing.ParentNode != null) {
                        throw new Exception("Can only assign a collectable to one owner");
                    }
                    thing.ParentNode = toNode;
                    toNode.Collectables.Add(thing); 
                } else {
                    throw new Exception("Internal edge must have either To or Split or Collectable");
                }

                var reqIn = this.ProcessReqs(edge.ReqIn, null, false);
                var reqOut = this.ProcessReqs(edge.ReqOut, null, true);

                var forward = new StaticEdge() {
                    FromNode = node,
                    NodeTarget = toNode,
                    ReqIn = reqIn,
                    ReqOut = reqOut
                };
                var reverse = new StaticEdgeReversed(forward, node);

                node.Edges.Add(forward);
                toNode.Edges.Add(reverse);
            }

            foreach (var col in config.Collectables) {
                if (col.Idx != null) {
                    var thing = this.Collectables[col.Idx.Value];
                    if (thing.ParentNode != null) {
                        throw new Exception("Can only assign a collectable to one owner");
                    }
                    thing.ParentNode = node;
                    thing.MustFly = col.MustFly;
                    node.Collectables.Add(thing);
                } else if (col.X != null && col.Y != null) {
                    node.Collectables.Add(new StaticCollectable {
                        ParentNode = node,
                        Position = new Vector2((float)col.X.Value, (float)col.Y.Value),
                        MustFly = col.MustFly
                    });
                } else {
                    throw new Exception("Collectable must specify Idx or X/Y");
                }
            }
        }

        private Requirement ProcessReqs(RandoConfigReq config, Hole matchHole=null, bool isOut=false) {
            if (matchHole != null) {
                if (matchHole.Kind == HoleKind.None) {
                    return new Impossible();
                }
                if (isOut && (matchHole.Kind == HoleKind.In || matchHole.Kind == HoleKind.Unknown)) {
                    return new Impossible();
                }
                if (!isOut && matchHole.Kind == HoleKind.Out) {
                    return new Impossible();
                }
                if (config == null) {
                    return new Possible();
                }
            }
            if (config == null) {
                return new Impossible();
            }

            var conjunction = new List<Requirement>();

            if (config.And != null) {
                var children = new List<Requirement>();
                foreach (var childconfig in config.And) {
                    children.Add(this.ProcessReqs(childconfig));
                }
                conjunction.Add(new Conjunction(children));
            }

            if (config.Or != null) {
                var children = new List<Requirement>();
                foreach (var childconfig in config.Or) {
                    children.Add(this.ProcessReqs(childconfig));
                }
                conjunction.Add(new Disjunction(children));
            }

            if (config.Dashes != null) {
                conjunction.Add(new DashRequirement(config.Dashes.Value));
            }

            // not nullable
            conjunction.Add(new SkillRequirement(config.Difficulty));

            if (config.Key) {
                conjunction.Add(new KeyRequirement());
            }

            if (conjunction.Count == 0) {
                throw new Exception("this should be unreachable");
            } else if (conjunction.Count == 1) {
                return conjunction[0];
            }
            return new Conjunction(conjunction);
        }

        public LevelData MakeLevelData(Vector2 newPosition, int? nonce) {
            var result = this.Level.Copy();
            result.Name = nonce == null ? this.Name : this.Name + "/" + nonce.ToString();
            result.Position = newPosition;
            result.Music = "";
            result.DisableDownTransition = false;
            result.HasCheckpoint = false;

            if (this.Tweaks == null) {
                this.Tweaks = new List<RandoConfigEdit>();
            }

            var toRemoveEntities = new List<EntityData>();
            var toRemoveTriggers = new List<EntityData>();
            var toRemoveSpawns = new List<Vector2>();
            void processEntity(EntityData entity, List<EntityData> removals) {
                switch (entity.Name.ToLower()) {
                    case "goldenberry":
                    case "musictrigger":
                    case "musicfadetrigger":
                    case "norefilltrigger":
                    case "checkpoint":
                    case "strawberry":
                    case "key":
                        removals.Add(entity);
                        return;
                    case "finalboss":
                    case "badelineoldsite":
                    case "darkchaser":
                        if (this.Name == "Celeste/2-OldSite/A/3") break;  // allow the cutscene badeline to change the music
                        if (entity.Values == null) entity.Values = new Dictionary<string, object>();
                        entity.Values["canChangeMusic"] = false;
                        break;
                    case "cloud":
                        if (this.Area.Mode != AreaMode.Normal) {
                            if (entity.Values == null) entity.Values = new Dictionary<string, object>();
                            entity.Values["small"] = "true";
                        }
                        break;
                }

                foreach (var econfig in this.Tweaks) {
                    if (!(econfig.Update?.Add ?? false) &&
                        (econfig.ID == null || econfig.ID == entity.ID) &&
                        (econfig.Name == null || econfig.Name.ToLower() == entity.Name.ToLower()) &&
                        (econfig.X == null || nearlyEqual(econfig.X.Value, entity.Position.X)) &&
                        (econfig.Y == null || nearlyEqual(econfig.Y.Value, entity.Position.Y))) {
                        if (econfig.Update?.Remove ?? false) {
                            removals.Add(entity);
                        } else {
                            if (econfig.Update?.X != null)
                                entity.Position.X = econfig.Update.X.Value;
                            if (econfig.Update?.Y != null)
                                entity.Position.Y = econfig.Update.Y.Value;
                            if (econfig.Update?.Width != null)
                                entity.Width = econfig.Update.Width.Value;
                            if (econfig.Update?.Height != null)
                                entity.Height = econfig.Update.Height.Value;
                            if (econfig.Update?.Nodes != null) {
                                foreach (var nodeconfig in econfig.Update.Nodes) {
                                    if (nodeconfig.Idx >= entity.Nodes.Length) {
                                        var newnodes = new List<Vector2>(entity.Nodes);
                                        newnodes.Add(new Vector2(nodeconfig.X.Value, nodeconfig.Y.Value));
                                        entity.Nodes = newnodes.ToArray();
                                    } else {
                                        entity.Nodes[nodeconfig.Idx] = new Vector2(
                                            nodeconfig.X ?? entity.Nodes[nodeconfig.Idx].X,
                                            nodeconfig.Y ?? entity.Nodes[nodeconfig.Idx].Y
                                        );
                                    }
                                }
                            }
                            if (econfig.Update?.Values != null) {
                                if (entity.Values == null) entity.Values = new Dictionary<string, object>();
                                foreach (var kv in econfig.Update.Values) {
                                    entity.Values[kv.Key] = (object)kv.Value;
                                }
                            }
                        }
                        break;
                    }
                }
            }
            Tuple<Vector2, bool> processSpawn(Vector2 spawn) {
                foreach (var econfig in this.Tweaks) {
                    if (!(econfig.Update?.Add ?? false) &&
                        econfig.Name?.ToLower() == "spawn" &&
                        (econfig.X == null || nearlyEqual(econfig.X.Value, spawn.X - newPosition.X)) &&
                        (econfig.Y == null || nearlyEqual(econfig.Y.Value, spawn.Y - newPosition.Y))) {
                        if (econfig.Update?.Remove ?? false) {
                            toRemoveSpawns.Add(spawn);
                        } else {
                            if (econfig.Update?.X != null)
                                spawn.X = econfig.Update.X.Value + newPosition.X;
                            if (econfig.Update?.Y != null)
                                spawn.Y = econfig.Update.Y.Value + newPosition.Y;
                        }
                        return Tuple.Create(spawn, econfig.Update.Default);
                    }
                }
                return null;
            }
            bool nearlyEqual(float a, float b, float epsilon=0.1f) {
                // https://stackoverflow.com/questions/3874627/floating-point-comparison-functions-for-c-sharp
                const float floatNormal = (1 << 23) * float.Epsilon;
                float absA = Math.Abs(a);
                float absB = Math.Abs(b);
                float diff = Math.Abs(a - b);

                if (a == b) {
                    // Shortcut, handles infinities
                    return true;
                }

                if (a == 0.0f || b == 0.0f || diff < floatNormal) {
                    // a or b is zero, or both are extremely close to it.
                    // relative error is less meaningful here
                    return diff < (epsilon * floatNormal);
                }

                // use relative error
                return diff / Math.Min((absA + absB), float.MaxValue) < epsilon;
            }

            int maxID = 0;
            foreach (var entity in result.Entities) {
                maxID = Math.Max(maxID, entity.ID);
                processEntity(entity, toRemoveEntities);
            }
            foreach (var entity in result.Triggers) {
                maxID = Math.Max(maxID, entity.ID);
                processEntity(entity, toRemoveTriggers);
            }

            int? defaultSpawn = null;
            for (int i = 0; i < result.Spawns.Count; i++) {
                var thing = processSpawn(result.Spawns[i]);
                if (thing != null) {
                    result.Spawns[i] = thing.Item1;
                    if (thing.Item2) {
                        defaultSpawn = i;
                    }
                }
            }
            if (defaultSpawn != null) {
                var thing = result.Spawns[defaultSpawn.Value];
                result.Spawns.RemoveAt(defaultSpawn.Value);
                result.Spawns.Insert(0, thing);
            }

            foreach (var entity in toRemoveEntities) {
                result.Entities.Remove(entity);
            }
            foreach (var entity in toRemoveTriggers) {
                result.Triggers.Remove(entity);
            }
            foreach (var spawn in toRemoveSpawns) {
                result.Spawns.Remove(spawn);
            }

            foreach (var econfig in this.Tweaks) {
                // implement add
                if (econfig.Update?.Add ?? false) {
                    if (econfig.Update?.X == null || econfig.Update?.Y == null) {
                        throw new Exception("Incomplete new entity: must have X and Y");
                    }
                    if (econfig.Name.ToLower() == "spawn") {
                        result.Spawns.Add(new Vector2(econfig.Update.X.Value + result.Position.X, econfig.Update.Y.Value + result.Position.Y));
                    } else {
                        var entity = new EntityData() {
                            Name = econfig.Name,
                            Position = new Vector2((float)econfig.Update.X, (float)econfig.Update.Y),
                            Width = econfig.Update.Width ?? 0,
                            Height = econfig.Update.Height ?? 0,
                            ID = ++maxID,
                            Level = result,
                        };

                        if (econfig.Name.ToLower().EndsWith("trigger")) {
                            result.Triggers.Add(entity);
                        } else {
                            result.Entities.Add(entity);
                        }
                    }
                }
            }
            return result;
        }

        public Rectangle AdjacentBounds(Rectangle against, ScreenDirection side, int offset) {
            var pos = this.AdjacentPosition(against, side, offset);
            return new Rectangle((int)pos.X, (int)pos.Y, this.Level.Bounds.Width, this.Level.Bounds.Height);
        }

        public Vector2 AdjacentPosition(Rectangle against, ScreenDirection side, int offset) {
            Vector2 position = new Vector2(against.Left, against.Top);

            int roundUp(int inp) {
                while (inp % 8 != 0) {
                    inp++;
                }
                return inp;
            }
            switch (side) {
                case ScreenDirection.Up:
                    return position + new Vector2(offset * 8, -roundUp(this.Level.Bounds.Height));
                case ScreenDirection.Down:
                    return position + new Vector2(offset * 8, roundUp(against.Height));
                case ScreenDirection.Left:
                    return position + new Vector2(-roundUp(this.Level.Bounds.Width), offset * 8);
                case ScreenDirection.Right:
                default:
                    return position + new Vector2(roundUp(against.Width), offset * 8);
            }
        }
    }

    public class StaticNode {
        public string Name;
        public List<StaticEdge> Edges = new List<StaticEdge>();
        public List<StaticCollectable> Collectables = new List<StaticCollectable>();
        public StaticRoom ParentRoom;
    }

    public class StaticCollectable {
        public Vector2 Position;
        public bool MustFly;
        public StaticNode ParentNode;
    }

    public class StaticEdge {
        public StaticNode FromNode;
        public StaticNode NodeTarget;
        public virtual Requirement ReqIn { get; set; }
        public virtual Requirement ReqOut { get; set; }
        public Hole HoleTarget;
    }

    public class StaticEdgeReversed : StaticEdge {
        public StaticEdge Child;

        public StaticEdgeReversed(StaticEdge Child, StaticNode target) {
            this.Child = Child;
            this.NodeTarget = target;
            this.FromNode = Child.FromNode;
        }

        public override Requirement ReqIn {
            get {
                return Child.ReqOut;
            }
        }

        public override Requirement ReqOut {
            get {
                return Child.ReqIn;
            }
        }
    }
}
