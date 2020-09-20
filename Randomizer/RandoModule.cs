using FMOD.Studio;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer {
    public partial class RandoModule : EverestModule {
        public const LevelExit.Mode LEVELEXIT_ENDLESS = (LevelExit.Mode) 56;
        public const Overworld.StartMode STARTMODE_RANDOMIZER = (Overworld.StartMode) 55;
        
        public static RandoModule Instance;
        public RandoMetadataFile MetaConfig;
        public override Type SettingsType => typeof(RandoModuleSettings);
        public RandoModuleSettings SavedData {
            get {
                var result = Instance._Settings as RandoModuleSettings;
                if (result.CurrentVersion != this.Metadata.VersionString) {
                    result.CurrentVersion = this.Metadata.VersionString;
                    result.BestTimes = new Dictionary<uint, long>();
                    result.BestSetSeedTimes = new Dictionary<Ruleset, RecordTuple>();
                    result.BestRandomSeedTimes = new Dictionary<Ruleset, RecordTuple>();
                    // intentionally do not wipe settings, they should upgrade gracefully
                }
                return result;
            }
        }

        public RandoSettings Settings;
        public const int MAX_SEED_CHARS = 20;

        public RandoModule() {
            Instance = this;
        }

        public override void Load() {
            Settings = this.SavedData.SavedSettings?.Copy() ?? new RandoSettings();
            LoadQol();
            LoadMechanics();
            LoadSessionLifecycle();
            LoadMenuLifecycle();
            Entities.LifeBerry.Load();
        }

        public Action ResetExtendedVariants;
        // Abusing this method as a delayed load thing
        public override void LoadContent(bool firstLoad) {
            var listof = AppDomain.CurrentDomain.GetAssemblies().Where(asm => asm.FullName.Contains("ExtendedVariant"));
            if (listof.Count() != 0) {
                var dll = listof.First();
                var ty = dll.GetType("ExtendedVariants.Module.ExtendedVariantsModule");
                var module = ty.GetField("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                var method = ty.GetMethod("ResetToDefaultSettings", BindingFlags.Public | BindingFlags.Instance);
                ResetExtendedVariants = () => {
                    method.Invoke(module, new object[0]);
                };
            } else {
                ResetExtendedVariants = () => {};
            }
        }


        public override void Unload() {
            UnloadQol();
            UnloadMechanics();
            UnloadSessionLifecycle();
            UnloadMenuLifecycle();
            Entities.LifeBerry.Unload();
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            // uncomment this when we have actual options to control
            //base.CreateModMenuSection(menu, inGame, snapshot);
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