using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer.Entities
{
    [Mod.Entities.CustomEntity("randomizer/TheoPhone")]
    public class FindTheoPhone : TheoPhone
    {
        private TalkComponent Talker;
        public EntityID ID;

        public FindTheoPhone(EntityData data, Vector2 offset, EntityID id) : base(offset + data.Position)
        {
            this.Add(this.Talker = new TalkComponent(new Rectangle(-12, -8, 24, 8), new Vector2(99999, 99999), this.OnTalk));
            this.ID = id;
        }

        private void OnTalk(Player obj)
        {
            this.Scene.Add(new CS_FindTheoPhone(this.Scene.Tracker.GetEntity<Player>(), this));
        }
    }

    public class CS_FindTheoPhone : CutsceneEntity
    {
        private Player Player;
        private float TargetX;
        private FindTheoPhone Phone;

        public CS_FindTheoPhone(Player player, FindTheoPhone phone)
        {
            this.Player = player;
            this.TargetX = phone.X + 8;
            this.Phone = phone;
        }

        public override void OnBegin(Level level) => this.Add(new Coroutine(this.Routine()));

        private bool SavedInvincible;
        private IEnumerator Routine()
        {
            this.Player.Speed = Vector2.Zero;
            this.SavedInvincible = SaveData.Instance.Assists.Invincible;
            SaveData.Instance.Assists.Invincible = true;
            this.Player.StateMachine.State = 11;
            this.Player.Facing = (Facings)Math.Sign(this.TargetX - this.Player.X);
            yield return 0.5f;
            var point = this.Level.Camera.CameraToScreen(this.Player.Position);
            point.X = Math.Min(Math.Max(point.X, this.Level.Camera.Viewport.Width / 4f), this.Level.Camera.Viewport.Width * 3f / 4f);
            point.Y = Math.Min(Math.Max(point.Y, this.Level.Camera.Viewport.Height / 4f), this.Level.Camera.Viewport.Height * 3f / 4f);
            yield return this.Level.ZoomTo(point, 2f, 0.5f);
            yield return Textbox.Say("RANDO_FOUNDTHEOPHONE", this.WalkToPhone, this.StandBackUp);
            yield return this.Level.ZoomBack(0.5f);
            this.EndCutscene(this.Level);
        }

        public override void OnEnd(Level level)
        {
            var reseter = new Entity {
                new Coroutine(this.ResetInvincible()),
            };
            reseter.Tag |= Tags.Global;
            Engine.Scene.Add(reseter);
            this.Player.StateMachine.State = 0;
            this.Level.Session.DoNotLoad.Add(this.Phone.ID);
            this.Phone.RemoveSelf();
        }

        private IEnumerator ResetInvincible()
        {
            yield return 1f;
            SaveData.Instance.Assists.Invincible = this.SavedInvincible;
        }

        private IEnumerator WalkToPhone()
        {
            yield return 0.25f;
            yield return this.Player.DummyWalkToExact((int)this.TargetX);
            this.Player.Facing = Facings.Left;
            yield return 0.5f;
            this.Player.DummyAutoAnimate = false;
            this.Player.Sprite.Play("duck");
            yield return 0.5f;
        }

        private IEnumerator StandBackUp()
        {
            this.Phone.RemoveSelf();
            yield return 0.6f;
            this.Player.Sprite.Play("idle");
            yield return 0.2f;
        }
    }
}
