using FMOD.Studio;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    public partial class RandoModule : EverestModule {
        public static RandoModule Instance;
        public override Type SettingsType => typeof(RandoModuleSettings);
        public RandoModuleSettings SavedData {
            get {
                var result = Instance._Settings as RandoModuleSettings;
                if (result.CurrentVersion != this.Metadata.VersionString) {
                    result.CurrentVersion = this.Metadata.VersionString;
                    result.BestTimes = new Dictionary<uint, long>();
                    result.BestSetSeedTimes = new Dictionary<Ruleset, RecordTuple>();
                    result.BestRandomSeedTimes = new Dictionary<Ruleset, RecordTuple>();
                }
                return result;
            }
        }

        public RandoSettings Settings;
        public const int MAX_SEED_CHARS = 20;

        public RandoModule() {
            Instance = this;
            Settings = new RandoSettings();
        }

        public override void Load() {
            LoadQol();
            LoadMechanics();
            LoadSessionLifecycle();
            LoadMenuLifecycle();
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
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            // uncomment this when we have actual options to control
            //base.CreateModMenuSection(menu, inGame, snapshot);
        }

        public bool InRandomizer {
            get {
                if (SaveData.Instance == null) {
                    return false;
                }
                if (SaveData.Instance.CurrentSession == null) {
                    return false;
                }
                if (SaveData.Instance.CurrentSession.Area == null) {
                    return false;
                }
                try {
                    AreaData area = AreaData.Get(SaveData.Instance.CurrentSession.Area);
                    return area.GetSID().StartsWith("randomizer/");
                } catch (NullReferenceException) {
                    return false; // I have no idea why this happens but it happens sometimes
                }
            }
        }


    }
}