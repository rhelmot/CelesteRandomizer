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
        public List<RandoConfigEdit> Tweaks;

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
            result.Music = "";

            if (this.Tweaks == null) {
                this.Tweaks = new List<RandoConfigEdit>();
            }

            var toRemoveEntities = new List<EntityData>();
            var toRemoveTriggers = new List<EntityData>();
            var toRemoveSpawns = new List<Vector2>();
            void processEntity(EntityData entity, List<EntityData> removals) {
                if (entity.Name == "goldenBerry" || entity.Name == "musicTrigger" || entity.Name == "noRefillTrigger") {
                    removals.Add(entity);
                    return;
                } else if (entity.Name == "finalBoss") {
                    entity.Values["canChangeMusic"] = false;
                }

                foreach (var econfig in this.Tweaks) {
                    if (!(econfig.Update?.Add ?? false) &&
                        (econfig.ID == null || econfig.ID == entity.ID) &&
                        (econfig.Name == null || econfig.Name.ToLower() == entity.Name.ToLower()) &&
                        (econfig.X == null || econfig.X == entity.Position.X) &&
                        (econfig.Y == null || econfig.Y == entity.Position.Y)) {
                        if (econfig.Update?.Remove ?? false) {
                            removals.Add(entity);
                        } else {
                            if (econfig.Update?.X != null)
                                entity.Position.X = (float)econfig.Update.X;
                            if (econfig.Update?.Y != null)
                                entity.Position.Y = (float)econfig.Update.Y;
                            if (econfig.Update?.Width != null)
                                entity.Width = (int)econfig.Update.Width;
                            if (econfig.Update?.Height != null)
                                entity.Height = (int)econfig.Update.Height;
                        }
                        break;
                    }
                }
            }
            void processSpawn(Vector2 spawn) {
                foreach (var econfig in this.Tweaks) {
                    if (!(econfig.Update?.Add ?? false) &&
                        econfig.Name.ToLower() == "spawn" &&
                        (econfig.X == null || econfig.X == spawn.X) &&
                        (econfig.Y == null || econfig.Y == spawn.Y)) {
                        if (econfig.Update?.Remove ?? false) {
                            toRemoveSpawns.Add(spawn);
                        } else {
                            if (econfig.Update?.X != null)
                                spawn.X = (float)econfig.Update.X;
                            if (econfig.Update?.Y != null)
                                spawn.Y = (float)econfig.Update.Y;
                        }
                        break;
                    }
                }
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
            foreach (var spawn in result.Spawns) {
                processSpawn(spawn);
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
                    if (econfig.Name.ToLower() == "spawn") {
                        Logger.Log("randomizer", "Adding spawn");
                        result.Spawns.Add(new Vector2((float)econfig.Update.X + result.Position.X, (float)econfig.Update.Y + result.Position.Y));
                    } else {
                        var entity = new EntityData() {
                            Name = econfig.Name,
                            Position = new Vector2((float)econfig.Update.X, (float)econfig.Update.Y),
                            Width = (int)econfig.Update.Width,
                            Height = (int)econfig.Update.Height,
                            ID = ++maxID,
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
