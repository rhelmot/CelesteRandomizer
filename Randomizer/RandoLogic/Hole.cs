using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public enum HoleKind {
        None, In, Out, InOut, Unknown
    }

    public static class HoleKindMethods {
        public static HoleKind FromString(String str) {
            switch (str.ToLower()) {
                case "none":
                    return HoleKind.None;
                case "in":
                    return HoleKind.In;
                case "out":
                    return HoleKind.Out;
                case "inout":
                    return HoleKind.InOut;
                default:
                    throw new Exception("Bad hole kind " + str);
            }
        }
    }

    public class Hole {
        public LevelData Level;
        public ScreenDirection Side;
        public HoleKind Kind = HoleKind.Unknown;
        public int LowBound;
        public int HighBound;
        public bool HighOpen;

        public bool LowOpen {
            get {
                return this.LowBound == 0;
            }
        }

        public bool BothOpen {
            get {
                return this.HighOpen && this.LowOpen;
            }
        }

        public bool HalfOpen {
            get {
                return this.LowOpen ^ this.HighOpen;
            }
        }

        public bool Closed {
            get {
                return !this.LowOpen && !this.HighOpen;
            }
        }

        public int Size {
            get {
                return this.HighBound - this.LowBound + 1;
            }
        }

        public ScreenDirection AlongDir {
            get {
                switch (this.Side) {
                    case ScreenDirection.Up:
                    case ScreenDirection.Down:
                        return ScreenDirection.Right;
                    case ScreenDirection.Left:
                    case ScreenDirection.Right:
                    default:
                        return ScreenDirection.Down;
                }
            }
        }

        public Vector2 LowCoord {
            get {
                Vector2 corner;
                switch (this.Side) {
                    case ScreenDirection.Up:
                        corner = new Vector2(Level.Bounds.Left + 4, Level.Bounds.Top - 5);
                        break;
                    case ScreenDirection.Left:
                        corner = new Vector2(Level.Bounds.Left - 5, Level.Bounds.Top + 4);
                        break;
                    case ScreenDirection.Down:
                        corner = new Vector2(Level.Bounds.Left + 4, Level.Bounds.Bottom + 4);
                        break;
                    case ScreenDirection.Right:
                    default:
                        corner = new Vector2(Level.Bounds.Right + 4, Level.Bounds.Top + 4);
                        break;
                }
                return corner + this.AlongDir.Unit() * this.LowBound * 8;
            }
        }

        public Vector2 HighCoord {
            get {
                return this.LowCoord + this.AlongDir.Unit() * 8 * (this.Size - 1);
            }
        }

        public Hole(LevelData Level, ScreenDirection Side, int LowBound, int HighBound, bool HighOpen) {
            this.Level = Level;
            this.Side = Side;
            this.LowBound = LowBound;
            this.HighBound = HighBound;
            this.HighOpen = HighOpen;
        }

        public override String ToString() {
            if (this.BothOpen) {
                return $"{this.Side} (-inf, inf)";
            } else if (this.LowOpen) {
                return $"{this.Side} (-inf, {this.HighBound}]";
            } else if (this.HighOpen) {
                return $"{this.Side} [{this.LowBound}, inf)";
            } else {
                return $"{this.Side} [{this.LowBound}, {this.HighBound}]";
            }
        }

        // negative MIN_INT is still MIN_INT
        public const int INCOMPATIBLE = -0x80000000;

        public int Compatible(Hole other) {
            int alignLow() {
                return this.LowBound - other.LowBound;
            }
            int alignHigh() {
                return this.HighBound - other.HighBound;
            }

            if (other.Side == ScreenDirection.Up && this.Side != ScreenDirection.Up && this.Side != ScreenDirection.Left) {
                return -other.Compatible(this);
            }
            if (other.Side == ScreenDirection.Left && this.Side != ScreenDirection.Left && this.Side != ScreenDirection.Up) {
                return -other.Compatible(this);
            }

            if (this.Side == ScreenDirection.Up && other.Side == ScreenDirection.Down) {
                // Vertical transitions
                if (this.BothOpen || other.BothOpen) {
                    // if either is open on both ends, they must line up perfectly
                    if (this.BothOpen && other.BothOpen && this.Size == other.Size) {
                        return 0;
                    }
                } else if (this.HalfOpen || other.HalfOpen) {
                    // if either is half-open, they must be the same half open
                    if (this.LowOpen == other.LowOpen) {
                        return this.LowOpen ? alignHigh() : alignLow();
                    }
                } else {
                    // Only remaining option is both closed. they must be the same size
                    if (this.Size == other.Size) {
                        return alignLow();
                    }
                }
            }
            if (this.Side == ScreenDirection.Left && other.Side == ScreenDirection.Right) {
                // Horizontal transitions
                if (this.HighOpen || other.HighOpen) {
                    // if either is open on the bottom, they both must be open on the bottom and we align the death planes
                    // this is kind of a questionable choice all around tbh
                    // maybe additionally restrict that sizes must be the same?
                    if (this.HighOpen && other.HighOpen) {
                        return alignHigh();
                    }
                } else if (this.LowOpen || other.LowOpen) {
                    // if either is open on the top, the other must also be open on the top OR it must be sufficiently tall
                    if (this.LowOpen && other.LowOpen) {
                        return alignHigh();
                    } else if (this.Closed && this.Size > 3) {
                        return alignHigh();
                    } else if (other.Closed && other.Size > 3) {
                        return alignHigh();
                    }
                } else {
                    // only remaining option is both closed. they must be the same size OR sufficiently tall
                    if (this.Size == other.Size) {
                        return alignHigh();
                    } else if (this.Size > 3 && other.Size > 3) {
                        return alignHigh();
                    }
                }
            }
            return INCOMPATIBLE;
        }
    }
}
