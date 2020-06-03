using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public enum ScreenDirection {
        Up, Down, Left, Right
    }

    public static class ScreenDirectionMethods {
        public static Vector2 Unit(this ScreenDirection self) {
            switch (self) {
                case ScreenDirection.Up:
                    return -Vector2.UnitY;
                case ScreenDirection.Down:
                    return Vector2.UnitY;
                case ScreenDirection.Left:
                    return -Vector2.UnitX;
                case ScreenDirection.Right:
                    return Vector2.UnitX;
                default:
                    return Vector2.Zero;
            }
        }

        public static ScreenDirection RotCW(this ScreenDirection self) {
            switch (self) {
                case ScreenDirection.Up:
                    return ScreenDirection.Right;
                case ScreenDirection.Down:
                    return ScreenDirection.Left;
                case ScreenDirection.Left:
                    return ScreenDirection.Up;
                case ScreenDirection.Right:
                    return ScreenDirection.Down;
                default:
                    return ScreenDirection.Up;
            }
        }

        public static ScreenDirection Opposite(this ScreenDirection self) {
            switch (self) {
                case ScreenDirection.Up:
                    return ScreenDirection.Down;
                case ScreenDirection.Down:
                    return ScreenDirection.Up;
                case ScreenDirection.Left:
                    return ScreenDirection.Right;
                case ScreenDirection.Right:
                default:
                    return ScreenDirection.Left;
            }
        }

        public static ScreenDirection FromString(String str) {
            switch (str.ToLower()) {
                case "up":
                    return ScreenDirection.Up;
                case "down":
                    return ScreenDirection.Down;
                case "left":
                    return ScreenDirection.Left;
                case "right":
                    return ScreenDirection.Right;
                default:
                    throw new Exception("Bad ScreenDirection " + str);
            }
        }
    }
}
