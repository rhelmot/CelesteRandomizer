using FMOD.Studio;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer {
    public partial class RandoModule : EverestModule {
        public const Overworld.StartMode STARTMODE_RANDOMIZER = (Overworld.StartMode) 55;
        
        public static RandoModule Instance;
        public RandoMetadataFile MetaConfig;
        public override Type SettingsType => typeof(RandoModuleSettings);
        public RandoModuleSettings SavedData {
            get {
                var result = Instance._Settings as RandoModuleSettings;
                if (result.CurrentVersion != this.VersionString) {
                    result.CurrentVersion = this.VersionString;
                    result.BestTimes = new Dictionary<uint, long>();
                    // intentionally do not wipe ruleset times, people have suffered enough
                    // intentionally do not wipe settings, they should upgrade gracefully
                }
                if (result.BestSetSeedTimes == null || result.BestRandomSeedTimes == null) {
                    result.BestSetSeedTimes = new Dictionary<string, RecordTuple>();
                    result.BestRandomSeedTimes = new Dictionary<string, RecordTuple>();
                }
                return result;
            }
        }

        public string VersionString {
            get {
                var result = this.Metadata.VersionString;
                if (!string.IsNullOrEmpty(this.Metadata.PathDirectory)) {
                    result += " (dev)";
                }
                return result;
            }
        }

        public RandoSettings Settings;
        public const int MAX_SEED_CHARS = 20;

        public RandoModule() {
            Instance = this;
        }
        
        public override void LoadContent(bool firstLoad) {
        }

        public override void Load() {
            Settings = this.SavedData.SavedSettings?.Copy() ?? new RandoSettings();
            On.Celeste.GameLoader.Begin += LateInitialize;
            LoadQol();
            LoadMechanics();
            LoadSessionLifecycle();
            LoadMenuLifecycle();
            Entities.LifeBerry.Load();
        }

        public Action ResetExtendedVariants;
        public Action ResetIsaVariants;
        
        private void LateInitialize(On.Celeste.GameLoader.orig_Begin orig, GameLoader self) {
            orig(self);
            var dll = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.FullName.Contains("ExtendedVariant"));
            if (dll != null) {
                var ty = dll.GetType("ExtendedVariants.Module.ExtendedVariantsModule");
                var module = ty.GetField("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                var method = ty.GetMethod("ResetToDefaultSettings", BindingFlags.Public | BindingFlags.Instance);
                ResetExtendedVariants = () => {
                    method.Invoke(module, new object[0]);
                };
            } else {
                ResetExtendedVariants = () => {};
            }
            dll = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.FullName.Contains("IsaMods"));
            if (dll != null) {
                var ty = dll.GetType("Celeste.Mod.IsaGrabBag.ForceVariantTrigger");
                var method = ty.GetMethod("SetVariantsToDefault", BindingFlags.Static | BindingFlags.Public);
                ResetIsaVariants = () => {
                    method.Invoke(null, new object[0]);
                };
            } else {
                ResetIsaVariants = () => {};
            }
            
            this.DelayedLoadMechanics();
            this.DelayedLoadQol();
        }

        public override void Unload() {
            On.Celeste.GameLoader.Begin -= LateInitialize;
            UnloadQol();
            UnloadMechanics();
            UnloadSessionLifecycle();
            UnloadMenuLifecycle();
            Entities.LifeBerry.Unload();
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            if (!inGame) {
                base.CreateModMenuSection(menu, inGame, snapshot);
            }
        }

        public RandoSettings InRandomizerSettings {
            get {
                AreaKey? key = SaveData.Instance?.CurrentSession?.Area;
                if (key == null) return null;
                AreaData area = AreaData.Get(key.Value);
                if (area == null) return null;
                var dyn = new DynData<AreaData>(area);
                var attachedSettings = dyn.Get<RandoSettings>("RandoSettings");
                return attachedSettings;
            }
        }

        public bool InRandomizer {
            get {
                return this.InRandomizerSettings != null;
            }
        }
    }
}