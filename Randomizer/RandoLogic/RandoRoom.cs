using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer {
    public class RandoRoom {
        public AreaKey Area;
        public LevelData Level;
        public List<Hole> Holes;
        public readonly String Name;
        public bool End;

        public RandoRoom(AreaKey Area, String prefix, LevelData Level, List<Hole> Holes) {
            this.Area = Area;
            this.Name = prefix + Level.Name;
            this.Level = Level;
            this.Holes = Holes;
        }

        private LevelData LevelCopy(Vector2 newPosition, int? nonce) {
            var result = this.Level.Copy();
            result.Name = nonce == null ? this.Name : this.Name + "/" + nonce.ToString();
            result.Position = newPosition;
            return result;
        }

        public LevelData LinkStart(int? nonce = null) {
            return this.LevelCopy(Vector2.Zero, nonce);
        }

        public LevelData LinkAdjacent(LevelData against, ScreenDirection side, int offset, int? nonce) {
            return this.LevelCopy(this.NewPosition(against, side, offset), nonce);
        }

        public Rectangle QuickLinkAdjacent(LevelData against, ScreenDirection side, int offset) {
            var pos = this.NewPosition(against, side, offset);
            return new Rectangle((int)pos.X, (int)pos.Y, this.Level.Bounds.Width, this.Level.Bounds.Height);
        }

        private Vector2 NewPosition(LevelData against, ScreenDirection side, int offset) {
            int roundUp(int inp) {
                while (inp % 8 != 0) {
                    inp++;
                }
                return inp;
            }
            switch (side) {
                case ScreenDirection.Up:
                    return against.Position + new Vector2(offset * 8, -roundUp(this.Level.Bounds.Height));
                case ScreenDirection.Down:
                    return against.Position + new Vector2(offset * 8, roundUp(against.Bounds.Height));
                case ScreenDirection.Left:
                    return against.Position + new Vector2(-roundUp(this.Level.Bounds.Width), offset * 8);
                case ScreenDirection.Right:
                default:
                    return against.Position + new Vector2(roundUp(against.Bounds.Width), offset * 8);
            }
        }
    }
}
