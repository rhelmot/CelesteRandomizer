using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Monocle;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MonoMod.RuntimeDetour;
using Celeste.Mod.Entities;
using On.Celeste.Mod.Entities;
using Celeste.Mod.Randomizer.Entities;
using System.Runtime.Remoting.Contexts;

namespace Celeste.Mod.Randomizer
{
    public partial class RandoModule : EverestModule
    {
        private List<IDetour> SpecialHooksQol = new List<IDetour>();
        private void LoadQol()
        {
            On.Celeste.TextMenu.MoveSelection += DisableMenuMovement;
            On.Celeste.Cassette.CollectRoutine += NeverCollectCassettes;
            On.Celeste.AngryOshiro.Added += DontSpawnTwoOshiros;
            On.Celeste.BadelineOldsite.Added += PlayBadelineCutscene;
            On.Celeste.Player.SummitLaunchUpdate += SummitLaunchReset;
            On.Celeste.Player.Added += DontMoveOnWakeup;
            On.Celeste.Dialog.Clean += PlayMadlibs1;
            On.Celeste.Dialog.Get += PlayMadlibs2;
            On.Celeste.Spikes.Render += TentacleOutline;
            On.Celeste.Poem.ctor += PoemColor;
            IL.Celeste.Level.EnforceBounds += DontBlockOnTheo;
            IL.Celeste.TheoCrystal.Update += BeGracefulOnTransitions;
            IL.Celeste.SummitGem.OnPlayer += GemRefillsDashes;
            IL.Celeste.SummitGem.OnPlayer += DashlessAccessability;
            IL.Celeste.HeartGem.OnPlayer += DashlessAccessability;
            IL.Celeste.CS10_Gravestone.OnEnd += DontGiveTwoDashes;
            IL.Celeste.CS10_Gravestone.BadelineRejoin += DontGiveTwoDashes;
            IL.Celeste.CS07_Ascend.OnEnd += DontGiveTwoDashes;
            IL.Celeste.CS07_Ascend.Cutscene += DontGiveTwoDashes;
            IL.Celeste.AngryOshiro.ChaseUpdate += MoveOutOfTheWay;
            IL.Celeste.NPC03_Oshiro_Lobby.Added += PleaseDontStopTheMusic;
            IL.Celeste.EventTrigger.OnEnter += DontGiveOneDash;
            IL.Celeste.CS10_MoonIntro.OnEnd += DontGiveOneDash;
            IL.Celeste.CS10_BadelineHelps.OnEnd += DontGiveOneDash;
            IL.Celeste.CS06_Campfire.OnBegin += FuckUpLess;
            On.Celeste.CS06_Campfire.OnBegin += FuckUpEvenLess;
            IL.Celeste.CS06_Campfire.OnEnd += FuckUpWayLess;
            IL.Celeste.LightningRenderer.Track += TrackExtraSpace;
            On.Celeste.LockBlock.OnPlayer += NoKeySkips;
            On.Celeste.Player.Update += PreventInvincibility;
            On.Celeste.RisingLava.Update += StopTheLava;
            On.Celeste.ClutterSwitch.Update += AllowClutterSwitchPress_ZeroDash;

            // https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/master/CelesteTAS-EverestInterop/EverestInterop/DisableAchievements.cs
            // Before hooking Achievements.Register, check the size of the method.
            // If it is 4 instructions long, hooking it is unnecessary and even causes issues.
            using (DynamicMethodDefinition statsDMD = new DynamicMethodDefinition(typeof(Achievements).GetMethod("Register")))
            {
                int instructionCount = statsDMD.Definition.Body.Instructions.Count;
                if (instructionCount > 4)
                {
                    On.Celeste.Achievements.Register += NoAchievements;
                }
            }

            SpecialHooksQol.Add(new ILHook(typeof(CS10_MoonIntro).GetMethod("BadelineAppears", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget(), DontGiveOneDash));
            SpecialHooksQol.Add(new ILHook(typeof(CS10_Gravestone).GetMethod("BadelineAppears", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget(), DontGiveOneDash));
            SpecialHooksQol.Add(new ILHook(typeof(CS10_Gravestone).GetMethod("BadelineRejoin", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget(), DontGiveTwoDashes));
            SpecialHooksQol.Add(new ILHook(typeof(EventTrigger).GetNestedType("<>c__DisplayClass10_0", BindingFlags.NonPublic).GetMethod("<OnEnter>b__0", BindingFlags.NonPublic | BindingFlags.Instance), DontGiveOneDash));
            SpecialHooksQol.Add(new Hook(typeof(EventTrigger).GetNestedType("<>c__DisplayClass10_0", BindingFlags.NonPublic).GetMethod("<OnEnter>b__0", BindingFlags.NonPublic | BindingFlags.Instance), new Action<Action<object>, object>(this.TransferGoldenBerries)));
            SpecialHooksQol.Add(new ILHook(typeof(HeartGem).GetMethod("CollectRoutine", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget(), HeartSfx));
        }

        private void DelayedLoadQol()
        {
            // needs to be delayed so we patch AFTER pridehearts
            IL.Celeste.HeartGem.Awake += SpecialHeartColors;
        }

        private void UnloadQol()
        {
            On.Celeste.TextMenu.MoveSelection -= DisableMenuMovement;
            On.Celeste.Cassette.CollectRoutine -= NeverCollectCassettes;
            On.Celeste.AngryOshiro.Added -= DontSpawnTwoOshiros;
            On.Celeste.BadelineOldsite.Added -= PlayBadelineCutscene;
            On.Celeste.Player.SummitLaunchUpdate -= SummitLaunchReset;
            On.Celeste.Player.Added -= DontMoveOnWakeup;
            On.Celeste.Dialog.Clean -= PlayMadlibs1;
            On.Celeste.Dialog.Get -= PlayMadlibs2;
            On.Celeste.Spikes.Render -= TentacleOutline;
            On.Celeste.Poem.ctor -= PoemColor;
            IL.Celeste.Level.EnforceBounds -= DontBlockOnTheo;
            IL.Celeste.TheoCrystal.Update -= BeGracefulOnTransitions;
            IL.Celeste.SummitGem.OnPlayer -= GemRefillsDashes;
            IL.Celeste.SummitGem.OnPlayer -= DashlessAccessability;
            IL.Celeste.HeartGem.OnPlayer -= DashlessAccessability;
            IL.Celeste.CS10_Gravestone.OnEnd -= DontGiveTwoDashes;
            IL.Celeste.CS10_Gravestone.BadelineRejoin -= DontGiveTwoDashes;
            IL.Celeste.CS07_Ascend.OnEnd -= DontGiveTwoDashes;
            IL.Celeste.CS07_Ascend.Cutscene -= DontGiveTwoDashes;
            IL.Celeste.AngryOshiro.ChaseUpdate -= MoveOutOfTheWay;
            IL.Celeste.NPC03_Oshiro_Lobby.Added -= PleaseDontStopTheMusic;
            IL.Celeste.EventTrigger.OnEnter -= DontGiveOneDash;
            IL.Celeste.CS10_MoonIntro.OnEnd -= DontGiveOneDash;
            IL.Celeste.CS10_BadelineHelps.OnEnd -= DontGiveOneDash;
            IL.Celeste.CS06_Campfire.OnBegin -= FuckUpLess;
            On.Celeste.CS06_Campfire.OnBegin -= FuckUpEvenLess;
            IL.Celeste.CS06_Campfire.OnEnd -= FuckUpWayLess;
            IL.Celeste.LightningRenderer.Track -= TrackExtraSpace;
            IL.Celeste.HeartGem.Awake -= SpecialHeartColors;
            On.Celeste.LockBlock.OnPlayer -= NoKeySkips;
            On.Celeste.Player.Update -= PreventInvincibility;
            On.Celeste.RisingLava.Update -= StopTheLava;
            On.Celeste.ClutterSwitch.Update -= AllowClutterSwitchPress_ZeroDash;

            foreach (var detour in this.SpecialHooksQol)
            {
                detour.Dispose();
            }
            this.SpecialHooksQol.Clear();
        }

        private void NoKeySkips(On.Celeste.LockBlock.orig_OnPlayer orig, LockBlock self, Player player)
        {
            if (this.InRandomizer)
            {
                var blockCount = Engine.Scene.Entities.Count(e => e is LockBlock);
                var keyCount = Engine.Scene.Entities.Count(e => e is Key);

                if (blockCount > keyCount)
                {
                    return;
                }
            }

            orig(self, player);
        }

        private void HeartSfx(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("event:/game/general/crystalheart_blue_get")))
            {
                throw new Exception("Could not find patch point");
            }

            cursor.EmitDelegate<Func<string, string>>(sfx =>
            {
                var settings = this.InRandomizerSettings;
                if (settings == null)
                {
                    return sfx;
                }

                switch (settings.Difficulty)
                {
                    case Difficulty.Easy:
                    case Difficulty.Normal:
                    case Difficulty.Hard:
                        sfx = "event:/game/general/crystalheart_blue_get";
                        break;
                    case Difficulty.Expert:
                    case Difficulty.Master:
                        sfx = "event:/game/general/crystalheart_red_get";
                        break;
                    case Difficulty.Perfect:
                        sfx = "event:/game/general/crystalheart_gold_get";
                        break;
                }

                return sfx;
            });
        }

        private void PoemColor(On.Celeste.Poem.orig_ctor orig, Poem self, string text, int heartindex, float heartalpha)
        {
            orig(self, text, heartindex, heartalpha);

            var settings = this.InRandomizerSettings;
            if (heartindex == 3 || settings == null)
            {
                return;
            }

            Color color = Color.White;
            string guiSprite = "";
            switch (settings.Difficulty)
            {
                case Difficulty.Easy:
                    color = Calc.HexToColor("20c020");
                    guiSprite = "heartgem0";
                    break;
                case Difficulty.Normal:
                    color = Calc.HexToColor("8cc7fa");
                    guiSprite = "heartgem0";
                    break;
                case Difficulty.Hard:
                    color = Calc.HexToColor("ff668a");
                    guiSprite = "heartgem1";
                    break;
                case Difficulty.Expert:
                    color = Calc.HexToColor("fffc24");
                    guiSprite = "heartgem2";
                    break;
                case Difficulty.Master:
                    color = Calc.HexToColor("ff5024");
                    guiSprite = "Randomizer_HeartMaster";
                    break;
                case Difficulty.Perfect:
                    color = Calc.HexToColor("9B3FB5");
                    guiSprite = "Randomizer_HeartPerfect";
                    break;
            }

            typeof(Poem).GetProperty("Color").SetValue(self, color);

            self.Heart = GFX.GuiSpriteBank.Create(guiSprite);
            self.Heart.Play("spin");
            self.Heart.Position = new Vector2(1920f, 1080f) * 0.5f;
            self.Heart.Color = Color.White * heartalpha;
        }

        private void SpecialHeartColors(ILContext il)
        {
            // this code copied from pridehearts, of course
            VariableDefinition someString = null;
            foreach (VariableDefinition variable in il.Body.Variables)
            {
                if (variable.VariableType.FullName == typeof(string).FullName)
                {
                    someString = variable;
                    break;
                }
            }

            if (someString == null)
            {
                throw new Exception("Could not find variable defn to patch!");
            }

            var cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdloc(someString.Index)))
            {
                cursor.EmitDelegate<Func<string, string>>(id =>
                {
                    var settings = this.InRandomizerSettings;
                    if (id == "heartgem3" || settings == null)
                    {
                        return id;
                    }

                    var prideMode = AppDomain.CurrentDomain.GetAssemblies().Any(asm => asm.FullName.Contains("PrideHearts"));

                    switch (settings.Difficulty)
                    {
                        case Difficulty.Easy:
                            id = prideMode ? "heartgem0" : "Randomizer_HeartEasy";
                            break;
                        case Difficulty.Normal:
                            id = "heartgem0";
                            break;
                        case Difficulty.Hard:
                            id = "heartgem1";
                            break;
                        case Difficulty.Expert:
                            id = "heartgem2";
                            break;
                        case Difficulty.Master:
                            id = prideMode ? "heartgem3" : "Randomizer_HeartMaster";
                            break;
                        case Difficulty.Perfect:
                            id = prideMode ? "heartGemGhost" : "Randomizer_HeartPerfect";
                            break;
                    }
                    return id;
                });
            }
        }

        private void NoAchievements(On.Celeste.Achievements.orig_Register orig, Achievement achievement)
        {
            if (!this.InRandomizer)
            {
                orig(achievement);
            }
        }

        private void TentacleOutline(On.Celeste.Spikes.orig_Render orig, Spikes self)
        {
            if (this.InRandomizer && (string)typeof(Spikes).GetField("spikeType", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(self) == "tentacles")
            {
                self.SetSpikeColor(Color.Black);
                var onShake = typeof(Spikes).GetMethod("OnShake", BindingFlags.Instance | BindingFlags.NonPublic);
                onShake.Invoke(self, new object[] { new Vector2(1, 0) });
                orig(self);
                onShake.Invoke(self, new object[] { new Vector2(-1, 1) });
                orig(self);
                onShake.Invoke(self, new object[] { new Vector2(-1, -1) });
                orig(self);
                onShake.Invoke(self, new object[] { new Vector2(1, -1) });
                orig(self);
                onShake.Invoke(self, new object[] { new Vector2(0, 1) });
                self.SetSpikeColor(Color.White);
            }

            orig(self);
        }

        private static string MadlibBlank(string description, string hash, string seed)
        {
            if (description.Contains(":"))
            {
                var split = description.Split(':');
                description = split[0];
                seed = split[1];
            }

            int count;
            try
            {
                count = int.Parse(Dialog.Get("RANDOHEART_" + description + "_COUNT"));
            }
            catch (FormatException)
            {
                throw new Exception("Bad key: RANDOHEART_" + description + "_COUNT");
            }
            var r = new Random((int)RandoSettings.djb2(hash + seed));
            var picked = r.Next(count);
            var result = Dialog.Get("RANDOHEART_" + description + "_" + picked.ToString());
            if (char.IsLower(description[0]))
            {
                result = result.ToLower();
            }
            return result;
        }

        private string PlayMadlibs(Func<string, string> orig, string name)
        {
            RandoSettings settings;
            if (!Dialog.Has("RANDO_" + name) || (settings = this.InRandomizerSettings) == null)
            {
                return orig(name);
            }

            var thing = orig("RANDO_" + name);
            var i = 0;
            while (thing.Contains("(RANDO:"))
            {
                var idx = thing.IndexOf("(RANDO:");
                var startidx = idx + "(RANDO:".Length;
                var endidx = thing.IndexOf(')', idx);
                var description = thing.Substring(startidx, endidx - startidx);
                thing = thing.Remove(idx, endidx + 1 - idx).Insert(idx, MadlibBlank(description, settings.Hash + settings.EndlessLevel, name + (i * 55).ToString()));
                i++;
            }
            return thing;
        }

        private string PlayMadlibs2(On.Celeste.Dialog.orig_Get orig, string name, Language language)
        {
            return PlayMadlibs(s => orig(s, language), name);
        }

        private string PlayMadlibs1(On.Celeste.Dialog.orig_Clean orig, string name, Language language)
        {
            return PlayMadlibs(s => orig(s, language), name);
        }

        private void DisableMenuMovement(On.Celeste.TextMenu.orig_MoveSelection orig, TextMenu self, int direction, bool wiggle = false)
        {
            if (self is DisablableTextMenu newself && newself.DisableMovement)
            {
                return;
            }
            orig(self, direction, wiggle);
        }

        private IEnumerator NeverCollectCassettes(On.Celeste.Cassette.orig_CollectRoutine orig, Cassette self, Player player)
        {
            var thing = orig(self, player);
            while (thing.MoveNext())
            {  // why does it not let me use foreach?
                yield return thing.Current;
            }

            if (this.InRandomizer)
            {
                var level = self.Scene as Level;
                level.Session.Cassette = false;
            }
        }

        private void PlayBadelineCutscene(On.Celeste.BadelineOldsite.orig_Added orig, BadelineOldsite self, Scene scene)
        {
            orig(self, scene);
            var level = scene as Level;
            if (!level.Session.GetFlag("evil_maddy_intro") && level.Session.Level.StartsWith("Celeste/2-OldSite/A/3"))
            {
                foreach (var c in self.Components)
                {
                    if (c is Coroutine)
                    {
                        self.Components.Remove(c);
                        break;
                    }
                }

                self.Hovering = false;
                self.Visible = true;
                self.Hair.Visible = false;
                self.Sprite.Play("pretendDead", false, false);
                if (level.Session.Area.Mode == AreaMode.Normal)
                {
                    level.Session.Audio.Music.Event = null;
                    level.Session.Audio.Apply(false);
                }
                scene.Add(new CS02_BadelineIntro(self));
            }
        }

        private int SummitLaunchReset(On.Celeste.Player.orig_SummitLaunchUpdate orig, Player self)
        {
            var level = Engine.Scene as Level;
            if (this.InRandomizer && self.Y < level.Bounds.Y + 0)
            {
                // teleport to spawn point
                self.Position = level.Session.RespawnPoint.Value;

                // reset camera
                var tmp = level.CameraLockMode;
                level.CameraLockMode = Level.CameraLockModes.None;
                level.Camera.Position = level.GetFullCameraTargetAt(self, self.Position);
                level.CameraLockMode = tmp;
                level.CameraUpwardMaxY = level.Camera.Y + 180f;

                // remove effects
                AscendManager mgr = null;
                Entity fader = null;
                HeightDisplay h = null;
                BadelineDummy b = null;
                foreach (var ent in Engine.Scene.Entities)
                {
                    if (ent is AscendManager manager)
                    {
                        mgr = manager;
                    }
                    if (ent.GetType().Name == "Fader")
                    {
                        fader = ent;
                    }
                    if (ent is HeightDisplay heightDisplay)
                    {
                        h = heightDisplay;
                    }
                    if (ent is BadelineDummy bd)
                    {
                        b = bd;
                    }
                }
                if (mgr != null)
                {
                    level.Remove(mgr);
                }
                if (fader != null)
                {
                    level.Remove(fader);
                }
                if (h != null)
                {
                    level.Remove(h);
                }
                if (b != null)
                {
                    level.Remove(b);
                }
                level.NextTransitionDuration = 0.65f;

                // return to normal
                return Player.StNormal;
            }
            else
            {
                return orig(self);
            }
        }

        private void DontSpawnTwoOshiros(On.Celeste.AngryOshiro.orig_Added orig, AngryOshiro self, Scene scene)
        {
            orig(self, scene);
            var level = scene as Level;
            if (!level.Session.GetFlag("oshiro_resort_roof") && level.Session.Level.StartsWith("Celeste/3-CelestialResort/A/roof00"))
            {
                self.RemoveSelf();
            }
        }

        private void DontMoveOnWakeup(On.Celeste.Player.orig_Added orig, Player self, Scene scene)
        {
            orig(self, scene);
            if (this.InRandomizer)
            {
                self.JustRespawned = true;
            }
        }

        private void DontBlockOnTheo(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<Monocle.Tracker>("GetEntity"));
            cursor.EmitDelegate<Func<TheoCrystal, TheoCrystal>>((theo) =>
            {
                return this.InRandomizer ? null : theo;
            });
        }

        private void BeGracefulOnTransitions(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt<Level>("get_Bounds")))
            {
                cursor.Remove();
                cursor.EmitDelegate<Func<Level, Rectangle>>((level) =>
                {
                    if (level.Transitioning && this.InRandomizer)
                    {
                        return level.Session.MapData.Bounds;
                    }
                    return level.Bounds;
                });
            }
        }

        private void DashlessAccessability(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<Player>("get_DashAttacking"));
            cursor.EmitDelegate<Func<bool, bool>>((dobreak) =>
            {
                if ((this.InRandomizerSettings?.Dashes ?? NumDashes.One) == NumDashes.Zero)
                {
                    return true;
                }
                return dobreak;
            });
        }

        private void GemRefillsDashes(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<Monocle.Entity>("Add"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_1);
            cursor.EmitDelegate<Action<Player>>((player) =>
            {
                if (this.InRandomizer)
                {
                    player.RefillDash();
                }
            });
        }

        private void DontGiveTwoDashes(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcI4(2)))
            {
                cursor.EmitDelegate<Func<int, int>>((dashes) =>
                {
                    if (this.InRandomizer)
                    {
                        return (Engine.Scene as Level).Session.Inventory.Dashes;
                    }
                    return dashes;
                });
            }
        }

        private void DontGiveOneDash(ILContext il)
        {
            var cursor = new ILCursor(il);
            var count = 0;
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStfld("Celeste.PlayerInventory", "Dashes")))
            {
                cursor.EmitDelegate<Func<int, int>>((dashes) =>
                {
                    if (this.InRandomizer)
                    {
                        return (Engine.Scene as Level).Session.Inventory.Dashes;
                    }
                    return dashes;
                });
                cursor.Index++;
                count++;
            }
            if (count == 0)
            {
                throw new Exception("Could not find patch point(s)!");
            }
        }

        private void MoveOutOfTheWay(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<AngryOshiro>("get_TargetY"));
            cursor.EmitDelegate<Func<float, float>>((targety) =>
            {
                if (this.InRandomizer)
                {
                    var level = Engine.Scene as Level;
                    var player = level.Tracker.GetEntity<Player>();
                    if (player.Facing == Facings.Left && player.X < level.Bounds.X + 70)
                    {
                        return targety - 50;
                    }
                }
                return targety;
            });
        }

        private void PleaseDontStopTheMusic(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<AudioTrackState>("set_Event"));
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt<AudioTrackState>("set_Event")))
            {
                throw new Exception("Could not find patching spot");
            }
            cursor.Remove();
            cursor.EmitDelegate<Action<AudioTrackState, string>>((music, track) =>
            {
                if (!this.InRandomizer)
                {
                    music.Event = track;
                }
            });
        }

        private void FuckUpLess(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<Session>("GetFlag")))
            {
                throw new Exception("Could not find patching spot 1");
            }

            cursor.EmitDelegate<Func<bool, bool>>(thing => this.InRandomizer ? true : thing);

            /*cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdarg(0),
                                                     instr => instr.MatchLdfld("Celeste.CS06_Campfire", "player"))) {
                throw new Exception("Could not find patching spot 2");
            }

            var label = cursor.DefineLabel();
            cursor.EmitDelegate<Func<bool>>(() => this.InRandomizer);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Brtrue, label);

            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStfld("Monocle.StateMachine", "Locked"))) {
                throw new Exception("Could not find patching spot 3");
            }
            cursor.MarkLabel(label);*/
        }

        private void FuckUpEvenLess(On.Celeste.CS06_Campfire.orig_OnBegin orig, CS06_Campfire self, Level level)
        {
            var player = level.Tracker.GetEntity<Player>();
            var savedX = player.X;
            orig(self, level);
            if (this.InRandomizer)
            {
                player.X = savedX;
                player.StateMachine.Locked = false;
                player.StateMachine.State = 0;
            }
        }

        private void FuckUpWayLess(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdarg(0),
                                                     instr => instr.MatchLdfld("Celeste.CS06_Campfire", "player"),
                                                     instr => instr.MatchLdfld("Celeste.Player", "Sprite")))
            {
                throw new Exception("Could not find patching spot 1");
            }

            var label = cursor.DefineLabel();
            cursor.EmitDelegate<Func<bool>>(() => this.InRandomizer);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Brtrue, label);

            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdarg(0),
                                                     instr => instr.MatchCall("Monocle.Entity", "RemoveSelf")))
            {
                throw new Exception("Could not find patching spot 2");
            }

            cursor.MarkLabel(label);
        }

        private void TransferGoldenBerries(Action<object> orig, object self)
        {
            var level = Engine.Scene as Level;
            var player = level.Tracker.GetEntity<Player>();
            var leader = player.Get<Leader>();
            foreach (var follower in leader.Followers)
            {
                if (follower.Entity != null)
                {
                    follower.Entity.Position -= player.Position;
                    follower.Entity.AddTag(Tags.Global);
                    level.Session.DoNotLoad.Add(follower.ParentEntityID);
                }
            }
            for (int i = 0; i < leader.PastPoints.Count; i++)
            {
                leader.PastPoints[i] -= player.Position;
            }
            orig(self);
            foreach (var follower in leader.Followers)
            {
                if (follower.Entity != null)
                {
                    follower.Entity.Position += player.Position;
                    follower.Entity.RemoveTag(Tags.Global);
                    level.Session.DoNotLoad.Remove(follower.ParentEntityID);
                }
            }
            for (int i = 0; i < leader.PastPoints.Count; i++)
            {
                leader.PastPoints[i] += player.Position;
            }
            leader.TransferFollowers();
        }

        private void TrackExtraSpace(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld("Microsoft.Xna.Framework.Rectangle", "Height")))
            {
                throw new Exception("Could not find patch point!");
            }
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, 32);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Add);
        }

        [Command("madlibs_stats", "run statistical tests on the madlibs")]
        public static void MadlibsStats(string blank = "WAVEDASHING", int runs = 100000)
        {
            var map = new Dictionary<string, int>();
            var seed = "1";
            for (int i = 0; i < runs; i++)
            {
                var result = MadlibBlank(blank, new Random().Next().ToString(), seed);
                map[result] = (map.TryGetValue(result, out int j) ? j : 0) + 1;
            }

            foreach (var kv in map.OrderBy(x => x.Value))
            {
                Engine.Commands.Log($"{kv.Value} {kv.Key}");
            }
        }
        public static void PreventInvincibility(On.Celeste.Player.orig_Update orig, Player self)
        {
            Level lvl = (Engine.Scene as Level);
            if (lvl.InCutscene && lvl.Entities.OfType<CS_FindTheoPhone>().Any() && lvl.Entities.OfType<CS_FindTheoPhone>().ToList()[0].State == 1 && self.StateMachine == 0 ||
                lvl.InCutscene && lvl.Entities.OfType<CS_FindTheoPhone>().Any() && lvl.Entities.OfType<CutsceneEntity>().Count() > 1)
            {
                lvl.Entities.OfType<CS_FindTheoPhone>().ToList()[0].OnEnd(lvl);
            }
            orig(self);
        }

        public void StopTheLava(On.Celeste.RisingLava.orig_Update orig, RisingLava self)
        {
            if (this.InRandomizer)
            {
                var level = (Engine.Scene as Level);
                var player = level.Tracker.GetEntity<Player>();

                // The four intro states that restrict player movement
                HashSet<int> launchStates = new HashSet<int>() { 13, 15, 23, 25 }; 
                if (player != null && launchStates.Contains(player.StateMachine.State))
                {
                    var lavaData = DynamicData.For(self);
                    lavaData.Set("waiting", true);
                }
                    
            }
            orig(self);
        }
        public void AllowClutterSwitchPress_ZeroDash(On.Celeste.ClutterSwitch.orig_Update orig, ClutterSwitch self)
         {
            orig(self);
            if (self.HasPlayerOnTop() && (this.InRandomizerSettings?.Dashes ?? NumDashes.One) == NumDashes.Zero)
            {
                DynamicData clutterSwitchData = DynamicData.For(self);
                Player player = Engine.Scene.Tracker.GetEntity<Player>();
                clutterSwitchData.Invoke("OnDashed", player, Vector2.UnitY);
            }
        }

    }

    public class DisablableTextMenu : TextMenu
    {
        public bool DisableMovement;
    }
}
