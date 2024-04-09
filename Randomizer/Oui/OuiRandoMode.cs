using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer
{
    public class OuiRandoMode : GenericOui
    {
        protected override Entity ReloadMenu()
        {
            var menu = new WidgetRandoMode()
            {
                VerticalSpacing = 200f,
                IconScale = 0.5f,
                Y = 200f,
                Index = (int)this.Settings.Algorithm,
            };
            menu.OnCancel = () =>
            {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            };
            menu.OnSelect = () =>
            {
                var thing = (LogicType)menu.Index;
                if (thing != this.Settings.Algorithm)
                {
                    this.Settings.Rules = "";
                }
                this.Settings.Algorithm = thing;
                Audio.Play("event:/ui/main/button_climb");
                Overworld.Goto<OuiRandoDifficulty>();
            };
            menu.OnPause += () =>
            {
                Audio.Play(SFX.ui_main_button_select);
                Overworld.Goto<OuiMainMenu>();
            };
            return menu;
        }

        protected override bool IsDeeperThan(Oui other)
        {
            // top level rando oui. every other GenericOui is below us.
            return !(other is GenericOui);
        }
    }
    public class WidgetRandoMode : Entity
    {
        public float VerticalSpacing, IconScale;
        public float Indent = 100f;
        public Vector2 MenuOffset = new Vector2(-400f, 200f);
        public int Index;
        public Action OnSelect, OnCancel, OnPause;

        private MTexture[] Icons;
        private string[] Names;
        private float[] Ease = { 0f, 0f, 0f };

        public WidgetRandoMode()
        {
            this.Tag = Tags.HUD;
            var modes = new[] { "pathway", "labyrinth", "endless" };
            this.Icons = modes.Select(s => GFX.Gui[$"menu/{s}_icon"]).ToArray();
            this.Names = modes.Select(s => Dialog.Clean($"modoptions_randomizer_logic_{s}")).ToArray();
        }

        public override void Update()
        {
            base.Update();

            for (var i = 0; i < this.Ease.Length; i++)
            {
                var target = i == this.Index ? 1f : 0f;
                this.Ease[i] = Calc.Approach(this.Ease[i], target, Engine.DeltaTime * 15);
            }

            if (Input.MenuConfirm.Pressed && this.OnSelect != null)
            {
                this.OnSelect();
                return;
            }
            else if (Input.MenuCancel.Pressed && this.OnCancel != null)
            {
                this.OnCancel();
            }
            else if (Input.Pause.Pressed && this.OnPause != null)
            {
                this.OnPause();
            }
            else if (Input.MenuUp.Pressed && !Input.MenuUp.Repeating && this.Index != 0)
            {
                Audio.Play("event:/ui/main/savefile_rollover_up");
                this.Index--;
            }
            else if (Input.MenuDown.Pressed && !Input.MenuDown.Repeating && this.Index != this.Names.Length - 1)
            {
                Audio.Play("event:/ui/main/savefile_rollover_down");
                this.Index++;
            }
        }

        public override void Render()
        {
            base.Render();

            ActiveFont.DrawEdgeOutline(Dialog.Get("MODOPTIONS_RANDOMIZER_LOGIC"), this.Position, new Vector2(0.5f, 0.5f), Vector2.One * 2f, Color.Gray, 4f, Color.DarkSlateBlue, 2f, Color.Black);

            for (var i = 0; i < this.Icons.Length; i++)
            {
                var x = this.X + this.MenuOffset.X + this.Ease[i] * this.Indent;
                var y = this.Y + this.MenuOffset.Y + i * this.VerticalSpacing;
                var color = Color.White;
                if (i == this.Index)
                {
                    color = !Settings.Instance.DisableFlashes && !this.Scene.BetweenInterval(0.1f) ? TextMenu.HighlightColorB : TextMenu.HighlightColorA;
                }
                this.Icons[i].DrawCentered(new Vector2(x, y), Color.White, new Vector2(this.IconScale, this.IconScale));
                ActiveFont.DrawOutline(this.Names[i], new Vector2(x + this.Icons[i].Width * this.IconScale, y), new Vector2(0f, 0.5f), Vector2.One * 1.5f, color, 2f, Color.Black);
            }
        }
    }
}
