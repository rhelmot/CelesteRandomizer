using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer {
    public partial class RandoModule : EverestModule {
        private void LoadMechanics() {
            Everest.Events.Level.OnTransitionTo += OnTransition;
            Everest.Events.Level.OnLoadLevel += OnLoadLevel;
            On.Celeste.Textbox.ctor_string_Language_Func1Array += RandomizeTextboxText;
            On.Celeste.Level.LoadLevel += OnLoadLevelHook;
            IL.Celeste.Level.EnforceBounds += DisableUpTransition;
            IL.Celeste.Level.EnforceBounds += DisableDownTransition;
        }

        private void UnloadMechanics() {
            Everest.Events.Level.OnTransitionTo -= OnTransition;
            Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
            On.Celeste.Textbox.ctor_string_Language_Func1Array -= RandomizeTextboxText;
            On.Celeste.Level.LoadLevel -= OnLoadLevelHook;
            IL.Celeste.Level.EnforceBounds -= DisableUpTransition;
            IL.Celeste.Level.EnforceBounds -= DisableDownTransition;
        }

        private void OnLoadLevelHook(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool fromLoader) {
            if (fromLoader && this.InRandomizer) {
                // Don't restart the timer on retry
                self.Session.FirstLevel = false;
            }
            orig(self, playerIntro, fromLoader);
            // also, set the core mode right
            if (fromLoader && this.InRandomizer) {
                var leveldata = self.Session.LevelData;
                var dyn = new DynData<LevelData>(leveldata);
                RandoConfigCoreMode modes = dyn.Get<RandoConfigCoreMode>("coreModes");
                self.CoreMode = modes?.All ?? Session.CoreModes.None;
                self.Session.CoreMode = self.CoreMode;
            }

            if (this.InRandomizer && Settings.Algorithm == LogicType.Labyrinth && Everest.Loader.DependencyLoaded(new EverestModuleMetadata() { Name = "BingoUI" })) {
                var ui = LoadGemUI(fromLoader); // must be a separate method or the jit will be very sad :(
                self.Add(ui); // lord fucking help us
            }
        }

        private Entity SavedGemUI;
        private Entity LoadGemUI(bool reset) {
            if (reset) {
                SavedGemUI = null;
            }
            if (SavedGemUI != null) {
                return SavedGemUI;
            }
            // lol reflection
            var bingo = AppDomain.CurrentDomain.GetAssemblies().Where(asm => asm.FullName.Contains("BingoUI")).First();
            var tcd_cls = bingo.GetType("Celeste.Mod.BingoUI.TotalCollectableDisplay");
            var dtype = tcd_cls.GetNestedType("CheckVal");
            var checker = typeof(RandoModule).GetMethod("CheckGems", BindingFlags.Instance | BindingFlags.NonPublic);
            var texture = GFX.Game["collectables/summitgems/3/gem00"];
            var constructor = tcd_cls.GetConstructor(new Type[] { typeof(float), dtype, typeof(bool), typeof(int), typeof(MTexture) });
            Entity tcd = (Entity)constructor.Invoke(new object[] { 177f, Delegate.CreateDelegate(dtype, this, checker), true, 0, texture });
            SavedGemUI = tcd;
            return tcd;
        }

        private int CheckGems() {
            if (Engine.Scene is Level level) {
                return level.Session.SummitGems.Count((hasGem) => hasGem);
            }
            return 0;
        }

        private void OnTransition(Level level, LevelData next, Vector2 direction) {
            if (this.InRandomizer) {
                // set core mode
                var extraData = new DynData<LevelData>(next);
                var coreModes = extraData.Get<RandoConfigCoreMode>("coreModes");
                Session.CoreModes newMode;
                if (coreModes == null) {
                    newMode = Session.CoreModes.None;
                } else if (direction.X > 0) {
                    newMode = coreModes.Left;
                } else if (direction.X < 0) {
                    newMode = coreModes.Right;
                } else if (direction.Y < 0) {
                    newMode = coreModes.Down;
                } else {
                    newMode = coreModes.Up;
                }
                level.CoreMode = newMode;
                level.Session.CoreMode = newMode;

                // clear summit flags
                var toRemove = new System.Collections.Generic.List<string>();
                foreach (var flag in level.Session.Flags) {
                    if (flag.StartsWith("summit_checkpoint_")) {
                        toRemove.Add(flag);
                    }
                }
                foreach (var flag in toRemove) {
                    level.Session.Flags.Remove(flag);
                }

                // reset camera (should hopefully fix badeline issues)
                level.CameraUpwardMaxY = level.Camera.Y + 1000f;
            }
        }

        private void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            if (this.InRandomizer) {
                // set summit gems
                SaveData.Instance.SummitGems = new bool[6];
                if (Settings.Length == MapLength.Short) {
                    SaveData.Instance.SummitGems[0] = true;
                    SaveData.Instance.SummitGems[1] = true;
                    SaveData.Instance.SummitGems[2] = true;
                }
            }
        }

        void RandomizeTextboxText(On.Celeste.Textbox.orig_ctor_string_Language_Func1Array orig, Textbox self, string dialog, Language language, Func<IEnumerator>[] events) {
            if (InRandomizer && RandoLogic.RandomDialogMappings.ContainsKey(dialog.ToLower())) {
                DynData<Textbox> selfData = new DynData<Textbox>(self);
                FancyText.Text origText = FancyText.Parse(Dialog.Get(dialog, language), (int)selfData.Get<float>("maxLineWidth"), selfData.Get<int>("linesPerPage"), 0f, null, language);
                var origTriggers = new List<FancyText.Trigger>(origText.Nodes.OfType<FancyText.Trigger>());
                orig(self, RandoLogic.RandomDialogMappings[dialog.ToLower()], language, events);

                // Replace triggers from randomized text with triggers from original text
                int origIndex = 0;
                for (int i = 0; i < self.Nodes.Count; i++) {
                    if (self.Nodes[i] is FancyText.Trigger trigger) {
                        if (origIndex < origTriggers.Count) {
                            trigger.Index = origTriggers[origIndex].Index;
                            trigger.Label = origTriggers[origIndex].Label;
                            trigger.Silent = origTriggers[origIndex].Silent;
                            origIndex++;
                        } else {
                            // This effectively disables the trigger if we've run out of original triggers
                            trigger.Index = -1;
                        }
                    }
                }
                // Add the remaining original triggers on to the end
                if (origIndex < origTriggers.Count) {
                    self.Nodes.AddRange(origTriggers.GetRange(origIndex, origTriggers.Count - origIndex));
                }
            } else {
                orig(self, dialog, language, events);
            }
        }

        private void DisableUpTransition(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<MapData>("CanTransitionTo"));
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<MapData>("CanTransitionTo"));
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<MapData>("CanTransitionTo"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Level, bool>>((level) => {
                if (!this.InRandomizer) {
                    return true;
                }
                
                var currentRoom = level.Session.LevelData;
                var player = level.Tracker.GetEntity<Player>();
                var dyn = new DynData<LevelData>(currentRoom);
                var holes = dyn.Get<List<Hole>>("UsedVerticalHoles");
                var found = false;
                foreach (var hole in holes) {
                    if (hole.Side != ScreenDirection.Up) {
                        continue;
                    }
                    if (player.Center.X > currentRoom.Bounds.Left + hole.LowBound*8 && player.Center.X < currentRoom.Bounds.Left + hole.HighBound*8 + 8) {
                        found = true;
                        break;
                    }
                }

                return found;
            });
            cursor.Emit(Mono.Cecil.Cil.OpCodes.And);
        }

        private void DisableDownTransition(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld<LevelData>("DisableDownTransition"))) {
                throw new Exception("Could not find patch point");
            }
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, Level, bool>>((prevDisable, level) => {
                if (!this.InRandomizer) {
                    return prevDisable;
                }

                var currentRoom = level.Session.LevelData;
                var player = level.Tracker.GetEntity<Player>();
                var dyn = new DynData<LevelData>(currentRoom);
                var holes = dyn.Get<List<Hole>>("UsedVerticalHoles");
                var found = false;
                foreach (var hole in holes) {
                    if (hole.Side != ScreenDirection.Down) {
                        continue;
                    }
                    if (player.Center.X > currentRoom.Bounds.Left + hole.LowBound*8 && player.Center.X < currentRoom.Bounds.Left + hole.HighBound*8 + 8) {
                        found = true;
                        break;
                    }
                }

                return !found;
            });
        }
    }
}