using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.Utils;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Randomizer {
    public partial class RandoModule : EverestModule {

        private List<IDetour> SpecialHooksMechanics = new List<IDetour>();
        private void LoadMechanics() {
            Everest.Events.Level.OnTransitionTo += OnTransition;
            Everest.Events.Level.OnLoadLevel += OnLoadLevel;
            On.Celeste.Textbox.ctor_string_Language_Func1Array += RandomizeTextboxText;
            On.Celeste.Level.LoadLevel += OnLoadLevelHook;
            On.Celeste.LevelLoader.LoadingThread += PatchLoadingThread;
            IL.Celeste.Level.EnforceBounds += DisableUpTransition;
            IL.Celeste.Level.EnforceBounds += DisableDownTransition;

            IL.Celeste.CS02_DreamingPhonecall.OnEnd += CutsceneWarpTarget;
            SpecialHooksMechanics.Add(new ILHook(typeof(CS04_MirrorPortal).GetMethod("Cutscene", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget(), CutsceneWarpMirrorFakeBSide));
            SpecialHooksMechanics.Add(new ILHook(typeof(CS04_MirrorPortal).GetNestedType("<>c__DisplayClass9_0", BindingFlags.NonPublic).GetMethod("<OnEnd>b__0", BindingFlags.NonPublic | BindingFlags.Instance), CutsceneWarpTargetMirror));
            SpecialHooksMechanics.Add(new ILHook(typeof(CS06_StarJumpEnd).GetNestedType("<>c__DisplayClass40_0", BindingFlags.NonPublic).GetMethod("<OnEnd>b__0", BindingFlags.NonPublic | BindingFlags.Instance), CutsceneWarpTarget));
            SpecialHooksMechanics.Add(new ILHook(typeof(CS06_StarJumpEnd).GetNestedType("<>c__DisplayClass40_0", BindingFlags.NonPublic).GetMethod("<OnEnd>b__1", BindingFlags.NonPublic | BindingFlags.Instance), CutsceneWarpTargetFall));

            SpecialHooksMechanics.Add(new ILHook(typeof(EventTrigger).GetNestedType("<>c__DisplayClass10_0", BindingFlags.NonPublic).GetMethod("<OnEnter>b__0", BindingFlags.NonPublic | BindingFlags.Instance), SpecificWarpTarget));
        }

        private void UnloadMechanics() {
            Everest.Events.Level.OnTransitionTo -= OnTransition;
            Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
            On.Celeste.Textbox.ctor_string_Language_Func1Array -= RandomizeTextboxText;
            On.Celeste.Level.LoadLevel -= OnLoadLevelHook;
            On.Celeste.LevelLoader.LoadingThread -= PatchLoadingThread;
            IL.Celeste.Level.EnforceBounds -= DisableUpTransition;
            IL.Celeste.Level.EnforceBounds -= DisableDownTransition;

            IL.Celeste.CS02_DreamingPhonecall.OnEnd -= CutsceneWarpTarget;
            IL.Celeste.CS04_MirrorPortal.Cutscene -= CutsceneWarpMirrorFakeBSide;
            foreach (var detour in this.SpecialHooksMechanics) {
                detour.Dispose();
            }
            this.SpecialHooksMechanics.Clear();
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

                // reset extended variants :(
                this.ResetExtendedVariants();
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

        private void CutsceneWarpTarget(ILContext il) {
            var cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStfld("Celeste.Session", "Level"))) {
                cursor.EmitDelegate<Func<string, string>>((prevNextLevel) => {
                    if (!this.InRandomizer) {
                        return prevNextLevel;
                    }

                    var dyn = new DynData<LevelData>(SaveData.Instance.CurrentSession.LevelData);
                    var newNextLevel = dyn.Get<string>("CustomWarp");
                    if (newNextLevel == null) {
                        throw new Exception("Randomizer error: no target for warp");
                    }
                    return newNextLevel;
                });
                cursor.Index++;
            }

            cursor.Index = 0;
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStfld("Celeste.Session", "RespawnPoint"))) {
                cursor.EmitDelegate<Func<Vector2?, Vector2?>>((prevStartPoint) => this.InRandomizer ? null : prevStartPoint);
                cursor.Index++;
            }
        }

        private void CutsceneWarpTargetMirror(ILContext il) {
            this.CutsceneWarpTarget(il);
            this.CutsceneWarpMirrorFakeBSide(il);
        }

        private void CutsceneWarpTargetFall(ILContext il) {
            this.CutsceneWarpTarget(il);
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcI4(6))) {
                throw new Exception("Could not find patch point!");
            }
            cursor.EmitDelegate<Func<Player.IntroTypes, Player.IntroTypes>>(oldType => this.InRandomizer ? Player.IntroTypes.None : oldType);
        }

        private void CutsceneWarpMirrorFakeBSide(ILContext il) {
            var cursor = new ILCursor(il);
            var count = 0;
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.AreaKey", "Mode"))) {
                cursor.EmitDelegate<Func<AreaMode, AreaMode>>(oldMode => this.InRandomizer ? AreaMode.BSide : oldMode);
                count++;
            }

            if (count == 0) {
                throw new Exception("Could not find patch point(s)!");
            }
        }

        private void SpecificWarpTarget(ILContext il) {
            var cursor = new ILCursor(il);
            var count = 0;
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStfld("Celeste.Session", "Level"))) {
                cursor.EmitDelegate<Func<string, string>>((oldTarget) => {
                    if (!this.InRandomizer) {
                        return oldTarget;
                    }
                    var session = SaveData.Instance.CurrentSession;
                    var room = session.LevelData;
                    var dyn = new DynData<LevelData>(room);
                    var mapping = dyn.Get<Dictionary<string, string>>("WarpMapping");
                    if (mapping == null) {
                        throw new Exception("Randomizer error: no warp mapping information available");
                    }
                    if (!mapping.TryGetValue(oldTarget, out string newTarget)) {
                        throw new Exception("Randomizer error: no warp mapping target for " + oldTarget);
                    }
                    return newTarget;
                });
                count++;
                cursor.Index++;
            }

            if (count == 0) {
                throw new Exception("Could not find patch point(s)!");
            }
        }

        private void PatchLoadingThread(On.Celeste.LevelLoader.orig_LoadingThread orig, LevelLoader self) {
            if (this.InRandomizer) {
                Logger.Log("randomizer", "Mashing up tilesets...");
                MakeFrankenTilesets();
            }
            orig(self);
        }

        private void MakeFrankenTilesets() {
            var fgPaths = new List<string>();
            var bgPaths = new List<string>();

            foreach (var map in this.Settings.EnabledMaps) {
                var meta = AreaData.Get(map).GetMeta();
                var fgPath = meta?.ForegroundTiles;
                var bgPath = meta?.BackgroundTiles;
                if (!string.IsNullOrEmpty(fgPath) && !fgPaths.Contains(fgPath)) {
                    fgPaths.Add(fgPath);
                }
                if (!string.IsNullOrEmpty(bgPath) && !bgPaths.Contains(bgPath)) {
                    bgPaths.Add(bgPath);
                }
            }

            MakeFrankenTileset(GFX.FGAutotiler, fgPaths);
            MakeFrankenTileset(GFX.BGAutotiler, bgPaths);
        }

        private static void MakeFrankenTileset(Autotiler basic, List<string> additions) {
            var counts = new Dictionary<char, int>();
            var r = new Random(); // TODO how to seed this?
            // uhhhhhhh this is intensely sketchy
            var lookup = (IDictionary)typeof(Autotiler).GetField("lookup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(basic);
            foreach (char k in lookup.Keys) {
                counts[k] = 1;
            }

            foreach (var path in additions) {
                var advanced = new Autotiler(path);
                var lookup2 = (IDictionary)typeof(Autotiler).GetField("lookup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(advanced);
                foreach (char k in lookup2.Keys) {
                    if (counts.ContainsKey(k)) {
                        counts[k]++;
                        if (r.Next(counts[k]) == 0) {
                            lookup[k] = lookup2[k];
                        }
                    } else {
                        counts[k] = 1;
                        lookup[k] = lookup2[k];
                    }
                }
            }
        }
    }
}