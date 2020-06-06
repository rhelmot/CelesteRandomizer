using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public class StaticRoom {
        public AreaKey Area;
        public LevelData Level;
        public List<Hole> Holes;
        public readonly String Name;
        public bool End;
        public List<RandoConfigEdit> Tweaks;
        public Dictionary<string, StaticNode> Nodes;

        public StaticRoom(AreaKey Area, RandoConfigRoom config, LevelData Level, List<Hole> Holes) {
            this.Area = Area;
            this.Level = Level;
            this.Holes = Holes;

            this.Name = AreaData.Get(Area).GetSID() + "/" + (Area.Mode == AreaMode.Normal ? "A" : Area.Mode == AreaMode.BSide ? "B" : "C") + "/" + Level.Name;
            this.End = config.End;
            this.Tweaks = config.Tweaks ?? new List<RandoConfigEdit>();

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
        }

        private void ProcessSubroom(StaticNode node, RandoConfigRoom config) {
            foreach (RandoConfigHole holeConfig in config.Holes ?? new List<RandoConfigHole>()) {
                if (holeConfig.Kind == HoleKind.None) {
                    continue;
                }

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

                node.Edges.Add(new StaticEdge() {
                    FromNode = node,
                    HoleTarget = matchedHole,
                    ReqIn = this.ProcessReqs(holeConfig.ReqIn, matchedHole, false),
                    ReqOut = this.ProcessReqs(holeConfig.ReqOut, matchedHole, true)
                });
            }

            foreach (var edge in config.InternalEdges ?? new List<RandoConfigInternalEdge>()) {
                StaticNode toNode;
                if (edge.To != null) {
                    toNode = this.Nodes[edge.To];
                } else if (edge.Split != null) {
                    if (node.Edges.Count != 2) {
                        throw new Exception("Cannot split: must have exactly two edges");
                    }

                    toNode = new StaticNode() {
                        Name = node.Name + "_autosplit",
                        ParentRoom = node.ParentRoom
                    };
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
                } else {
                    throw new Exception("Internal edge must have either To or Split");
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

            if (matchHole != null) {
                conjunction.Add(new HoleRequirement(matchHole));
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
                if (entity.Name == "goldenBerry" || entity.Name == "musicTrigger" || entity.Name == "noRefillTrigger" || entity.Name == "checkpoint") {
                    removals.Add(entity);
                    return;
                } else if (entity.Name == "finalBoss") {
                    entity.Values["canChangeMusic"] = false;
                }

                foreach (var econfig in this.Tweaks) {
                    if (econfig.Update?.Remove ?? false) {
                        Logger.Log("randomizer", $"config may remove; entity: {entity.Name} {entity.ID}; config: {econfig.Name} {econfig.ID}");
                    }
                    if (!(econfig.Update?.Add ?? false) &&
                        (econfig.ID == null || econfig.ID == entity.ID) &&
                        (econfig.Name == null || econfig.Name.ToLower() == entity.Name.ToLower()) &&
                        (econfig.X == null || nearlyEqual(econfig.X.Value, entity.Position.X)) &&
                        (econfig.Y == null || nearlyEqual(econfig.Y.Value, entity.Position.Y))) {
                        if (econfig.Update?.Remove ?? false) {
                            Logger.Log("randomizer", "...removed");
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
                        }
                        break;
                    }
                }
            }
            Vector2? processSpawn(Vector2 spawn) {
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
                        return spawn;
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
            for (int i = 0; i < result.Spawns.Count; i++) {
                result.Spawns[i] = processSpawn(result.Spawns[i]) ?? result.Spawns[i];
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
        public StaticRoom ParentRoom;
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

    public abstract class Requirement {
        public abstract bool Able(Capabilities state);
    }

    public class Impossible : Requirement {
        public override bool Able(Capabilities state) {
            return false;
        }
    }

    public class Possible : Requirement {
        public override bool Able(Capabilities state) {
            return true;
        }
    }

    public class Conjunction : Requirement {
        public List<Requirement> Children;
        public Conjunction(List<Requirement> Children) {
            this.Children = Children;
        }

        public override bool Able(Capabilities state) {
            foreach (var child in this.Children) {
                if (!child.Able(state)) {
                    return false;
                }
            }
            return true;
        }
    }

    public class Disjunction : Requirement {
        public List<Requirement> Children;
        public Disjunction(List<Requirement> Children) {
            this.Children = Children;
        }

        public override bool Able(Capabilities state) {
            foreach (var child in this.Children) {
                if (child.Able(state)) {
                    return true;
                }
            }
            return false;
        }
    }

    public class DashRequirement : Requirement {
        public NumDashes Dashes;
        public DashRequirement(NumDashes dashes) {
            this.Dashes = dashes;
        }

        public override bool Able(Capabilities state) {
            return state.Dashes >= this.Dashes;
        }
    }

    public class SkillRequirement : Requirement {
        public Difficulty Difficulty;
        public SkillRequirement(Difficulty Difficulty) {
            this.Difficulty = Difficulty;
        }

        public override bool Able(Capabilities state) {
            return state.PlayerSkill >= this.Difficulty;
        }
    }

    public class HoleRequirement : Requirement {
        public Hole Hole;
        public HoleRequirement(Hole Hole) {
            this.Hole = Hole;
        }

        public override bool Able(Capabilities state) {
            if (state.MatchHole == null) {
                return true;
            }

            // not the most efficient! but who cares!
            return this.Hole.Compatible(state.MatchHole) != Hole.INCOMPATIBLE;
        }
    }

    public class Capabilities {
        public NumDashes Dashes;
        public NumDashes RefillDashes;
        public Difficulty PlayerSkill;

        public Hole MatchHole;
    }
}
