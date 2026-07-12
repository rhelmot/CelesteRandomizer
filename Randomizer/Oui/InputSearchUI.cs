using Celeste;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Reflection;

namespace Celeste.Mod.Randomizer {

    public class InputSearchUI : Entity {
        private static readonly MethodInfo ouiModOptionsAddSearchBox = typeof(OuiModOptions).GetMethod("AddSearchBox", BindingFlags.Public | BindingFlags.Static);
        private static readonly FieldInfo overworldInputEaseField = typeof(Overworld).GetField("inputEase", BindingFlags.NonPublic | BindingFlags.Instance);

        public static InputSearchUI Instance { get; private set; }

        private static VirtualButton key => Input.QuickRestart;

        private bool showSearchUI;
        private float wiggleDelay;
        private readonly Wiggler wiggler = Wiggler.Create(0.4f, 4f);
        private float inputEase;
        private Overworld overworld;

        public InputSearchUI(Overworld overworld) {
            Tag = Tags.HUD | Tags.PauseUpdate;
            Depth = -10000;
            Add(wiggler);
            this.overworld = overworld;
        }

        public override void Update() {
            if (key.Pressed && wiggleDelay <= 0f) {
                wiggler.Start();
                wiggleDelay = 0.5f;
            }

            wiggleDelay -= Engine.DeltaTime;
            inputEase = Calc.Approach(inputEase, (showSearchUI ? 1 : 0), Engine.DeltaTime * 4f);
            base.Update();
        }

        public override void Render() {
            if (inputEase <= 0f) return;

            float overworldInputEase = overworld == null ? 0f : (float) overworldInputEaseField.GetValue(overworld);

            const float scale = 0.5f;
            float positionOffset = overworldInputEase > 0f ? 48f : 0f;
            string label = Dialog.Clean("MAPLIST_SEARCH");
            float buttonWidth = ButtonUI.Width(label, key);

            Vector2 position = new Vector2(1880f, 1024f - positionOffset);
            position.X += (40f + buttonWidth * scale + 32f) * (1f - Ease.CubeOut(inputEase));
            ButtonUI.Render(position, label, key, scale, 1f, wiggler.Value * 0.05f);
        }

        public static void RegisterMenuEvents(TextMenu menu, bool showSearchUI) {
            if (Instance == null) {
                if (ouiModOptionsAddSearchBox == null || overworldInputEaseField == null)
                    return;
                Instance = new InputSearchUI(null);
            }
            Instance.showSearchUI = showSearchUI;
            if (!showSearchUI || menu == null)
                return;
            Overworld overworld = Engine.Scene as Overworld;

            // make sure the button is part of the current scene (Level or Overworld)
            if (Instance.Scene != Engine.Scene) {
                Instance.overworld = overworld;
                Engine.Scene.Add(Instance);
            }
            Action startSearching = ouiModOptionsAddSearchBox.Invoke(null, new object[] { menu, overworld }) as Action;
            // Remove Celeste.TextMenuExt+SearchToolTip added in the previous line
            menu.Remove(menu.Items[menu.Items.Count - 1]);

            menu.OnClose += () => Instance.showSearchUI = false;
            menu.OnUpdate += () => {
                if (key.Pressed) startSearching.Invoke();
            };
        }
    }
}