using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.Utils;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Randomizer
{
    public partial class RandoModule
    {

        private List<IDetour> SpecialHooksSession = new List<IDetour>();
        private void LoadSessionLifecycle()
        {
            Everest.Events.Level.OnComplete += OnComplete;
            On.Celeste.AreaComplete.VersionNumberAndVariants += AreaCompleteDrawHash;
            On.Celeste.SpeedrunTimerDisplay.Render += EndlessShowScore;
            On.Celeste.AreaComplete.Info += EndlessShowScore2;
            On.Celeste.AutoSplitterInfo.Update += MainThreadHook;
            On.Celeste.Editor.MapEditor.ctor += MarkSessionUnclean;
            On.Celeste.LevelExit.Begin += HijackExitBegin;
            On.Celeste.Player.Die += DieInEndless;
            IL.Celeste.SpeedrunTimerDisplay.DrawTime += SetPlatinumColor;
            IL.Celeste.AreaComplete.ctor += SetEndlessTitle;

            // this method is patched by everest so we need to get at the unpatched orig version
            SpecialHooksSession.Add(new ILHook(typeof(AreaComplete).GetMethod("orig_Update"), GotoNextEndless));
            SpecialHooksSession.Add(new Hook(typeof(AreaComplete).GetMethod("InitAreaCompleteInfoForEverest2"), new Action<Action<bool, Session>, bool, Session>(EverestDontIntrospect)));
        }

        private void UnloadSessionLifecycle()
        {
            Everest.Events.Level.OnComplete -= OnComplete;
            On.Celeste.AreaComplete.VersionNumberAndVariants -= AreaCompleteDrawHash;
            On.Celeste.SpeedrunTimerDisplay.Render -= EndlessShowScore;
            On.Celeste.AreaComplete.Info -= EndlessShowScore2;
            On.Celeste.AutoSplitterInfo.Update -= MainThreadHook;
            On.Celeste.Editor.MapEditor.ctor -= MarkSessionUnclean;
            On.Celeste.LevelExit.Begin -= HijackExitBegin;
            On.Celeste.Player.Die -= DieInEndless;
            IL.Celeste.SpeedrunTimerDisplay.DrawTime -= SetPlatinumColor;
            IL.Celeste.AreaComplete.ctor -= SetEndlessTitle;

            foreach (var detour in this.SpecialHooksSession)
            {
                detour.Dispose();
            }
            this.SpecialHooksSession.Clear();
        }

        private Task<AreaKey> genTask;
        private RandoSettings endingSettings = null;
        private void GotoNextEndless(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdarg(0),
                                                     instr => instr.MatchLdfld<AreaComplete>("snow")))
            {
                throw new Exception("Can't find patch point 2!");
            }

            var label = cursor.MarkLabel();

            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld<AreaComplete>("canConfirm")))
            {
                throw new Exception("Can't find patch point 1");
            }

            cursor.EmitDelegate<Func<bool>>(() =>
            {
                var settings = this.endingSettings;
                if (settings == null || settings.Algorithm != LogicType.Endless)
                {
                    return false;
                }

                if (!genTask.IsCompleted)
                {
                    typeof(AreaComplete).GetField("canConfirm", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Engine.Scene, true);
                    return true;
                }

                var session = SaveData.Instance.CurrentSession;
                var time = session.Time;
                var deaths = session.Deaths;
                var clean = session.SeedCleanRandom();
                var dynOld = new DynData<Session>(session);
                var berries = dynOld.Get<Entities.BerrySet>("GrabbedLifeBerries");
                session = new Session(genTask.Result);
                session.Time = time;
                session.Deaths = deaths;
                session.SeedCleanRandom(clean);
                var dynNew = new DynData<Session>(session);
                dynNew.Set("GrabbedLifeBerries", berries);
                UseSession = session;
                StartMe = genTask.Result;
                genTask = null;
                return true;
            });
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Brtrue, label);
        }

        private void EverestDontIntrospect(Action<bool, Session> orig, bool pieScreen, Session session)
        {
            if (this.endingSettings != null)
            {
                session = null;
            }
            orig(pieScreen, session);
        }

        private PlayerDeadBody DieInEndless(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenifinvincible, bool registerdeathinstats)
        {
            var result = orig(self, direction, evenifinvincible, registerdeathinstats);
            var settings = this.InRandomizerSettings;
            if (result == null || settings == null || !settings.HasLives)
            {
                return result;
            }

            var dyn = new DynData<Session>(SaveData.Instance.CurrentSession);
            if (dyn.Get<bool?>("SavedByTheBell") ?? false)
            {
                dyn.Set<bool?>("SavedByTheBell", false);
                return result;
            }

            result.DeathAction = () => Engine.Scene = (Scene)new LevelExit(LevelExit.Mode.GoldenBerryRestart, SaveData.Instance.CurrentSession);
            return result;
        }

        private StrawberriesCounter strawbs;
        private void SetEndlessTitle(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCall("Celeste.Dialog", "Clean")))
            {
                throw new Exception("Could not find patch point 1!");
            }

            cursor.EmitDelegate<Func<string, string>>((val) =>
            {
                if (this.endingSettings != null && this.endingSettings.Algorithm == LogicType.Endless)
                {
                    return string.Format(Dialog.Get("RANDOENDLESS_HEADER"), this.endingSettings.EndlessLevel + 1);
                }

                return val;
            });

            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCall<Scene>("Add")))
            {
                throw new Exception("Could not find patch point 2!");
            }

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<AreaComplete>>(self =>
            {
                if (this.endingSettings != null && this.endingSettings.HasLives)
                {
                    this.strawbs = new StrawberriesCounter(false, Entities.LifeBerry.GrabbedLifeBerries.Carrying)
                    {
                        Position = new Vector2(70f, 100f),
                    };
                    // :/
                    var e = new Entity();
                    e.Add(this.strawbs);
                    self.Add(e);
                }
            });
        }

        private void HijackExitBegin(On.Celeste.LevelExit.orig_Begin orig, LevelExit self)
        {
            orig(self);
            var settings = this.InRandomizerSettings;
            this.endingSettings = settings;
            if (settings == null || settings.Algorithm != LogicType.Endless)
            {
                return;
            }

            LevelExit.Mode mode = (LevelExit.Mode)typeof(LevelExit).GetField("mode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(self);
            if (mode != LevelExit.Mode.Completed)
            {
                return;
            }

            var newSettings = settings.Copy();
            newSettings.EndlessLevel++;
            Audio.SetMusic(SFX.music_complete_bside);
            Audio.SetAmbience(null);

            this.genTask = Task.Run(() =>
            {
                try
                {
                    return RandoLogic.GenerateMap(newSettings);
                }
                catch (GenerationError e)
                {
                    LevelEnter.ErrorMessage = e.Message ?? "Failed to generate area";
                    LevelEnter.Go(new Session(new AreaKey(1).SetSID("")), false);
                    return AreaKey.None;
                }
            });
        }

        internal static Builder MapBuilder = null;
        public static AreaData AreaHandoff;
        public static AreaKey? StartMe;
        public static Session UseSession;
        private bool Entering;
        private void MainThreadHook(On.Celeste.AutoSplitterInfo.orig_Update orig, AutoSplitterInfo self)
        {
            orig(self);

            if (MapBuilder?.Check() == true)
            {
                MapBuilder.Dispose();
                MapBuilder = null;
            }

            if (AreaHandoff != null)
            {
                IngestNewArea(AreaHandoff);
                AreaHandoff = null;
            }
            if (StartMe != null && !Entering)
            {
                LaunchIntoRandoArea(StartMe.Value);
            }

            // update endless mode score
            var settings = (Engine.Scene is AreaComplete) ? this.endingSettings : this.InRandomizerSettings;
            if (settings != null && settings.Algorithm == LogicType.Endless)
            {
                this.CurrentScore = ComputeScore(settings);
            }
            else
            {
                this.CurrentScore = -1;
            }
        }

        internal static void IngestNewArea(AreaData areaData)
        {
            RandoModule.Instance.ResetCachedSettings();
            if (areaData.ID < AreaData.Areas.Count)
            {
                AreaData.Areas[areaData.ID] = areaData;
            }
            else if (areaData.ID == AreaData.Areas.Count)
            {
                AreaData.Areas.Add(areaData);
            }
            else
            {
                throw new Exception("Strange edge case in the randomizer, please report this bug");
            }
            var unused = new AreaKey(AreaData.Areas.Count - 1); // does this trigger some extra behavior
        }

        internal static void LaunchIntoRandoArea(AreaKey newArea)
        {
            var area = AreaData.Get(newArea);
            var dyn = new DynData<AreaData>(area);
            var areaSettings = dyn.Get<RandoSettings>("RandoSettings");
            Audio.SetMusic(null);
            Audio.SetAmbience(null);
            Audio.Play("event:/ui/main/savefile_begin");

            // use the debug file
            SaveData.InitializeDebugMode();  // TODO (corkr900) option to not change savefiles
                                             // turn on/off variants mode
            SaveData.Instance.VariantMode = areaSettings.Variants;
            SaveData.Instance.AssistMode = false;
            SaveData.Instance.AssistModeChecks();
            // mark as completed to spawn golden berry
            // but only if we actually want the golden berry so otherwise we can see postcards
            SaveData.Instance.Areas[newArea.ID].Modes[0].Completed = areaSettings.SpawnGolden;
            // mark heart as not collected
            SaveData.Instance.Areas[newArea.ID].Modes[0].HeartGem = false;
            Instance.Entering = true;

            var unused = new FadeWipe(Engine.Scene, false, () =>
            {   // assign to variable to suppress compiler warning
                Session session;
                if (UseSession != null)
                {
                    session = UseSession;
                    UseSession = null;
                }
                else
                {
                    session = new Session(newArea)
                    {
                        FirstLevel = true,
                        StartedFromBeginning = true,
                    };
                    session.SeedCleanRandom(Instance.Settings.SeedType == SeedType.Random);
                }

                session.OldStats.Modes[0].HeartGem = false;

                var showPostcard = areaSettings.EndlessLevel == 0 && !areaSettings.SpawnGolden;
                string postcard = null;
                if (showPostcard)
                {
                    int count = Instance.SavedData.StartCounter;
                    if (count % 3 == 0)
                    {
                        int postnum = count / 3;
                        if (Dialog.Has("RANDOCARD_" + postnum))
                        {
                            postcard = Dialog.Get("RANDOCARD_" + postnum);
                            count++;
                        }
                    }
                    else
                    {
                        count++;
                    }

                    Instance.SavedData.StartCounter = count;
                    Instance.SaveSettings();
                }

                if (postcard != null)
                {
                    Dialog.Language.Dialog[area.Name + "_postcard"] = postcard;
                }
                else
                {
                    Dialog.Language.Dialog.Remove(area.Name + "_postcard");
                }

                SaveData.Instance.StartSession(session);    // need to set this earlier than we would get otherwise
                StartMe = null;
                Instance.Entering = false;
                LevelEnter.Go(session, false);
            });

            /*foreach (AreaData area in AreaData.Areas) {
				Logger.Log("randomizer", $"Skeleton for {area.GetSID()}");
				RandoConfigFile.YamlSkeleton(area);

			}*/
        }

        // when we load the map editor, effectively change to a set seed speedrun
        private void MarkSessionUnclean(On.Celeste.Editor.MapEditor.orig_ctor orig, Editor.MapEditor self, AreaKey area, bool reloadMapData)
        {
            if (Engine.Scene is Level level)
            {
                level.Session.SeedCleanRandom(false);
            }
            orig(self, area, reloadMapData);
        }

        void OnComplete(Level level)
        {
            level.Session.BeatBestTimePlatinum(false);
            var settings = this.InRandomizerSettings;
            if (settings != null && level.Session.StartedFromBeginning)
            {  // how strong can/should we make this condition?
                var hash = uint.Parse(settings.Hash); // convert and unconvert, yeah I know

                level.Session.BeatBestTime = false;
                if (this.SavedData.BestTimes.TryGetValue(hash, out long prevBest))
                {
                    if (level.Session.Time < prevBest)
                    {
                        level.Session.BeatBestTime = true;
                        this.SavedData.BestTimes[hash] = level.Session.Time;
                    }
                }
                else
                {
                    this.SavedData.BestTimes[hash] = level.Session.Time;
                }

                if (!String.IsNullOrEmpty(settings.Rules))
                {
                    long submittedValue = settings.Algorithm == LogicType.Endless ? this.CurrentScore : level.Session.Time;
                    Func<long, bool> betterthan = oldval => settings.Algorithm == LogicType.Endless ? (submittedValue > oldval) : (submittedValue < oldval);
                    if (settings.Algorithm != LogicType.Endless || settings.SeedType != SeedType.Random)
                    {
                        // unless we're playing endless, allow random seeds to count toward set seed records
                        if (this.SavedData.BestSetSeedTimes.TryGetValue(settings.Rules, out var prevBestSet))
                        {
                            if (betterthan(prevBestSet.Item1))
                            {
                                level.Session.BeatBestTimePlatinum(true);
                                this.SavedData.BestSetSeedTimes[settings.Rules] = RecordTuple.Create(submittedValue, settings.Seed);
                            }
                        }
                        else
                        {
                            this.SavedData.BestSetSeedTimes[settings.Rules] = RecordTuple.Create(submittedValue, settings.Seed);
                        }
                    }

                    if (level.Session.SeedCleanRandom())
                    {
                        if (this.SavedData.BestRandomSeedTimes.TryGetValue(settings.Rules, out var prevBestRand))
                        {
                            if (betterthan(prevBestRand.Item1))
                            {
                                level.Session.BeatBestTimePlatinum(true);
                                this.SavedData.BestRandomSeedTimes[settings.Rules] = RecordTuple.Create(submittedValue, settings.Seed);
                            }
                        }
                        else
                        {
                            this.SavedData.BestRandomSeedTimes[settings.Rules] = RecordTuple.Create(submittedValue, settings.Seed);
                        }
                    }
                }

                this.SaveSettings();
            }
        }

        private void AreaCompleteDrawHash(On.Celeste.AreaComplete.orig_VersionNumberAndVariants orig, string version, float ease, float alpha)
        {
            orig(version, ease, alpha);

            var settings = this.endingSettings;
            var session = SaveData.Instance?.CurrentSession;
            if (settings != null)
            {
                var text = settings.Seed;
                if (!string.IsNullOrEmpty(settings.Rules))
                {
                    text += " " + settings.Rules;
                    if (session?.SeedCleanRandom() ?? false)
                    {
                        text += "!";
                    }
                }

                text += "\n#" + settings.Hash;
                text += "\nrando " + this.VersionString;
                var variants = SaveData.Instance?.VariantMode ?? false;
                ActiveFont.DrawOutline(text, new Vector2(1820f + 300f * (1f - Ease.CubeOut(ease)), variants ? 810f : 894f), new Vector2(0.5f, 0f), Vector2.One * 0.5f, settings.SpawnGolden ? Calc.HexToColor("fad768") : Color.White, 2f, Color.Black);
            }
        }

        private void SetPlatinumColor(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdarg(5)))
            {
                throw new Exception("Failed to find patch spot 2 [first pass]");
            }
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdcI4(0)))
            {
                throw new Exception("Failed to find patch spot 1");
            }
            var afterInstr = cursor.MarkLabel();

            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchLdarg(5)))
            {
                throw new Exception("Failed to find patch spot 2");
            }

            cursor.EmitDelegate<Func<bool>>(() =>
            {
                if (!this.InRandomizer)
                {
                    return false;
                }
                if (Engine.Scene is Level level)
                {
                    return level.Session.BeatBestTimePlatinum();
                }
                return false;
            });

            var beforeInstr = cursor.DefineLabel();
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Brfalse, beforeInstr);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldstr, "cb19d2");
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Calc).GetMethod("HexToColor", new[] { typeof(string) }));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, 6);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Color).GetMethod("op_Multiply"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Stloc, 5);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldstr, "994f9c");
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Calc).GetMethod("HexToColor", new[] { typeof(string) }));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, 6);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Color).GetMethod("op_Multiply"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Stloc, 6);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Br, afterInstr);
            cursor.MarkLabel(beforeInstr);
        }

        public int CurrentScore;
        public static int ComputeScore(RandoSettings settings)
        {
            float time = (float)TimeSpan.FromTicks(SaveData.Instance.CurrentSession.Time).TotalSeconds;
            float berries = Entities.LifeBerry.GrabbedLifeBerries.Carrying;
            float levels = settings.EndlessLevel + 1;
            float score;

            // scoring consts
            // formatted like this in case we want to have per-settings scores
            float levelBonus, berryBonus, timeDecay;
            if (settings.SeedType == SeedType.Custom)
            {
                levelBonus = 150f;
                berryBonus = 40f;
                timeDecay = 0.1f / (60f * 10f);
            }
            else
            {
                levelBonus = 450f;
                berryBonus = 60f;
                timeDecay = 0.1f / (60f * 15f);
            }
            score = levels * levelBonus + berries * berryBonus - time - timeDecay / 2f * time * time;

            return (int)score;
        }

        private void EndlessShowScore(On.Celeste.SpeedrunTimerDisplay.orig_Render orig, SpeedrunTimerDisplay self)
        {
            var settings = this.InRandomizerSettings;
            if (settings == null || settings.Algorithm != LogicType.Endless || global::Celeste.Settings.Instance.SpeedrunClock == SpeedrunType.Off)
            {
                orig(self);
                return;
            }

            float x = -300f * Ease.CubeIn(1f - self.DrawLerp);
            var scene = Engine.Scene as Level;
            if (scene == null)
            {
                return;
            }
            var session = scene.Session;
            var wiggler = (Wiggler)typeof(SpeedrunTimerDisplay).GetField("wiggler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(self);
            var bg = (MTexture)typeof(SpeedrunTimerDisplay).GetField("bg", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(self);

            string timeString = TimeSpan.FromTicks(session.Time).ShortGameplayFormat();
            string scoreString = Dialog.Clean("RANDOENDLESS_SCORE") + " " + this.CurrentScore;

            bg.Draw(new Vector2(x, self.Y));
            SpeedrunTimerDisplay.DrawTime(new Vector2(x + 32f, self.Y + 44f), timeString);
            Draw.Rect(x, self.Y + 38f, (float)(96 + 2), 22.8f, Color.Black); // ???
            bg.Draw(new Vector2(x + 96, self.Y + 38f), Vector2.Zero, Color.White, 0.6f);
            SpeedrunTimerDisplay.DrawTime(new Vector2(x + 32f, (float)((double)self.Y + 40.0 + 26.400001525878906)), scoreString, (float)((1.0 + (double)wiggler.Value * 0.15000000596046448) * 0.6000000238418579), session.StartedFromBeginning, scene.Completed, session.BeatBestTime, 0.6f);
        }

        private void EndlessShowScore2(On.Celeste.AreaComplete.orig_Info orig, float ease, string speedruntimerchapterstring, string speedruntimerfilestring, string chapterspeedruntext, string versiontext)
        {
            var settings = Engine.Scene is AreaComplete ? this.endingSettings : this.InRandomizerSettings;
            var savedSetting = global::Celeste.Settings.Instance.SpeedrunClock;
            if (savedSetting != SpeedrunType.Off && settings != null && settings.Algorithm == LogicType.Endless)
            {
                global::Celeste.Settings.Instance.SpeedrunClock = SpeedrunType.Chapter;
            }
            orig(ease, speedruntimerchapterstring, speedruntimerfilestring, chapterspeedruntext, versiontext);
            global::Celeste.Settings.Instance.SpeedrunClock = savedSetting;

            if (settings != null && settings.Algorithm == LogicType.Endless && savedSetting != SpeedrunType.Off)
            {
                Vector2 position = new Vector2((float)(80.0 - 300.0 * (1.0 - (double)Ease.CubeOut(ease))), 1000f);
                var scoreSpeedrunText = Dialog.Clean("RANDOENDLESS_SCORE");
                ActiveFont.DrawOutline(scoreSpeedrunText, position + new Vector2(0.0f, 40f), new Vector2(0.0f, 1f), Vector2.One * 0.6f, Color.White, 2f, Color.Black);
                SpeedrunTimerDisplay.DrawTime(position + new Vector2((float)((double)ActiveFont.Measure(scoreSpeedrunText).X * 0.6000000238418579 + 8.0), 40f), this.CurrentScore.ToString(), 0.6f);
            }
        }
    }

    public static class SessionExt
    {
        public static bool BeatBestTimePlatinum(this Session session, bool? set = null)
        {
            return SessionVariable(session, "BeatBestTimePlatinum", set);
        }

        public static bool SeedCleanRandom(this Session session, bool? set = null)
        {
            return SessionVariable(session, "SeedCleanRandom", set);
        }

        private static bool SessionVariable(Session session, string name, bool? set = null)
        {
            var dyn = new DynData<Session>(session);
            if (set != null)
            {
                dyn.Set<bool>(name, set.Value);
                return set.Value;
            }
            else
            {
                return dyn.Get<bool?>(name) ?? false;
            }
        }
    }
}
