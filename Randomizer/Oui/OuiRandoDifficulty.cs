using Monocle;

namespace Celeste.Mod.Randomizer
{
    public class OuiRandoDifficulty : GenericOui
    {
        protected override Entity ReloadMenu()
        {
            var menu = new TextMenu() {
                new TextMenu.Header(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY")),
            };
            var easy = new TextMenuExt.ButtonExt(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_EASY"), "menu/skulls/strawberry").Pressed(() => this.Next(Difficulty.Easy));
            var normal = new TextMenuExt.ButtonExt(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_NORMAL"), "menu/skulls/skullblue").Pressed(() => this.Next(Difficulty.Normal));
            var hard = new TextMenuExt.ButtonExt(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_HARD"), "menu/skulls/skullred").Pressed(() => this.Next(Difficulty.Hard));
            var expert = new TextMenuExt.ButtonExt(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_EXPERT"), "menu/skulls/skullgold").Pressed(() => this.Next(Difficulty.Expert));
            var master = new TextMenuExt.ButtonExt(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_MASTER"), "menu/skulls/skullorange").Pressed(() => this.Next(Difficulty.Master));
            var perfect = new TextMenuExt.ButtonExt(Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_PERFECT"), "menu/skulls/skullpurple").Pressed(() => this.Next(Difficulty.Perfect));
            var explain = new SpecialSizeSubheader("Filler");

            easy.OnEnter += () => explain.Title = Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_EXPLAIN_EASY");
            normal.OnEnter += () => explain.Title = Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_EXPLAIN_NORMAL");
            hard.OnEnter += () => explain.Title = Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_EXPLAIN_HARD");
            expert.OnEnter += () => explain.Title = Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_EXPLAIN_EXPERT");
            master.OnEnter += () => explain.Title = Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_EXPLAIN_MASTER");
            perfect.OnEnter += () => explain.Title = Dialog.Clean("MODOPTIONS_RANDOMIZER_DIFFICULTY_EXPLAIN_PERFECT");

            menu.Add(easy);
            menu.Add(normal);
            menu.Add(hard);
            menu.Add(expert);
            menu.Add(master);
            menu.Add(perfect);
            menu.Add(explain);
            menu.Selection = (int)this.Settings.Difficulty + 1;
            menu.Current.OnEnter();

            menu.OnCancel += () =>
            {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiRandoMode>();
            };

            menu.OnPause += () =>
            {
                Audio.Play(SFX.ui_main_button_select);
                Overworld.Goto<OuiMainMenu>();
            };

            return menu;
        }

        private void Next(Difficulty thing)
        {
            if (thing != this.Settings.Difficulty)
            {
                this.Settings.Rules = "";
            }
            this.Settings.Difficulty = thing;

            Audio.Play("event:/ui/main/button_climb");
            Overworld.Goto<OuiRandoSettings>();
        }

        protected override bool IsDeeperThan(Oui other)
        {
            if (other is OuiRandoMode)
            {
                return true;
            }
            else if (other is GenericOui)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    public class SpecialSizeSubheader : TextMenu.SubHeader
    {
        public SpecialSizeSubheader(string title) : base(title)
        {
        }

        public override float LeftWidth() => 0;
        public override float RightWidth() => 0;
    }
}
