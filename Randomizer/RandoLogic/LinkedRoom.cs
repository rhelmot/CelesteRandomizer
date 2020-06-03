using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public class LinkedMap {
        private List<LinkedRoom> Rooms = new List<LinkedRoom>();
        private LinkedRoom CachedHit;
        private int nonce;

        public bool AreaFree(Rectangle rect) {
            if (this.CachedHit != null && CachedHit.Bounds.Intersects(rect)) {
                return false;
            }

            foreach (var room in this.Rooms) {
                if (room.Bounds.Intersects(rect)) {
                    this.CachedHit = room;
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

        public void FillMap(MapData map) {
            foreach (var room in this.Rooms) {
                map.Levels.Add(room.Room.MakeLevelData(new Vector2(room.Bounds.Left, room.Bounds.Top), this.nonce++));
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
        public StaticRoom Room;
        public Dictionary<string, LinkedNode> Nodes;

        public LinkedRoom(StaticRoom Room, Vector2 Position) {
            this.Room = Room;
            this.Bounds = new Rectangle((int)Position.X, (int)Position.Y, Room.Level.Bounds.Width, Room.Level.Bounds.Height);
            this.Nodes = new Dictionary<string, LinkedNode>();
            foreach (var staticnode in Room.Nodes.Values) {
                var node = new LinkedNode() { Static = staticnode, Room = this };
                this.Nodes.Add(staticnode.Name, node);
            }
        }
    }

    public class LinkedNode {
        public StaticNode Static;
        public LinkedRoom Room;
        public List<LinkedEdge> Edges = new List<LinkedEdge>();

        public IEnumerable<LinkedNode> Successors(Capabilities caps, bool requireReverse, bool onlyInternal=false) {
            foreach (var iedge in this.Static.Edges) {
                if (iedge.NodeTarget == null) {
                    continue;
                }
                if (!iedge.ReqOut.Able(caps)) {
                    continue;
                }
                yield return this.Room.Nodes[iedge.NodeTarget.Name];
            }

            if (!onlyInternal) {
                foreach (var edge in this.Edges) {
                    var check1 = edge.CorrespondingEdge(this);
                    var check2 = edge.OtherEdge(this);

                    if (!check1.ReqOut.Able(caps) || !check2.ReqIn.Able(caps)) {
                        continue;
                    }
                    if (requireReverse && (!check1.ReqIn.Able(caps) || !check2.ReqOut.Able(caps))) {
                        continue;
                    }
                    yield return edge.OtherNode(this);
                }
            }
        }

        public IEnumerable<StaticEdge> UnlinkedEdges(Capabilities caps, bool requireReverse) {
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
                if (!staticedge.ReqOut.Able(caps)) {
                    continue;
                }
                if (requireReverse && !staticedge.ReqIn.Able(caps)) {
                    continue;
                }
                yield return staticedge;
            }
        }

        public HashSet<LinkedNode> Closure(Capabilities caps, bool requireReverse, bool onlyInternal=false) {
            var result = new HashSet<LinkedNode>();
            var queue = new Queue<LinkedNode>();
            void enqueue(LinkedNode node) {
                if (!result.Contains(node)) {
                    queue.Enqueue(node);
                    result.Add(node);
                }
            }
            enqueue(this);

            while (queue.Count != 0) {
                var item = queue.Dequeue();

                foreach (var succ in item.Successors(caps, requireReverse, onlyInternal)) {
                    enqueue(succ);
                }
            }

            return result;
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
}
