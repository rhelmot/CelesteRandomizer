﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer {
    public class StaticRoom {
        public AreaKey Area;
        public LevelData Level;
        public readonly string Name;
        public readonly Requirement ReqEnd;
        public readonly bool Hub;
        public readonly float Worth;
        public readonly bool SpinnersShatter;
        private List<RandoConfigEdit> Tweaks;
        private RandoConfigCoreMode CoreModes;
        public Dictionary<string, StaticNode> Nodes;
        public List<RandoConfigRectangle> ExtraSpace;

        public List<Hole> Holes;
        public List<StaticCollectable> Collectables;

        public override string ToString() {
            return this.Name;
        }

        public StaticRoom(AreaKey Area, RandoConfigRoom config, LevelData Level, List<Hole> Holes) {
            // hack: force credits screens into the epilogue roomset
            if (Area.ID == 7 && Level.Name.StartsWith("credits-")) {
                Area = new AreaKey(8);
            }
            this.Area = Area;
            this.Level = Level;
            this.Holes = Holes;

            this.Name = AreaData.Get(Area).GetSID() + "/" + (Area.Mode == AreaMode.Normal ? "A" : Area.Mode == AreaMode.BSide ? "B" : "C") + "/" + Level.Name;
            this.ReqEnd = this.ProcessReqs(config.ReqEnd);
            this.Hub = config.Hub;
            this.Tweaks = config.Tweaks ?? new List<RandoConfigEdit>();
            this.CoreModes = config.Core;
            this.ExtraSpace = config.ExtraSpace ?? new List<RandoConfigRectangle>();
            this.Worth = config.Worth ?? (float)Math.Sqrt(Level.Bounds.Width * Level.Bounds.Width + Level.Bounds.Height * Level.Bounds.Height) / 369.12870384189847f + 1;
            this.SpinnersShatter = config.SpinnersShatter;

            this.Collectables = new List<StaticCollectable>();
            foreach (var entity in Level.Entities) {
                if (RandoModule.Instance.MetaConfig.CollectableNames.Contains(entity.Name)) {
                    this.Collectables.Add(new StaticCollectable {
                        Position = entity.Position,
                        MustFly = false,
                    });
                }
            }
            this.Collectables.Sort((a, b) => {
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
                    foreach (var edge in node.Edges.Where(edge => edge.HoleTarget != null)) {
                        if (edge.HoleTarget == uhole) {
                            bestNode = null;
                            goto doublebreak;
                        }

                        var pos = edge.HoleTarget.LowCoord(this.Level.Bounds);
                        var dist = (pos - lowPos).Length();
                        if (!uhole.LowOpen && dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }

                        dist = (pos - highPos).Length();
                        if (!uhole.HighOpen && dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }

                        pos = edge.HoleTarget.HighCoord(this.Level.Bounds);
                        dist = (pos - lowPos).Length();
                        if (!uhole.LowOpen && dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }

                        dist = (pos - highPos).Length();
                        if (!uhole.HighOpen && dist < bestDist) {
                            bestDist = dist;
                            bestNode = node;
                        }
                    }
                }

                doublebreak:
                bestNode?.Edges?.Add(new StaticEdge {
                    FromNode = bestNode,
                    HoleTarget = uhole,
                    ReqIn = this.ProcessReqs(null, uhole, false),
                    ReqOut = this.ProcessReqs(null, uhole, true),
                });
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
            
            // perform fg tweaks
            var regex = new Regex("\\r\\n|\\n\\r|\\n|\\r");
            var tweakable = new List<List<char>>();
            foreach (var line in regex.Split(Level.Solids)) {
                var lst = new List<char>();
                tweakable.Add(lst);
                foreach (var ch in line) {
                    lst.Add(ch);
                }
            }

            void setTile(int x, int y, char tile) {
                while (y >= tweakable.Count) {
                    tweakable.Add(new List<char>());
                }

                while (x >= tweakable[y].Count) {
                    tweakable[y].Add('0');
                }

                tweakable[y][x] = tile;
            }

            foreach (var tweak in config.Tweaks ?? new List<RandoConfigEdit>()) {
                if (tweak.Name == "fgTiles") {
                    setTile((int)tweak.X, (int)tweak.Y, tweak.Update.Tile);
                }
            }

            Level.Solids = string.Join("\n", tweakable.Select(line => string.Join("", line)));
        }

        private void ProcessSubroom(StaticNode node, RandoConfigRoom config) {
            if (node.Name != "main" && config.Tweaks != null) {
                throw new Exception("Config error: you have a subroom with tweaks in it");
            }

            foreach (RandoConfigHole holeConfig in config.Holes ?? new List<RandoConfigHole>()) {
                if (holeConfig.New) {
                    if (holeConfig.LowBound == null || holeConfig.HighBound == null) {
                        throw new Exception("Config error: new hole missing LowBound/HighBound");
                    }

                    if (holeConfig.Kind == HoleKind.None) {
                        throw new Exception("You probably didn't mean to add a new hole with kind None");
                    }

                    var hole = new Hole(holeConfig.Side, holeConfig.LowBound.Value, holeConfig.HighBound.Value, holeConfig.HighOpen ?? false) {
                        Launch = holeConfig.Launch,
                        Kind = holeConfig.Kind,
                    };
                    this.Holes.Add(hole);
                    node.Edges.Add(new StaticEdge {
                        FromNode = node,
                        HoleTarget = hole,
                        ReqIn = this.ProcessReqs(holeConfig.ReqIn, hole, false),
                        ReqOut = this.ProcessReqs(holeConfig.ReqOut, hole, true),
                    });
                    continue;
                }
                Hole matchedHole = null;
                var remainingMatches = holeConfig.Idx;
                foreach (var hole in this.Holes.Where(hole => hole.Side == holeConfig.Side)) {
                    if (remainingMatches == 0) {
                        matchedHole = hole;
                        break;
                    }
                    remainingMatches--;
                }

                if (matchedHole == null) {
                    throw new Exception($"Could not find the hole identified by area:{this.Area} room:{config.Room} side:{holeConfig.Side} idx:{holeConfig.Idx}");
                }

                //Logger.Log("randomizer", $"Matching {roomConfig.Room} {holeConfig.Side} {holeConfig.Idx} to {matchedHole}");
                matchedHole.Kind = holeConfig.Kind;
                matchedHole.Launch = holeConfig.Launch;
                if (holeConfig.LowBound != null) {
                    matchedHole.LowBound = holeConfig.LowBound.Value;
                }
                if (holeConfig.HighBound != null) {
                    matchedHole.HighBound = holeConfig.HighBound.Value;
                }
                if (holeConfig.HighOpen != null) {
                    matchedHole.HighOpen = holeConfig.HighOpen.Value;
                }

                if (holeConfig.Kind != HoleKind.None) {
                    node.Edges.Add(new StaticEdge {
                        FromNode = node,
                        HoleTarget = matchedHole,
                        ReqIn = this.ProcessReqs(holeConfig.ReqIn, matchedHole, false),
                        ReqOut = this.ProcessReqs(holeConfig.ReqOut, matchedHole, true),
                    });
                }
                
                if (holeConfig.Split != null) {
                    matchedHole.HighOpen = true;
                    
                    var hole = new Hole(matchedHole.Side, 0, matchedHole.HighBound, false) {
                        Launch = holeConfig.Split.Launch,
                        Kind = holeConfig.Split.Kind,
                    };
                    this.Holes.Add(hole);
                    node.Edges.Add(new StaticEdge {
                        FromNode = node,
                        HoleTarget = hole,
                        ReqIn = this.ProcessReqs(holeConfig.Split.ReqIn, hole, false),
                        ReqOut = this.ProcessReqs(holeConfig.Split.ReqOut, hole, true),
                    });
                }
            }

            foreach (var edge in config.InternalEdges ?? new List<RandoConfigInternalEdge>()) {
                StaticNode toNode;
                if (edge.Warp != null) {
                    // deal with this later
                    node.WarpConfig.Add(edge);
                    continue;
                } else if (edge.To != null) {
                    toNode = this.Nodes[edge.To];
                } else if (edge.CustomWarp) {
                    toNode = null;
                } else if (edge.Split != null) {
                    if (node.Edges.Count != 2) {
                        throw new Exception($"[{this.Name}.{node.Name}] Cannot split: must have exactly two edges");
                    }

                    toNode = new StaticNode() {
                        Name = node.Name + "_autosplit",
                        ParentRoom = node.ParentRoom
                    };
                    if (node.ParentRoom.Nodes.ContainsKey(toNode.Name)) {
                        throw new Exception($"[{this.Name}.{node.Name}] You may only autosplit a room once");
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
                        throw new Exception($"[{this.Name}.{node.Name}] You may only autosplit a room once");
                    }
                    node.ParentRoom.Nodes[toNode.Name] = toNode;

                    var thing = this.Collectables[edge.Collectable.Value];
                    if (thing.ParentNode != null) {
                        throw new Exception($"[{this.Name}.{node.Name}] Can only assign a collectable to one owner");
                    }
                    thing.ParentNode = toNode;
                    toNode.Collectables.Add(thing); 
                } else {
                    throw new Exception($"[{this.Name}.{node.Name}] Internal edge must have either To or Split or Collectable or CustomWarp");
                }

                var reqIn = this.ProcessReqs(edge.ReqIn, null, false);
                var reqOut = this.ProcessReqs(edge.ReqOut, null, true);

                var forward = new StaticEdge() {
                    FromNode = node,
                    NodeTarget = toNode,
                    ReqIn = reqIn,
                    ReqOut = reqOut,
                    CustomWarp = edge.CustomWarp,
                };

                node.Edges.Add(forward);
                if (toNode != null) {
                    var reverse = new StaticEdgeReversed(forward, node);
                    toNode.Edges.Add(reverse);
                }
            }

            foreach (var col in config.Collectables) {
                if (col.Idx != null) {
                    var thing = this.Collectables[col.Idx.Value];
                    if (thing.ParentNode != null) {
                        throw new Exception($"[{this.Name}.{node.Name}] Can only assign a collectable to one owner");
                    }
                    thing.ParentNode = node;
                    thing.MustFly = col.MustFly;
                    node.Collectables.Add(thing);
                } else if (col.X != null && col.Y != null) {
                    node.Collectables.Add(new StaticCollectable {
                        ParentNode = node,
                        Position = new Vector2(col.X.Value, col.Y.Value),
                        MustFly = col.MustFly
                    });
                } else {
                    throw new Exception($"[{this.Name}.{node.Name}] Collectable must specify Idx or X/Y");
                }
            }
        }

        private Requirement ProcessReqs(RandoConfigReq config) {
            return this.ProcessReqs(config, null, false);
        }

        private Requirement ProcessReqs(RandoConfigReq config, Hole matchHole, bool isOut) {
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
                if (config.KeyholeID == null) {
                    throw new Exception("Config error: Key: true without KeyholeID");
                }
                conjunction.Add(new KeyRequirement(config.KeyholeID.Value));
            }

            if (conjunction.Count == 0) {
                throw new Exception("this should be unreachable");
            } else if (conjunction.Count == 1) {
                return conjunction[0];
            }
            return new Conjunction(conjunction);
        }

        public void ProcessWarps(Dictionary<string, StaticRoom> mapRooms) {
            foreach (var node in this.Nodes.Values) {
                foreach (var conf in node.WarpConfig) {
                    if (!mapRooms.TryGetValue(conf.Warp, out StaticRoom toRoom)) {
                        throw new Exception($"{this.Name}: could not find warp target {conf.Warp}");
                    }
                    if (!toRoom.Nodes.TryGetValue(conf.To ?? "main", out StaticNode toNode)) {
                        throw new Exception($"{this.Name}: warp target {conf.Warp} has no node {conf.To ?? "main"}");
                    }

                    var edge = new StaticEdge {
                        FromNode = node,
                        NodeTarget = toNode,
                        ReqOut = this.ProcessReqs(conf.ReqOut, null, true),
                        ReqIn = this.ProcessReqs(conf.ReqIn, null, false),
                    };
                    node.Edges.Add(edge);
                    var reverse = new StaticEdgeReversed(edge, node);
                    toNode.Edges.Add(reverse);
                }
            }
        }

        public LevelData MakeLevelData(Vector2 newPosition, int? nonce) {
            var result = this.Level.Copy();
            result.Name = nonce == null ? this.Name : this.Name + "/" + nonce.ToString();
            result.Position = newPosition;
            result.Music = "";
            result.DisableDownTransition = false;
            result.HasCheckpoint = false;

            if (this.CoreModes != null) {
                var newData = new DynData<LevelData>(result);
                newData.Set("coreModes", this.CoreModes);
            }

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
                    case "lightbeam":
                    case "detachfollowerstrigger":
                    case "lightfadetrigger":
                    case "bloomfadetrigger":
                    case "summitgem":
                    case "picoconsole":
                    case "acidhelper/advancedmusiclayerfadetrigger":
                        removals.Add(entity);
                        return;
                    case "finalboss":
                    case "badelineoldsite":
                    case "darkchaser":
                        if (this.Name == "Celeste/2-OldSite/A/3") break;  // allow the cutscene badeline to change the music
                        if (entity.Values == null) entity.Values = new Dictionary<string, object>();
                        entity.Values["canChangeMusic"] = false;
                        if (entity.Name == "finalBoss") { // big hack: disable vertical camera locking unless the room is very small horizontally
                            if (result.Bounds.Width > 376) {
                                entity.Values["cameraLockY"] = "false";
                            }
                        }
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
                                    entity.Values[kv.Key] = kv.Value;
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
                        return Tuple.Create(spawn, econfig.Update?.Default ?? false);
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

                // ReSharper disable once CompareOfFloatsByEqualityOperator
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

                        if (econfig.Update.Values != null) {
                            entity.Values = new Dictionary<string, object>();
                            foreach (var kv in econfig.Update.Values) {
                                entity.Values.Add(kv.Key, kv.Value);
                            }
                        }

                        if (econfig.Update.Nodes != null) {
                            entity.Nodes = new Vector2[econfig.Update.Nodes.Count];
                            foreach (var node in econfig.Update.Nodes) {
                                if (node.X == null || node.Y == null) {
                                    throw new Exception("Entity configures node without X or Y");
                                }
                                entity.Nodes[node.Idx] = new Vector2(node.X.Value, node.Y.Value);
                            }
                        }

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
        public List<RandoConfigInternalEdge> WarpConfig = new List<RandoConfigInternalEdge>();

        public override string ToString() {
            return $"{this.ParentRoom}:{this.Name}";
        }

        public StaticEdge WarpEdge =>
            new StaticEdge {
                FromNode = this,
                ReqIn = new Possible(),
                ReqOut = new Possible(),
            };
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
        public bool CustomWarp;

        public override string ToString() {
            if (this.HoleTarget != null) {
                return this.HoleTarget.ToString();
            } else if (this.CustomWarp) {
                return "CustomWarp";
            } else {
                return $"-> {this.NodeTarget}";
            }
        }
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
