using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer
{
    public partial class RandoLogic
    {
        public abstract class Receipt
        {
            public abstract void Undo();
        }

        public class StartRoomReceipt : Receipt
        {
            private RandoLogic Logic;
            public LinkedRoom NewRoom;
            private List<LinkedRoom> ExtraRooms;

            public static StartRoomReceipt Do(RandoLogic logic, StaticRoom newRoomStatic)
            {
                Logger.Log("randomizer", $"Adding room {newRoomStatic.Name} at start");
                var newRoom = new LinkedRoom(newRoomStatic, Vector2.Zero);
                var extras = ConnectAndMapReceipt.WarpClosure(logic, newRoom.Nodes["main"]);

                logic.Map.AddRoom(newRoom);

                if (!logic.Settings.RepeatRooms)
                {
                    logic.RemainingRooms.Remove(newRoomStatic);
                    foreach (var extra in extras)
                    {
                        logic.RemainingRooms.Remove(extra.Static);
                    }
                }

                return new StartRoomReceipt
                {
                    Logic = logic,
                    NewRoom = newRoom,
                    ExtraRooms = extras,
                };
            }

            public override void Undo()
            {
                Logger.Log("randomizer", $"Undo: Adding room {NewRoom.Static.Name} at start");
                Logic.Map.RemoveRoom(NewRoom);
                foreach (var room in this.ExtraRooms)
                {
                    this.Logic.Map.RemoveRoom(room);
                }

                if (!this.Logic.Settings.RepeatRooms)
                {
                    this.Logic.RemainingRooms.Add(this.NewRoom.Static);
                    foreach (var room in this.ExtraRooms)
                    {
                        this.Logic.RemainingRooms.Add(room.Static);
                    }
                }
            }
        }

        public class ConnectAndMapReceipt : Receipt
        {
            public LinkedRoom NewRoom;
            private RandoLogic Logic;
            public LinkedEdge Edge;
            public LinkedNode EntryNode;
            private List<LinkedRoom> ExtraRooms;

            public static ConnectAndMapReceipt Do(RandoLogic logic, UnlinkedEdge fromEdge, StaticEdge toEdge, bool isBacktrack = false)
            {
                var toRoomStatic = toEdge.FromNode.ParentRoom;
                var fromRoom = fromEdge.Node.Room;

                if (fromEdge.Static.HoleTarget == null || toEdge.HoleTarget == null)
                {
                    return null;
                }

                var newOffset = fromEdge.Static.HoleTarget.Compatible(toEdge.HoleTarget);
                if (newOffset == Hole.INCOMPATIBLE)
                {
                    return null;
                }

                var newPosition = toRoomStatic.AdjacentPosition(fromRoom.Bounds, fromEdge.Static.HoleTarget.Side, newOffset);
                var toRoom = new LinkedRoom(toRoomStatic, newPosition);
                if (!logic.Map.AreaFree(toRoom))
                {
                    return null;
                }
                toRoom.IsBacktrack = isBacktrack;
                logic.Map.AddRoom(toRoom);

                var extras = WarpClosure(logic, toRoom.Nodes[toEdge.FromNode.Name], isBacktrack);
                if (extras == null)
                {
                    logic.Map.RemoveRoom(toRoom);
                    return null;
                }

                var newEdge = new LinkedEdge
                {
                    NodeA = fromEdge.Node,
                    NodeB = toRoom.Nodes[toEdge.FromNode.Name],
                    StaticA = fromEdge.Static,
                    StaticB = toEdge,
                };
                newEdge.NodeA.Edges.Add(newEdge);
                newEdge.NodeB.Edges.Add(newEdge);

                if (!logic.Settings.RepeatRooms)
                {
                    logic.RemainingRooms.Remove(toRoomStatic);
                    foreach (var extra in extras)
                    {
                        logic.RemainingRooms.Remove(extra.Static);
                    }
                }

                Logger.Log("randomizer", $"Adding room {toRoomStatic.Name} at {newPosition} ({logic.Map.Count})");
                return new ConnectAndMapReceipt
                {
                    NewRoom = toRoom,
                    Logic = logic,
                    Edge = newEdge,
                    EntryNode = toRoom.Nodes[toEdge.FromNode.Name],
                    ExtraRooms = extras,
                };
            }

            public static ConnectAndMapReceipt DoWarp(RandoLogic logic, UnlinkedEdge fromEdge, StaticRoom toRoomStatic)
            {
                var fromRoom = fromEdge.Node.Room;
                if (!fromEdge.Static.CustomWarp)
                {
                    return null;
                }

                var toRoom = LinkRoomAnywhere(logic, fromRoom, toRoomStatic);
                var extras = WarpClosure(logic, toRoom.Nodes["main"]);
                if (extras == null)
                {
                    return null;
                }

                var newEdge = new LinkedEdge
                {
                    NodeA = fromEdge.Node,
                    NodeB = toRoom.Nodes["main"],
                    StaticA = fromEdge.Static,
                    StaticB = toRoomStatic.Nodes["main"].WarpEdge,
                };
                newEdge.NodeA.Edges.Add(newEdge);
                newEdge.NodeB.Edges.Add(newEdge);

                if (!logic.Settings.RepeatRooms)
                {
                    logic.RemainingRooms.Remove(toRoomStatic);
                    foreach (var extra in extras)
                    {
                        logic.RemainingRooms.Remove(extra.Static);
                    }
                }

                Logger.Log("randomizer", $"Adding room {toRoomStatic.Name} at {toRoom.Position} ({logic.Map.Count})");
                return new ConnectAndMapReceipt
                {
                    NewRoom = toRoom,
                    Logic = logic,
                    Edge = newEdge,
                    EntryNode = toRoom.Nodes["main"],
                    ExtraRooms = extras,
                };
            }

            public override void Undo()
            {
                Logger.Log("randomizer", $"Undo: Adding room {NewRoom.Static.Name} at {NewRoom.Bounds}");
                this.Logic.Map.RemoveRoom(this.NewRoom);
                foreach (var room in this.ExtraRooms)
                {
                    this.Logic.Map.RemoveRoom(room);
                }
                this.Edge.NodeA.Edges.Remove(this.Edge);
                this.Edge.NodeB.Edges.Remove(this.Edge);

                if (!this.Logic.Settings.RepeatRooms)
                {
                    this.Logic.RemainingRooms.Add(this.NewRoom.Static);
                    foreach (var room in this.ExtraRooms)
                    {
                        this.Logic.RemainingRooms.Add(room.Static);
                    }
                }
            }

            public static LinkedRoom LinkRoomAnywhere(RandoLogic logic, LinkedRoom start, StaticRoom room, bool isBacktrack = false)
            {
                float jumpScale = 8 * 150;
                int jumpDir = 0;
                LinkedRoom toRoom = null;
                Vector2 newPosition;
                while (true)
                {
                    newPosition = start.Position + Vector2.UnitX * jumpScale * (jumpDir == 0 ? 1 : jumpDir == 1 ? -1 : 0)
                                                    + Vector2.UnitY * jumpScale * (jumpDir == 2 ? 1 : jumpDir == 3 ? -1 : 0);
                    toRoom = new LinkedRoom(room, newPosition);
                    toRoom.IsBacktrack = isBacktrack;

                    if (logic.Map.AreaFree(toRoom))
                    {
                        break;
                    }

                    if (++jumpDir == 4)
                    {
                        jumpDir = 0;
                        jumpScale *= 2f;
                    }
                }

                logic.Map.AddRoom(toRoom);
                return toRoom;
            }

            public static List<LinkedRoom> WarpClosure(RandoLogic logic, LinkedNode start, bool isBacktrack = false)
            {
                var startRoom = start.Static.ParentRoom;
                var queue = new Queue<StaticNode>();
                queue.Enqueue(start.Static);
                var seen = new HashSet<StaticNode> { start.Static };

                while (queue.Count != 0)
                {
                    var next = queue.Dequeue();
                    foreach (var edge in next.Edges)
                    {
                        if (edge.NodeTarget == null)
                        {
                            continue;
                        }
                        if (seen.Contains(edge.NodeTarget))
                        {
                            continue;
                        }
                        queue.Enqueue(edge.NodeTarget);
                        seen.Add(edge.NodeTarget);
                    }
                }

                // linq is a blessing
                var newRooms = new List<StaticRoom>(seen.Select(node => node.ParentRoom).Distinct().Where(room => room != startRoom));
                foreach (var room in newRooms)
                {
                    if (!logic.RemainingRooms.Contains(room))
                    {
                        return null;
                    }
                }

                var result = new List<LinkedRoom>();
                var resultMap = new Dictionary<string, LinkedRoom> { { startRoom.Level.Name, start.Room } };
                var lastRoom = start.Room;
                lastRoom.WarpMap = resultMap;
                foreach (var room in newRooms)
                {
                    var linkedRoom = LinkRoomAnywhere(logic, lastRoom, room);
                    linkedRoom.WarpMap = resultMap;
                    result.Add(linkedRoom);
                    resultMap[room.Level.Name] = linkedRoom;
                }

                if (result.Count == 0)
                {
                    start.Room.WarpMap = null;
                }
                return result;
            }
        }

        public class PlaceCollectableReceipt : Receipt
        {
            private LinkedNode Node;
            private StaticCollectable Place;
            private int? KeyholeID;
            private LinkedRoom KeyholeRoom;

            public static PlaceCollectableReceipt Do(LinkedNode node, StaticCollectable place, LinkedCollectable item, bool autoBubble)
            {
                Logger.Log("randomizer", $"Placing collectable {item} in {node.Room.Static.Name}:{node.Static.Name}");
                node.Collectables[place] = Tuple.Create(item, autoBubble);
                return new PlaceCollectableReceipt
                {
                    Node = node,
                    Place = place,
                };
            }

            public static PlaceCollectableReceipt Do(LinkedNode node, StaticCollectable place, LinkedCollectable item, bool autoBubble, int keyholeID, LinkedRoom keyholeRoom)
            {
                var result = Do(node, place, item, autoBubble);
                keyholeRoom.UsedKeyholes.Add(keyholeID);
                result.KeyholeID = keyholeID;
                result.KeyholeRoom = keyholeRoom;
                return result;
            }

            public override void Undo()
            {
                Logger.Log("randomizer", $"Undo: Placing collectable in {Node.Room.Static.Name}:{Node.Static.Name}");
                this.Node.Collectables.Remove(this.Place);
                if (this.KeyholeID != null)
                {
                    this.KeyholeRoom.UsedKeyholes.Remove(this.KeyholeID.Value);
                }
            }
        }
    }
}
