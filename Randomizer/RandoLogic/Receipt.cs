using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        public abstract class Receipt {
            public abstract void Undo();
        }

        public class StartRoomReceipt : Receipt {
            private RandoLogic Logic;
            public LinkedRoom NewRoom;

            public static StartRoomReceipt Do(RandoLogic logic, StaticRoom newRoomStatic) {
                var newRoom = new LinkedRoom(newRoomStatic, Vector2.Zero);

                logic.Map.AddRoom(newRoom);

                if (!logic.Settings.RepeatRooms) {
                    logic.RemainingRooms.Remove(newRoomStatic);
                }

                return new StartRoomReceipt {
                    Logic = logic,
                    NewRoom = newRoom,
                };
            }

            public override void Undo() {
                Logic.Map.RemoveRoom(NewRoom);

                if (!this.Logic.Settings.RepeatRooms) {
                    this.Logic.RemainingRooms.Add(this.NewRoom.Static);
                }
            }
        }

        public class ConnectAndMapReceipt : Receipt {
            public LinkedRoom NewRoom;
            private RandoLogic Logic;
            public LinkedEdge Edge;

            public static ConnectAndMapReceipt Do(RandoLogic logic, LinkedRoom fromRoom, StaticEdge fromEdge, StaticEdge toEdge) {
                var toRoomStatic = toEdge.FromNode.ParentRoom;

                if (fromEdge.HoleTarget == null || toEdge.HoleTarget == null) {
                    return null;
                }

                // this check is maaaaaybe the responsibility of the caller?
                // it is less the world of connection ability and more the world of logic
                if (toEdge.HoleTarget.Kind == HoleKind.Unknown && !logic.Settings.EnterUnknown) {
                    return null;
                }

                var newOffset = fromEdge.HoleTarget.Compatible(toEdge.HoleTarget);
                if (newOffset == Hole.INCOMPATIBLE) {
                    return null;
                }

                var newPosition = toRoomStatic.AdjacentPosition(fromRoom.Bounds, fromEdge.HoleTarget.Side, newOffset);
                var toRoom = new LinkedRoom(toRoomStatic, newPosition);
                if (!logic.Map.AreaFree(toRoom.Bounds)) {
                    return null;
                }

                logic.Map.AddRoom(toRoom);
                var newEdge = new LinkedEdge {
                    NodeA = fromRoom.Nodes[fromEdge.FromNode.Name],
                    NodeB = toRoom.Nodes[toEdge.FromNode.Name],
                    StaticA = fromEdge,
                    StaticB = toEdge,
                };
                newEdge.NodeA.Edges.Add(newEdge);
                newEdge.NodeB.Edges.Add(newEdge);

                if (!logic.Settings.RepeatRooms) {
                    logic.RemainingRooms.Remove(toRoomStatic);
                }

                return new ConnectAndMapReceipt {
                    NewRoom = toRoom,
                    Logic = logic,
                    Edge = newEdge,
                };
            }

            public override void Undo() {
                this.Logic.Map.RemoveRoom(this.NewRoom);
                this.Edge.NodeA.Edges.Remove(this.Edge);
                this.Edge.NodeB.Edges.Remove(this.Edge);

                if (!this.Logic.Settings.RepeatRooms) {
                    this.Logic.RemainingRooms.Add(this.NewRoom.Static);
                }
            }
        }
    }
}
