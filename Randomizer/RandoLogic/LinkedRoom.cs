using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer {
    public class LinkedMap {
        private List<LinkedRoom> Rooms = new List<LinkedRoom>();
        private LinkedRoom CachedHit;
        private int nonce;

        public bool AreaFree(Rectangle rect) {
            if (this.CachedHit != null) {
                if (CachedHit.Bounds.Intersects(rect)) {
                    return false;
                }
                foreach (var r in CachedHit.ExtraBounds) {
                    if (r.Intersects(rect)) {
                        return false;
                    }
                }
            }

            foreach (var room in this.Rooms) {
                if (room.Bounds.Intersects(rect)) {
                    this.CachedHit = room;
                    return false;
                }
                foreach (var r in room.ExtraBounds) {
                    if (r.Intersects(rect)) {
                        this.CachedHit = room;
                        return false;
                    }
                }
            }

            return true;
        }

        public bool AreaFree(LinkedRoom room) {
            if (!this.AreaFree(room.Bounds)) {
                return false;
            }
            foreach (var r in room.ExtraBounds) {
                if (!this.AreaFree(r)) {
                    return false;
                }
            }
            return true;
        }

        public bool HoleFree(LinkedRoom level, Hole hole) {
            Vector2 outward = hole.Side.Unit() * (hole.Side == ScreenDirection.Up || hole.Side == ScreenDirection.Down ? 180 : 320);
            Vector2 pt1v = hole.LowCoord(level.Bounds) + outward;
            Vector2 pt2v = hole.HighCoord(level.Bounds) + outward;
            Point pt1 = new Point((int)pt1v.X, (int)pt1v.Y);
            Point pt2 = new Point((int)pt2v.X, (int)pt2v.Y);

            foreach (var room in this.Rooms) {
                if (room.Bounds.Contains(pt1)) {
                    return false;
                }
            }
            foreach (var room in this.Rooms) {
                if (room.Bounds.Contains(pt2)) {
                    return false;
                }
            }
            return true;
        }

        public void AddRoom(LinkedRoom room) {
            this.Rooms.Add(room);
        }

        public void RemoveRoom(LinkedRoom room) {
            this.Rooms.Remove(room);
            if (room == this.CachedHit) {
                this.CachedHit = null;
            }
        }

        public void FillMap(MapData map, Random random) {
            foreach (var room in this.Rooms) {
                map.Levels.Add(room.Bake(this.nonce++, random));
            }
        }

        public int Count {
            get {
                return this.Rooms.Count;
            }
        }
    }

    public class LinkedRoom {
        public Rectangle Bounds;
        public List<Rectangle> ExtraBounds;
        public StaticRoom Static;
        public Dictionary<string, LinkedNode> Nodes = new Dictionary<string, LinkedNode>();
        public HashSet<int> UsedKeyholes = new HashSet<int>();

        public LinkedRoom(StaticRoom Room, Vector2 Position) {
            this.Static = Room;
            this.Bounds = new Rectangle((int)Position.X, (int)Position.Y, Room.Level.Bounds.Width, Room.Level.Bounds.Height);
            this.ExtraBounds = new List<Rectangle>();
            foreach (var r in Room.ExtraSpace) {
                this.ExtraBounds.Add(new Rectangle((int)Position.X + r.X, (int)Position.Y + r.Y, r.Width, r.Height));
            }
            foreach (var staticnode in Room.Nodes.Values) {
                var node = new LinkedNode() { Static = staticnode, Room = this };
                this.Nodes.Add(staticnode.Name, node);
            }
        }

        public virtual LevelData Bake(int? nonce, Random random) {
            var result = this.Static.MakeLevelData(new Vector2(this.Bounds.Left, this.Bounds.Top), nonce);

            bool ohgodwhat = random.Next(100) == 0; // :)
            string pickCrystalColor() {
                tryagain:
                switch (random.Next(14)) {
                    case 0:
                    case 1:
                    case 2:
                        return "blue";
                    case 3:
                    case 4:
                    case 5:
                        return "red";
                    case 6:
                    case 7:
                    case 8:
                        return "purple";
                    case 9:
                        return "rainbow";
                    default:
                        // dust bunnies can't be shattered, lmao
                        if (this.Static.Name == "Celeste/LostLevels/A/h-10") {
                            goto tryagain;
                        }
                        return "dust";
                }
            }
            string crystalcolor = pickCrystalColor();

            string pickSpinnerColor() {
                return new string[] { "dust", "spike", "star" }[random.Next(3)];
            }
            string spinnercolor = pickSpinnerColor();

            int maxID = 0;
            var toRemove = new List<EntityData>();
            foreach (var e in result.Entities) {
                maxID = Math.Max(maxID, e.ID);

                switch (e.Name) {
                    case "spinner":
                        if (ohgodwhat) {
                            crystalcolor = pickCrystalColor();
                        }
                        if (e.Values == null) e.Values = new Dictionary<string, object>();
                        if (crystalcolor == "dust") {
                            e.Values["dust"] = "true";
                        } else {
                            e.Values["color"] = crystalcolor;
                        }
                        break;
                    case "trackSpinner":
                    case "rotateSpinner":
                        if (ohgodwhat) {
                            spinnercolor = pickSpinnerColor();
                        }
                        if (e.Values == null) e.Values = new Dictionary<string, object>();
                        if (spinnercolor == "dust") {
                            e.Values["dust"] = "true";
                        } else if (crystalcolor == "star") {
                            e.Values["star"] = "true";
                        }
                        break;
                    case "lockBlock":
                        if (!this.UsedKeyholes.Contains(e.ID)) {
                            toRemove.Add(e);
                        }
                        break;
                }
            }
            foreach (var e in toRemove) {
                result.Entities.Remove(e);
            }

            void blockHole(Hole hole) {
                var topbottom = hole.Side == ScreenDirection.Up || hole.Side == ScreenDirection.Down;
                var farside = hole.Side == ScreenDirection.Down || hole.Side == ScreenDirection.Right;

                Vector2 corner;
                switch (hole.Side) {
                    case ScreenDirection.Up:
                        corner = new Vector2(0, -8);
                        break;
                    case ScreenDirection.Left:
                        corner = new Vector2(-8, 0);
                        break;
                    case ScreenDirection.Down:
                        corner = new Vector2(0, this.Bounds.Height);
                        break;
                    case ScreenDirection.Right:
                    default:
                        corner = new Vector2(this.Bounds.Width, 0);
                        break;
                }
                corner += hole.AlongDir.Unit() * hole.LowBound * 8;
                var e = new EntityData {
                    ID = ++maxID,
                    Name = "invisibleBarrier",
                    Width = !topbottom ? 8 : hole.Size * 8,
                    Height = topbottom ? 8 : hole.Size * 8,
                    Position = corner,
                    Level = result,
                };
                result.Entities.Add(e);
            }

            void partiallyBlockHole(Hole hole, Hole hole2) {
                int diff = hole.Compatible(hole2);
                var newHighBound = diff + hole2.LowBound - 1;
                var newHole = new Hole(hole.Side, hole.LowBound, newHighBound, false);
                //Logger.Log("DEBUG", $"{result.Name}: special-blocking {newHole}");
                blockHole(newHole);
            }

            void beamHole(Hole hole) {
                Vector2 center;
                int rotation;
                switch (hole.Side) {
                    case ScreenDirection.Up:
                        center = new Vector2(0, 0);
                        rotation = 0;
                        break;
                    case ScreenDirection.Left:
                        center = new Vector2(0, 0);
                        rotation = 270;
                        break;
                    case ScreenDirection.Down:
                        center = new Vector2(0, this.Bounds.Height);
                        rotation = 180;
                        break;
                    case ScreenDirection.Right:
                    default:
                        center = new Vector2(this.Bounds.Width, 0);
                        rotation = 90;
                        break;
                }
                center += hole.AlongDir.Unit() * (hole.LowBound * 8 + hole.Size * 4);
                var e = new EntityData {
                    ID = ++maxID,
                    Name = "lightbeam",
                    Width = hole.Size * 8,
                    Height = 24,
                    Position = center,
                    Level = result,
                    Values = new Dictionary<string, object> {
                        {"rotation", (object)rotation},
                    }
                };
                result.Entities.Add(e);
            }

            void gateTopHole(Hole hole) {
                if (!hole.LowOpen) {
                    var coord = (hole.LowBound - 1) * 8;
                    var e = new EntityData {
                        Name = "invisibleBarrier",
                        Position = new Vector2(coord, -80),
                        Width = 8,
                        Height = 80,
                        Level = result,
                        ID = ++maxID,
                    };
                    result.Entities.Add(e);
                }
                if (!hole.HighOpen) {
                    var coord = (hole.HighBound + 1) * 8;
                    var e = new EntityData {
                        Name = "invisibleBarrier",
                        Position = new Vector2(coord, -80),
                        Width = 8,
                        Height = 80,
                        Level = result,
                        ID = ++maxID,
                    };
                    result.Entities.Add(e);
                }
            }

            bool disableDown = true;
            bool disableUp = true;
            var unusedHorizontalHoles = new HashSet<Hole>();
            var unusedTopHoles = new HashSet<Hole>();
            foreach (var hole in this.Static.Holes) {
                if (hole.Side == ScreenDirection.Left || hole.Side == ScreenDirection.Right) {
                    unusedHorizontalHoles.Add(hole);
                }
                if (hole.Side == ScreenDirection.Up) {
                    unusedTopHoles.Add(hole);
                }
            }
            foreach (var node in this.Nodes.Values) {
                foreach (var edge in node.Edges) {
                    var hole = edge.CorrespondingEdge(node).HoleTarget;
                    var hole2 = edge.OtherEdge(node).HoleTarget;
                    if (hole != null && hole.Side == ScreenDirection.Down) {
                        disableDown = false;
                    }
                    if (hole != null && hole.Side == ScreenDirection.Up) {
                        disableUp = false;
                    }
                    if (hole != null && (hole.Side == ScreenDirection.Left || hole.Side == ScreenDirection.Right)) {
                        unusedHorizontalHoles.Remove(hole);
                    }
                    if (hole != null && hole.Side == ScreenDirection.Up) {
                        unusedTopHoles.Remove(hole);
                    }

                    // Block off holes connected to edges which should not be re-entered
                    if (hole != null && hole2 != null && hole2.Kind == HoleKind.Out) {
                        blockHole(hole);
                    }
                    // Block off pieces of holes which are partially unused
                    if (hole != null && hole2 != null && hole.Size > hole2.Size) {
                        partiallyBlockHole(hole, hole2);
                    }
                    // Add beams
                    var shine = RandoModule.Instance.Settings.Lights;
                    if ((shine == ShineLights.On || (shine == ShineLights.Hubs && this.Static.Hub)) &&
                        hole != null && hole2 != null &&
                        (hole.Kind == HoleKind.Out || hole.Kind == HoleKind.InOut) &&
                        (hole2.Kind == HoleKind.In || hole2.Kind == HoleKind.Unknown || hole2.Kind == HoleKind.InOut)) {
                        beamHole(hole);
                    }
                }

                foreach (var kv in node.Collectables) {
                    string name = null;
                    switch (kv.Value) {
                        case LinkedNode.LinkedCollectable.Key:
                            name = "key";
                            break;
                        case LinkedNode.LinkedCollectable.Strawberry:
                        case LinkedNode.LinkedCollectable.WingedStrawberry:
                            name = "strawberry";
                            break;
                    }

                    var e = new EntityData {
                        ID = ++maxID,
                        Name = name,
                        Level = result,
                        Position = kv.Key.Position,
                    };
                    if (kv.Value == LinkedNode.LinkedCollectable.WingedStrawberry) {
                        e.Values["winged"] = "true";
                    }
                    result.Entities.Add(e);
                }
            }

            result.DisableDownTransition = disableDown;
            if (disableUp) {
                new DynData<LevelData>(result).Set("DisableUpTransition", true);
            }
            foreach (var hole in unusedHorizontalHoles) {
                blockHole(hole);
            }
            foreach (var hole in unusedTopHoles) {
                gateTopHole(hole);
            }
            return result;
        }
    }

    public class LinkedNode : IComparable<LinkedNode>, IComparable {
        public StaticNode Static;
        public LinkedRoom Room;
        public List<LinkedEdge> Edges = new List<LinkedEdge>();
        public Dictionary<StaticCollectable, LinkedCollectable> Collectables = new Dictionary<StaticCollectable, LinkedCollectable>();

        public int CompareTo(LinkedNode obj) {
            return 0;
        }

        public int CompareTo(object obj) {
            if (!(obj is LinkedNode other)) {
                throw new ArgumentException("Must compare LinkedNode to LinkedNode");
            }
            return this.CompareTo(other);
        }

        public enum LinkedCollectable {
            Strawberry,
            WingedStrawberry,
            Key
        }

        public IEnumerable<LinkedNode> Successors(Capabilities capsForward, Capabilities capsReverse, bool onlyInternal = false) {
            foreach (var iedge in this.Static.Edges) {
                if (iedge.NodeTarget == null) {
                    continue;
                }
                if (capsForward != null && !iedge.ReqOut.Able(capsForward)) {
                    continue;
                }
                if (capsReverse != null && !iedge.ReqIn.Able(capsReverse)) {
                    continue;
                }
                yield return this.Room.Nodes[iedge.NodeTarget.Name];
            }

            if (!onlyInternal) {
                foreach (var edge in this.Edges) {
                    var check1 = edge.CorrespondingEdge(this);
                    var check2 = edge.OtherEdge(this);

                    if (capsForward != null && (!check1.ReqOut.Able(capsForward) || !check2.ReqIn.Able(capsForward))) {
                        continue;
                    }
                    if (capsReverse != null && (!check1.ReqIn.Able(capsReverse) || !check2.ReqOut.Able(capsReverse))) {
                        continue;
                    }
                    yield return edge.OtherNode(this);
                }
            }
        }

        public IEnumerable<Tuple<LinkedNode, Requirement>> SuccessorsRequires(Capabilities capsForward, bool onlyInternal = false) {
            foreach (var iedge in this.Static.Edges) {
                if (iedge.NodeTarget == null) {
                    continue;
                }
                var reqs = iedge.ReqOut.Conflicts(capsForward);
                if (reqs is Impossible) {
                    continue;
                }

                yield return Tuple.Create(this.Room.Nodes[iedge.NodeTarget.Name], reqs);
            }

            if (!onlyInternal) {
                foreach (var edge in this.Edges) {
                    var check1 = edge.CorrespondingEdge(this);
                    var check2 = edge.OtherEdge(this);

                    var reqs = Requirement.And(new List<Requirement> { check1.ReqOut.Conflicts(capsForward), check2.ReqIn.Conflicts(capsForward) });
                    if (reqs is Impossible) {
                        continue;
                    }
                    yield return Tuple.Create(edge.OtherNode(this), reqs);
                }
            }
        }

        public IEnumerable<StaticEdge> UnlinkedEdges(Capabilities capsForward, Capabilities capsReverse) {
            foreach (var staticedge in this.Static.Edges) {
                if (staticedge.HoleTarget == null) {
                    continue;
                }
                bool found = false;
                foreach (var linkededge in this.Edges) {
                    if (linkededge.CorrespondingEdge(this) == staticedge) {
                        found = true;
                        break;
                    }
                }
                if (found) {
                    continue;
                }
                if (capsForward != null && !staticedge.ReqOut.Able(capsForward)) {
                    continue;
                }
                if (capsReverse != null && !staticedge.ReqIn.Able(capsReverse)) {
                    continue;
                }
                yield return staticedge;
            }
        }

        public IEnumerable<StaticCollectable> UnlinkedCollectables() {
            foreach (var c in this.Static.Collectables) {
                if (!this.Collectables.ContainsKey(c)) {
                    yield return c;
                }
            }
        }
    }

    public class LinkedEdge {
        public StaticEdge StaticA, StaticB;
        public LinkedNode NodeA, NodeB;

        public LinkedNode OtherNode(LinkedNode One) {
            return One == NodeA ? NodeB : One == NodeB ? NodeA : null;
        }

        public StaticEdge CorrespondingEdge(LinkedNode One) {
            return One == NodeA ? StaticA : One == NodeB ? StaticB : null;
        }

        public StaticEdge OtherEdge(LinkedNode One) {
            return One == NodeA ? StaticB : One == NodeB ? StaticA : null;
        }
    }

    public class UnlinkedEdge {
        public StaticEdge Static;
        public LinkedNode Node;
    }

    public class UnlinkedCollectable {
        public StaticCollectable Static;
        public LinkedNode Node;
    }

    public class LinkedNodeSet {
        private List<LinkedNode> Nodes;
        private Capabilities CapsForward;
        private Capabilities CapsReverse;
        private Random Random;

        public static LinkedNodeSet Closure(LinkedNode start, Capabilities capsForward, Capabilities capsReverse, bool internalOnly) {
            var result = new HashSet<LinkedNode>();
            var queue = new Queue<LinkedNode>();
            void enqueue(LinkedNode node) {
                if (!result.Contains(node)) {
                    queue.Enqueue(node);
                    result.Add(node);
                }
            }
            enqueue(start);

            while (queue.Count != 0) {
                var item = queue.Dequeue();

                foreach (var succ in item.Successors(capsForward, capsReverse, internalOnly)) {
                    enqueue(succ);
                }
            }

            return new LinkedNodeSet(result) {
                CapsForward = capsForward,
                CapsReverse = capsReverse,
            };
        }

        public static Requirement TraversalRequires(LinkedNode start, Capabilities capsForward, bool internalOnly, UnlinkedEdge end) {
            return Requirement.And(new List<Requirement> { TraversalRequires(start, capsForward, internalOnly, end.Node),
                                                           end.Static.ReqOut.Conflicts(capsForward)
            });
        }

        public static Requirement TraversalRequires(LinkedNode start, Capabilities capsForward, bool internalOnly, LinkedNode end) {
            var queue = new PriorityQueue<Tuple<Requirement, LinkedNode>>();
            var seen = new Dictionary<LinkedNode, List<Requirement>>();

            Requirement p = new Possible();
            queue.Enqueue(Tuple.Create(p, start));
            seen[start] = new List<Requirement> { p };

            // implementation question: should this loop break as soon as one path to end is found, or should it exhaust the queue?
            // for now, let's go with exhaust the queue so if there's a Possible we don't miss it

            while (queue.Count != 0) {
                var entry = queue.Dequeue();
                var entryReq = entry.Item1;
                var entryNode = entry.Item2;

                foreach (var where in entryNode.SuccessorsRequires(capsForward)) {
                    var realReq = Requirement.And(new List<Requirement> { entryReq, where.Item2 });
                    var nextNode = where.Item1;

                    if (!seen.TryGetValue(nextNode, out var seenLst)) {
                        seenLst = new List<Requirement>();
                        seen[nextNode] = seenLst;
                    }

                    // search for any requirement already seen which obsoletes this new requirement
                    var found = false;
                    foreach (var req in seenLst) {
                        if (req.Equals(realReq) || req.StrictlyBetterThan(realReq)) {
                            found = true;
                            break;
                        }
                    }
                    if (!found) {
                        seenLst.Add(realReq);
                        queue.Enqueue(Tuple.Create(realReq, nextNode));
                    }
                }
            }

            if (!seen.TryGetValue(end, out var disjunct)) {
                return new Impossible();
            }

            return Requirement.Or(disjunct);
        }

        private LinkedNodeSet(List<LinkedNode> nodes) {
            this.Nodes = nodes;
        }

        private LinkedNodeSet(IEnumerable<LinkedNode> nodes) : this(new List<LinkedNode>(nodes)) { }

        public LinkedNodeSet Shuffle(Random random) {
            this.Random = random;
            return this;
        }

        public List<UnlinkedEdge> UnlinkedEdges(Func<UnlinkedEdge, bool> filter=null) {
            var result = new List<UnlinkedEdge>();
            foreach (var node in this.Nodes) {
                foreach (var edge in node.UnlinkedEdges(this.CapsForward, this.CapsReverse)) {
                    var uEdge = new UnlinkedEdge { Node = node, Static = edge };
                    if (filter != null && !filter(uEdge)) {
                        continue;
                    }
                    result.Add(uEdge);
                }
            }
            if (this.Random != null) {
                result.Shuffle(this.Random);
            }
            return result;
        }

        public List<UnlinkedCollectable> UnlinkedCollectables() {
            var result = new List<UnlinkedCollectable>();
            foreach (var node in this.Nodes) {
                foreach (var col in node.UnlinkedCollectables()) {
                    result.Add(new UnlinkedCollectable { Node = node, Static = col });
                }
            }
            if (this.Random != null) {
                result.Shuffle(this.Random);
            }
            return result;
        }
    }
}
