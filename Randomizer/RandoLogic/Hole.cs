using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer
{
    public enum HoleKind
    {
        None, In, Out, InOut, Unknown
    }

    public enum HoleObjective
    {
        Progression, Strawberry, Key, Gem, Flag,  
    }

    public static class HoleKindMethods
    {
        public static HoleKind FromString(String str)
        {
            switch (str.ToLower())
            {
                case "none":
                    return HoleKind.None;
                case "in":
                    return HoleKind.In;
                case "out":
                    return HoleKind.Out;
                case "inout":
                    return HoleKind.InOut;
                case "unknown":
                    return HoleKind.Unknown;
                default:
                    throw new Exception("Bad hole kind " + str);
            }
        }
    }

    public static class HoleObjectiveMethoids
    {
        public static HoleObjective FromString(String str)
        {
            switch (str.ToLower())
            {
                case "progression":
                    return HoleObjective.Progression;
                case "strawberry":
                    return HoleObjective.Strawberry;
                case "key":
                    return HoleObjective.Key;
                case "gem":
                    return HoleObjective.Gem;
                case "flag":
                    return HoleObjective.Flag;
                default:
                    throw new Exception("Bad hole objective " + str);
            }
        }
    }

    public class Hole
    {
        public ScreenDirection Side;
        public HoleKind Kind = HoleKind.Unknown;
        public HoleObjective Objective = HoleObjective.Progression;
        public int LowBound;
        public int HighBound;
        public bool HighOpen;
        public int? Launch;

        public bool LowOpen
        {
            get
            {
                return this.LowBound == 0;
            }
        }

        public bool BothOpen
        {
            get
            {
                return this.HighOpen && this.LowOpen;
            }
        }

        public bool HalfOpen
        {
            get
            {
                return this.LowOpen ^ this.HighOpen;
            }
        }

        public bool Closed
        {
            get
            {
                return !this.LowOpen && !this.HighOpen;
            }
        }

        public int Size
        {
            get
            {
                return this.HighBound - this.LowBound + 1;
            }
        }

        public ScreenDirection AlongDir
        {
            get
            {
                switch (this.Side)
                {
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

        public Vector2 LowCoord(Rectangle levelBounds)
        {
            Vector2 corner;
            switch (this.Side)
            {
                case ScreenDirection.Up:
                    corner = new Vector2(levelBounds.Left + 4, levelBounds.Top - 5);
                    break;
                case ScreenDirection.Left:
                    corner = new Vector2(levelBounds.Left - 5, levelBounds.Top + 4);
                    break;
                case ScreenDirection.Down:
                    corner = new Vector2(levelBounds.Left + 4, levelBounds.Bottom + 4);
                    break;
                case ScreenDirection.Right:
                default:
                    corner = new Vector2(levelBounds.Right + 4, levelBounds.Top + 4);
                    break;
            }
            return corner + this.AlongDir.Unit() * this.LowBound * 8;
        }

        public Vector2 HighCoord(Rectangle levelBounds)
        {
            return this.LowCoord(levelBounds) + this.AlongDir.Unit() * 8 * (this.Size - 1);
        }

        public Hole(ScreenDirection Side, int LowBound, int HighBound, bool HighOpen)
        {
            this.Side = Side;
            this.LowBound = LowBound;
            this.HighBound = HighBound;
            this.HighOpen = HighOpen;
        }

        public override String ToString()
        {
            if (this.BothOpen)
            {
                return $"{this.Side} (-inf, inf)";
            }
            else if (this.LowOpen)
            {
                return $"{this.Side} (-inf, {this.HighBound}]";
            }
            else if (this.HighOpen)
            {
                return $"{this.Side} [{this.LowBound}, inf)";
            }
            else
            {
                return $"{this.Side} [{this.LowBound}, {this.HighBound}]";
            }
        }

        // negative MIN_INT is still MIN_INT
        public const int INCOMPATIBLE = int.MinValue;

        public int Compatible(Hole other)
        {
            int alignLow()
            {
                return this.LowBound - other.LowBound;
            }
            int alignHigh()
            {
                return this.HighBound - other.HighBound;
            }

            if (other.Side == ScreenDirection.Up && this.Side != ScreenDirection.Up && this.Side != ScreenDirection.Left)
            {
                return -other.Compatible(this);
            }
            if (other.Side == ScreenDirection.Left && this.Side != ScreenDirection.Left && this.Side != ScreenDirection.Up)
            {
                return -other.Compatible(this);
            }

            if (this.Side == ScreenDirection.Up && other.Side == ScreenDirection.Down)
            {
                var oneWayUp = this.Kind == HoleKind.Out || other.Kind == HoleKind.In || other.Kind == HoleKind.Unknown;
                var oneWayDown = this.Kind == HoleKind.In || this.Kind == HoleKind.Unknown || other.Kind == HoleKind.Out;
                var topIsLarger = this.Size <= other.Size;
                // Vertical transitions
                if (this.Launch != null)
                {
                    if (other.Launch != null)
                    {
                        if (other.Launch == -1)
                        {
                            return INCOMPATIBLE;
                        }
                        return this.Launch.Value - other.Launch.Value;
                    }
                    else if (other.Closed)
                    {
                        return this.Launch.Value - (other.LowBound + other.HighBound) / 2;
                    }
                    else if (other.HighOpen)
                    {
                        return this.Launch.Value - (other.LowBound + 2);
                    }
                    else if (other.LowOpen)
                    {
                        return this.Launch.Value - (other.HighBound - 2);
                    }
                }
                else if (this.BothOpen || other.BothOpen)
                {
                    // if either is open on both ends, they must line up perfectly
                    if (this.BothOpen && other.BothOpen && this.Size == other.Size)
                    {
                        return 0;
                    }
                }
                else if (this.HalfOpen || other.HalfOpen)
                {
                    // if either is half-open, they must be the same half open
                    // alternatively, as long as they're not open on different sides (i.e. one is closed)
                    // then it's okay if it's one way and the target hole is larger
                    if (this.LowOpen == other.LowOpen)
                    {
                        return this.LowOpen ? alignHigh() : alignLow();
                    }
                    else if (this.LowOpen != other.HighOpen)
                    {
                        if ((oneWayUp && topIsLarger) || (oneWayDown && !topIsLarger))
                        {
                            return (this.LowOpen || other.LowOpen) ? alignHigh() : alignLow();
                        }
                    }
                }
                else
                {
                    // Only remaining option is both closed. they must be the same size
                    // alternately, same trick with oneways
                    if (this.Size == other.Size)
                    {
                        return alignLow();
                    }
                    else if ((oneWayUp && topIsLarger) || (oneWayDown && !topIsLarger))
                    {
                        // pick alignment at random
                        return (this.Size + other.Size) % 2 == 0 ? alignLow() : alignHigh();
                    }
                }
            }
            if (this.Side == ScreenDirection.Left && other.Side == ScreenDirection.Right)
            {
                // Horizontal transitions
                if (this.HighOpen || other.HighOpen)
                {
                    // if either is open on the bottom, they both must be open on the bottom and we align the death planes
                    // this is kind of a questionable choice all around tbh
                    // maybe additionally restrict that sizes must be the same?
                    if (this.HighOpen && other.HighOpen)
                    {
                        return alignHigh();
                    }
                }
                else if (this.LowOpen || other.LowOpen)
                {
                    // if either is open on the top, the other must also be open on the top OR it must be sufficiently tall
                    if (this.LowOpen && other.LowOpen)
                    {
                        return alignHigh();
                    }
                    else if (this.Closed && this.Size > 2)
                    {
                        return alignHigh();
                    }
                    else if (other.Closed && other.Size > 2)
                    {
                        return alignHigh();
                    }
                }
                else
                {
                    // only remaining option is both closed. they must be the same size OR sufficiently tall
                    if (this.Size == other.Size)
                    {
                        return alignHigh();
                    }
                    else if (this.Size > 2 && other.Size > 2)
                    {
                        return alignHigh();
                    }
                }
            }
            return INCOMPATIBLE;
        }
    }
}
