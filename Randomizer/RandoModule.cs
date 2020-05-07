using Celeste.Mod;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using On.Celeste;
using Mono.Cecil.Cil;

namespace Celeste.Mod.Randomizer {
    public class RandoModule : EverestModule {
        public static RandoModule Instance;

        public RandoModule() {
            Instance = this;
        }

        public override void Load() {
            Everest.Events.MainMenu.OnCreateButtons += CreateMainMenuButton;
        }

        public override void LoadContent(bool firstLoad) {
        }


        public override void Unload() {
            Everest.Events.MainMenu.OnCreateButtons -= CreateMainMenuButton;
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            base.CreateModMenuSection(menu, inGame, snapshot);
        }

        public void CreateMainMenuButton(OuiMainMenu menu, List<MenuButton> buttons) {
            MainMenuSmallButton btn = new MainMenuSmallButton("MODOPTIONS_RANDOMIZER_TOPMENU", "menu/randomizer", menu, Vector2.Zero, Vector2.Zero, () => {
                Audio.Play(SFX.ui_main_button_select);
                Audio.Play(SFX.ui_main_whoosh_large_in);
                menu.Overworld.Goto<OuiRandoSettings>();
            });
            buttons.Insert(1, btn);
        }
    }
}
