using System.Collections;
using System.Reflection;
using Monocle;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Randomizer
{
    public abstract class GenericOui : Oui
    {
        private const float OnScreenX = 960f;
        private const float OffScreenXGeneric = 2880f;

        protected Entity Menu;

        public RandoSettings Settings => RandoModule.Instance.Settings;

        public override IEnumerator Enter(Oui from)
        {
            this.Visible = true;
            this.Menu = this.ReloadMenu();
            this.Scene.Add(this.Menu);

            var fromRight = this.IsDeeperThan(from);
            var OffScreenX = fromRight ? OffScreenXGeneric : -OffScreenXGeneric;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f)
            {
                this.Menu.X = OffScreenX + (OnScreenX - OffScreenX) * Ease.CubeOut(p);
                yield return null;
            }
            this.Menu.X = OnScreenX;
            this.Menu.Active = true;
        }

        public override IEnumerator Leave(Oui next)
        {
            this.Menu.Active = false;

            var toRight = this.IsDeeperThan(next);
            var OffScreenX = toRight ? OffScreenXGeneric : -OffScreenXGeneric;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f)
            {
                this.Menu.X = OnScreenX + (OffScreenX - OnScreenX) * Ease.CubeIn(p);
                yield return null;
            }

            this.Menu.RemoveSelf();
            this.Menu = null;
            this.Visible = false;
        }

        protected abstract Entity ReloadMenu();
        protected abstract bool IsDeeperThan(Oui other);
    }
}
