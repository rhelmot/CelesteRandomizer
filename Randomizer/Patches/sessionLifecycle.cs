using System;
using System.Reflection;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer {
    public partial class RandoModule : EverestModule {

        private void LoadSessionLifecycle() {
            Everest.Events.Level.OnComplete += OnComplete;
            On.Celeste.AreaComplete.VersionNumberAndVariants += AreaCompleteDrawHash;
            On.Celeste.AutoSplitterInfo.Update += MainThreadHook;
            On.Celeste.Session.Restart += MaintainSessionFlags;
            On.Celeste.Editor.MapEditor.ctor += MarkSessionUnclean;
            On.Celeste.Editor.MapEditor.LoadLevel += MaintainSessionFlags2;
            IL.Celeste.SpeedrunTimerDisplay.DrawTime += SetPlatinumColor;
        }

        private void UnloadSessionLifecycle() {
            Everest.Events.Level.OnComplete -= OnComplete;
            On.Celeste.AreaComplete.VersionNumberAndVariants -= AreaCompleteDrawHash;
            On.Celeste.AutoSplitterInfo.Update -= MainThreadHook;
            On.Celeste.Session.Restart -= MaintainSessionFlags;
            On.Celeste.Editor.MapEditor.ctor -= MarkSessionUnclean;
            On.Celeste.Editor.MapEditor.LoadLevel -= MaintainSessionFlags2;
            IL.Celeste.SpeedrunTimerDisplay.DrawTime -= SetPlatinumColor;
        }
        public static AreaData AreaHandoff;
        public static AreaKey? StartMe;
        private bool Entering;
        private void MainThreadHook(On.Celeste.AutoSplitterInfo.orig_Update orig, AutoSplitterInfo self) {
            orig(self);

            if (AreaHandoff != null) {
                AreaData.Areas.Add(AreaHandoff);
                var key = new AreaKey(AreaData.Areas.Count - 1); // does this trigger some extra behavior
                AreaHandoff = null;
            }
            if (StartMe != null && !Entering) {
                var newArea = StartMe.Value;
                Audio.SetMusic((string)null, true, true);
                Audio.SetAmbience((string)null, true);
                Audio.Play("event:/ui/main/savefile_begin");

                // use the debug file
                SaveData.InitializeDebugMode();
                // turn off variants mode
                SaveData.Instance.VariantMode = false;
                SaveData.Instance.AssistMode = false;
                // mark as completed to spawn golden berry
                SaveData.Instance.Areas[newArea.ID].Modes[0].Completed = true;
                // mark heart as not collected
                SaveData.Instance.Areas[newArea.ID].Modes[0].HeartGem = false;
                Entering = true;

                var fade = new FadeWipe(Engine.Scene, false, () => {   // assign to variable to suppress compiler warning
                    var session = new Session(newArea, null, null) {
                        FirstLevel = true,
                        StartedFromBeginning = true,
                    };
                    session.StartedFromRandomizerMenu(true);
                    session.SeedCleanRandom(Settings.SeedType == SeedType.Random);
                    SaveData.Instance.StartSession(session);    // need to set this earlier than we would get otherwise
                    LevelEnter.Go(session, true);
                    StartMe = null;
                    Entering = false;
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

        // when we restart the level, mantain the randomizer status
        private Session MaintainSessionFlags(On.Celeste.Session.orig_Restart orig, Session self, string intoLevel) {
            var result = orig(self, intoLevel);
            result.StartedFromRandomizerMenu(self.StartedFromRandomizerMenu());
            return result;

        }

        // when we teleport with the map editor, maintain the randomizer status
        // awful reflection hack instead of IL mod to maintain compatibility across everest versions
        private void MaintainSessionFlags2(On.Celeste.Editor.MapEditor.orig_LoadLevel orig, Editor.MapEditor self, Editor.LevelTemplate levelTemplate, Vector2 at) {
            var field = typeof(Editor.MapEditor).GetField("CurrentSession", BindingFlags.Instance | BindingFlags.NonPublic);
            var oldval = false;
            if (field != null) {
                var session = (Session)field.GetValue(self);
                oldval = session.StartedFromRandomizerMenu();
            }
            orig(self, levelTemplate, at);

            var scene1 = Engine.Scene;
            Scene scene2 = null;
            Session newsession = null;
            var field2 = typeof(Monocle.Engine).GetField("nextScene", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field2 != null) {
                scene2 = (Scene)field2.GetValue(Engine.Instance);
            }
            if (scene1 != null && scene1 is LevelLoader ll) {
                newsession = (Session)typeof(LevelLoader).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ll);
            } else if (scene1 != null && scene1 is Level ll2) {
                newsession = ll2.Session;
            } else if (scene2 != null && scene2 is LevelLoader ll3) {
                newsession = (Session)typeof(LevelLoader).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ll3);
            } else if (scene2 != null && scene2 is Level ll4) {
                newsession = ll4.Session;
            }

            if (newsession != null) {
                newsession.StartedFromRandomizerMenu(oldval);
            }
        }

        void OnComplete(Level level) {
            level.Session.BeatBestTimePlatinum(false);
            if (level.Session.StartedFromRandomizerMenu() && level.Session.StartedFromBeginning) {  // how strong can/should we make this condition?   
                var hash = uint.Parse(Settings.Hash); // convert and unconvert, yeah I know

                level.Session.BeatBestTime = false;
                if (this.SavedData.BestTimes.TryGetValue(hash, out long prevBest)) {
                    if (level.Session.Time < prevBest) {
                        level.Session.BeatBestTime = true;
                        this.SavedData.BestTimes[hash] = level.Session.Time;
                    }
                } else {
                    this.SavedData.BestTimes[hash] = level.Session.Time;
                }

                if (Settings.Rules != Ruleset.Custom) {
                    if (this.SavedData.BestSetSeedTimes.TryGetValue(Settings.Rules, out var prevBestSet)) {
                        if (level.Session.Time < prevBestSet.Item1) {
                            level.Session.BeatBestTimePlatinum(true);
                            this.SavedData.BestSetSeedTimes[Settings.Rules] = RecordTuple.Create(level.Session.Time, Settings.Seed);
                        }
                    } else {
                        this.SavedData.BestSetSeedTimes[Settings.Rules] = RecordTuple.Create(level.Session.Time, Settings.Seed);
                    }

                    if (level.Session.SeedCleanRandom()) {
                        if (this.SavedData.BestRandomSeedTimes.TryGetValue(Settings.Rules, out var prevBestRand)) {
                            if (level.Session.Time < prevBestRand.Item1) {
                                level.Session.BeatBestTimePlatinum(true);
                                this.SavedData.BestRandomSeedTimes[Settings.Rules] = RecordTuple.Create(level.Session.Time, Settings.Seed);
                            }
                        } else {
                            this.SavedData.BestRandomSeedTimes[Settings.Rules] = RecordTuple.Create(level.Session.Time, Settings.Seed);
                        }
                    }
                }

                this.SaveSettings();
            }
        }

        private void AreaCompleteDrawHash(On.Celeste.AreaComplete.orig_VersionNumberAndVariants orig, string version, float ease, float alpha) {
            orig(version, ease, alpha);

            Session session = null;
            if (Engine.Scene is AreaComplete unStatic) {
                session = unStatic.Session;
            } else if (Engine.Scene is Level unStatic2) {
                session = unStatic2.Session;
            }
            if (session?.StartedFromRandomizerMenu() ?? false) {
                var text = this.Settings.Seed;
                if (this.Settings.Rules != Ruleset.Custom) {
                    text += " " + this.Settings.Rules.ToString();
                    if (session.SeedCleanRandom()) {
                        text += "!";
                    }
                }
                text += "\n#" + this.Settings.Hash.ToString();
                text += "\nrando " + this.Metadata.VersionString;
                ActiveFont.DrawOutline(text, new Vector2(1820f + 300f * (1f - Ease.CubeOut(ease)), 894f), new Vector2(0.5f, 0f), Vector2.One * 0.5f, Color.White, 2f, Color.Black);
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
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Monocle.Calc).GetMethod("HexToColor", new Type[] {typeof(string)}));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, 6);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Microsoft.Xna.Framework.Color).GetMethod("op_Multiply"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Stloc, 5);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldstr, "994f9c");
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Monocle.Calc).GetMethod("HexToColor", new Type[] {typeof(string)}));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, 6);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Microsoft.Xna.Framework.Color).GetMethod("op_Multiply"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Stloc, 6);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Br, afterInstr);
            cursor.MarkLabel(beforeInstr);
            //Logger.Log("DEBUG", il.ToString());
        }
    }

    public static class SessionExt {
        public static bool BeatBestTimePlatinum(this Session session, bool? set=null) {
            return SessionVariable(session, "BeatBestTimePlatinum", set);
        }

        public static bool SeedCleanRandom(this Session session, bool? set=null) {
            return SessionVariable(session, "SeedCleanRandom", set);
        }

        public static bool StartedFromRandomizerMenu(this Session session, bool? set=null) {
            return SessionVariable(session, "StartedFromRandomizerMenu", set);
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