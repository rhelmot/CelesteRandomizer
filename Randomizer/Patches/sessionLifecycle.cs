using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.Utils;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Randomizer {
    public partial class RandoModule {

        private List<IDetour> SpecialHooksSession = new List<IDetour>();
        private void LoadSessionLifecycle() {
            Everest.Events.Level.OnComplete += OnComplete;
            On.Celeste.AreaComplete.VersionNumberAndVariants += AreaCompleteDrawHash;
            On.Celeste.AutoSplitterInfo.Update += MainThreadHook;
            On.Celeste.Editor.MapEditor.ctor += MarkSessionUnclean;
            On.Celeste.LevelExit.Begin += HijackExitBegin;
            IL.Celeste.SpeedrunTimerDisplay.DrawTime += SetPlatinumColor;
            IL.Celeste.AreaComplete.ctor += SetEndlessTitle;
            
            // this method is patched by everest so we need to get at the unpatched orig version
            SpecialHooksSession.Add(new ILHook(typeof(AreaComplete).GetMethod("orig_Update"), GotoNextEndless));
        }

        private void UnloadSessionLifecycle() {
            Everest.Events.Level.OnComplete -= OnComplete;
            On.Celeste.AreaComplete.VersionNumberAndVariants -= AreaCompleteDrawHash;
            On.Celeste.AutoSplitterInfo.Update -= MainThreadHook;
            On.Celeste.Editor.MapEditor.ctor -= MarkSessionUnclean;
            On.Celeste.LevelExit.Begin -= HijackExitBegin;
            IL.Celeste.SpeedrunTimerDisplay.DrawTime -= SetPlatinumColor;
            IL.Celeste.AreaComplete.ctor -= SetEndlessTitle;
            IL.Celeste.AreaComplete.Update -= GotoNextEndless;
            
            foreach (var detour in this.SpecialHooksSession) {
                detour.Dispose();
            }
            this.SpecialHooksSession.Clear();
        }

        private Task<AreaKey> genTask;
        private RandoSettings endingSettings = null;
        private void GotoNextEndless(ILContext il) {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdarg(0),
                                                     instr => instr.MatchLdfld<AreaComplete>("snow"))) {
                throw new Exception("Can't find patch point 2!");
            }

            var label = cursor.MarkLabel();

            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld<AreaComplete>("canConfirm"))) {
                throw new Exception("Can't find patch point 1");
            }

            cursor.EmitDelegate<Func<bool>>(() => {
                var settings = this.endingSettings;
                if (settings == null || settings.Algorithm != LogicType.Endless) {
                    return false;
                }

                if (!genTask.IsCompleted) {
                    typeof(AreaComplete).GetField("canConfirm", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Engine.Scene, true);
                    return true;
                }

                
                var session = SaveData.Instance.CurrentSession;
                var time = session.Time;
                var deaths = session.Deaths;
                var clean = session.SeedCleanRandom();
                session = new Session(genTask.Result);
                session.Time = time;
                session.Deaths = deaths;
                session.SeedCleanRandom(clean);
                UseSession = session;
                StartMe = genTask.Result;
                genTask = null;
                return true;
            });
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Brtrue, label);
        }

        private void SetEndlessTitle(ILContext il) {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCall("Celeste.Dialog", "Clean"))) {
                throw new Exception("Could not find patch point!");
            }

            cursor.EmitDelegate<Func<string, string>>((val) => {
                if (this.endingSettings != null && this.endingSettings.Algorithm == LogicType.Endless) {
                    return string.Format(Dialog.Get("RANDOENDLESS_HEADER"), this.endingSettings.EndlessLevel + 1);
                }

                return val;
            });
        }

        private void HijackExitBegin(On.Celeste.LevelExit.orig_Begin orig, LevelExit self) {
            orig(self);
            var settings = this.InRandomizerSettings;
            this.endingSettings = settings;
            if (settings == null || settings.Algorithm != LogicType.Endless) {
                return;
            }

            LevelExit.Mode mode = (LevelExit.Mode) typeof(LevelExit).GetField("mode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(self);
            if (mode != LevelExit.Mode.Completed) {
                return;
            }

            var newSettings = settings.Copy();
            newSettings.EndlessLevel++;
            Audio.SetMusic(SFX.music_complete_bside);
            Audio.SetAmbience(null);

            this.genTask = Task.Run(() => RandoLogic.GenerateMap(newSettings));
        }

        public static AreaData AreaHandoff;
        public static AreaKey? StartMe;
        public static Session UseSession;
        private bool Entering;
        private void MainThreadHook(On.Celeste.AutoSplitterInfo.orig_Update orig, AutoSplitterInfo self) {
            orig(self);

            if (AreaHandoff != null) {
                AreaData.Areas.Add(AreaHandoff);
                var unused = new AreaKey(AreaData.Areas.Count - 1); // does this trigger some extra behavior
                AreaHandoff = null;
            }
            if (StartMe != null && !Entering) {
                var newArea = StartMe.Value;
                
                var area = AreaData.Get(newArea);
                var dyn = new DynData<AreaData>(area);
                var settings = dyn.Get<RandoSettings>("RandoSettings");
                Audio.SetMusic(null);
                Audio.SetAmbience(null);
                Audio.Play("event:/ui/main/savefile_begin");

                // use the debug file
                SaveData.InitializeDebugMode();
                // turn on/off variants mode
                SaveData.Instance.VariantMode = settings.Variants;
                SaveData.Instance.AssistMode = false;
                // mark as completed to spawn golden berry
                SaveData.Instance.Areas[newArea.ID].Modes[0].Completed = true;
                // mark heart as not collected
                SaveData.Instance.Areas[newArea.ID].Modes[0].HeartGem = false;
                Entering = true;

                var unused = new FadeWipe(Engine.Scene, false, () => {   // assign to variable to suppress compiler warning
                    Session session;
                    if (UseSession != null) {
                        session = UseSession;
                        UseSession = null;
                    } else {
                        session = new Session(newArea) {
                            FirstLevel = true,
                            StartedFromBeginning = true,
                        };
                        session.SeedCleanRandom(Settings.SeedType == SeedType.Random);
                    }
                    SaveData.Instance.StartSession(session);    // need to set this earlier than we would get otherwise
                    StartMe = null;
                    Entering = false;
                    LevelEnter.Go(session, true);
                });

                /*foreach (AreaData area in AreaData.Areas) {
                    Logger.Log("randomizer", $"Skeleton for {area.GetSID()}");
                    RandoConfigFile.YamlSkeleton(area);

                }*/
            }
        }

        // when we load the map editor, effectively change to a set seed speedrun
        private void MarkSessionUnclean(On.Celeste.Editor.MapEditor.orig_ctor orig, Editor.MapEditor self, AreaKey area, bool reloadMapData) {
            if (Engine.Scene is Level level) {
                level.Session.SeedCleanRandom(false);
            }
            orig(self, area, reloadMapData);
        }

        void OnComplete(Level level) {
            level.Session.BeatBestTimePlatinum(false);
            var settings = this.InRandomizerSettings;
            if (settings != null && level.Session.StartedFromBeginning) {  // how strong can/should we make this condition?   
                var hash = uint.Parse(settings.Hash); // convert and unconvert, yeah I know

                level.Session.BeatBestTime = false;
                if (this.SavedData.BestTimes.TryGetValue(hash, out long prevBest)) {
                    if (level.Session.Time < prevBest) {
                        level.Session.BeatBestTime = true;
                        this.SavedData.BestTimes[hash] = level.Session.Time;
                    }
                } else {
                    this.SavedData.BestTimes[hash] = level.Session.Time;
                }

                if (settings.Rules != Ruleset.Custom) {
                    if (this.SavedData.BestSetSeedTimes.TryGetValue(settings.Rules, out var prevBestSet)) {
                        if (level.Session.Time < prevBestSet.Item1) {
                            level.Session.BeatBestTimePlatinum(true);
                            this.SavedData.BestSetSeedTimes[settings.Rules] = RecordTuple.Create(level.Session.Time, settings.Seed);
                        }
                    } else {
                        this.SavedData.BestSetSeedTimes[settings.Rules] = RecordTuple.Create(level.Session.Time, settings.Seed);
                    }

                    if (level.Session.SeedCleanRandom()) {
                        if (this.SavedData.BestRandomSeedTimes.TryGetValue(settings.Rules, out var prevBestRand)) {
                            if (level.Session.Time < prevBestRand.Item1) {
                                level.Session.BeatBestTimePlatinum(true);
                                this.SavedData.BestRandomSeedTimes[settings.Rules] = RecordTuple.Create(level.Session.Time, settings.Seed);
                            }
                        } else {
                            this.SavedData.BestRandomSeedTimes[settings.Rules] = RecordTuple.Create(level.Session.Time, settings.Seed);
                        }
                    }
                }

                this.SaveSettings();
            }
        }

        private void AreaCompleteDrawHash(On.Celeste.AreaComplete.orig_VersionNumberAndVariants orig, string version, float ease, float alpha) {
            orig(version, ease, alpha);

            var settings = this.endingSettings;
            var session = SaveData.Instance?.CurrentSession;
            if (settings != null) {
                var text = settings.Seed;
                if (settings.Rules != Ruleset.Custom) {
                    text += " " + settings.Rules.ToString();
                    if (session?.SeedCleanRandom() ?? false) {
                        text += "!";
                    }
                }

                text += "\n#" + settings.Hash;
                text += "\nrando " + this.Metadata.VersionString;
                var variants = SaveData.Instance?.VariantMode ?? false;
                ActiveFont.DrawOutline(text, new Vector2(1820f + 300f * (1f - Ease.CubeOut(ease)), variants ? 810f : 894f), new Vector2(0.5f, 0f), Vector2.One * 0.5f, settings.SpawnGolden ? Calc.HexToColor("fad768") : Color.White, 2f, Color.Black);
            }
        }
        
        private void SetPlatinumColor(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdarg(5))) {
                throw new Exception("Failed to find patch spot 2 [first pass]");
            }
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdcI4(0))) {
                throw new Exception("Failed to find patch spot 1");
            }
            var afterInstr = cursor.MarkLabel();

            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchLdarg(5))) {
                throw new Exception("Failed to find patch spot 2");
            }

            cursor.EmitDelegate<Func<bool>>(() => {
                if (!this.InRandomizer) {
                    return false;
                }
                if (Engine.Scene is Level level) {
                    return level.Session.BeatBestTimePlatinum();
                }
                return false;
            });

            var beforeInstr = cursor.DefineLabel();
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Brfalse, beforeInstr);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldstr, "cb19d2");
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Calc).GetMethod("HexToColor", new[] {typeof(string)}));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, 6);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Color).GetMethod("op_Multiply"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Stloc, 5);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldstr, "994f9c");
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Calc).GetMethod("HexToColor", new[] {typeof(string)}));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, 6);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Color).GetMethod("op_Multiply"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Stloc, 6);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Br, afterInstr);
            cursor.MarkLabel(beforeInstr);
        }
    }

    public static class SessionExt {
        public static bool BeatBestTimePlatinum(this Session session, bool? set=null) {
            return SessionVariable(session, "BeatBestTimePlatinum", set);
        }

        public static bool SeedCleanRandom(this Session session, bool? set=null) {
            return SessionVariable(session, "SeedCleanRandom", set);
        }

        private static bool SessionVariable(Session session, string name, bool? set=null) {
            var dyn = new DynData<Session>(session);
            if (set != null) {
                dyn.Set<bool>(name, set.Value);
                return set.Value;
            } else {
                return dyn.Get<bool?>(name) ?? false;
            }
        }
    }
}