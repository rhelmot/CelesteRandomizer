using System;
using System.Collections;
using System.Runtime.InteropServices;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer.Entities
{
    [Mod.Entities.CustomEntity("randomizer/CustomLightBeam")]
    public class CustomLightBeam : Entity
    {
        public static ParticleType P_Glow;

        private MTexture texture = GFX.Game["util/lightbeam"];
        public Color color = new Color(0.8f, 1f, 1f);
        private float alpha;
        public int LightWidth;
        public int LightLength;
        public float Rotation;
        public string Flag;
        private float timer = Calc.Random.NextFloat(1000f);


        public CustomLightBeam(EntityData data, Vector2 offset) : base(data.Position + offset){
            base.Tag = Tags.TransitionUpdate;
            base.Depth = -9998;
            this.LightWidth = data.Width;
            this.LightLength = data.Height;
            this.Flag = data.Attr("flag", "");
            this.Rotation = data.Float("rotation", 0f) * ((float)Math.PI / 180f);

            this.color = Calc.HexToColor(data.Attr("color","CCFFFF"));
        }

        public override void Update()
        {
            this.timer += Engine.DeltaTime;
            Level level = base.Scene as Level;
            Player entity = base.Scene.Tracker.GetEntity<Player>();
            if (entity != null && (string.IsNullOrEmpty(this.Flag) || level.Session.GetFlag(this.Flag)))
            {
                Vector2 value = Calc.AngleToVector(this.Rotation + ((float)Math.PI / 2f), 1f);
                Vector2 value2 = Calc.ClosestPointOnLine(this.Position, this.Position + value * 10000f, entity.Center);
                float target = Math.Min(1f, Math.Max(0f, (value2 - this.Position).Length() - 8f) / (float)this.LightLength);
                if ((value2 - entity.Center).Length() > (float)this.LightWidth / 2f)
                {
                    target = 1f;
                }
                if (level.Transitioning)
                {
                    target = 0f;
                }
                this.alpha = Calc.Approach(this.alpha, target, Engine.DeltaTime * 4f);
            }
            if (this.alpha >= 0.5f && level.OnInterval(0.8f))
            {
                Vector2 vector = Calc.AngleToVector(this.Rotation + ((float)Math.PI / 2f), 1f);
                Vector2 vector2 = this.Position - vector * 4f;
                float scaleFactor = (float)(Calc.Random.Next(this.LightWidth - 4) + 2 - this.LightWidth / 2);
                vector2 += scaleFactor * vector.Perpendicular();
                level.Particles.Emit(LightBeam.P_Glow, vector2, this.Rotation + ((float)Math.PI / 2f));
            }
            base.Update();
        }

        public override void Render()
        {
            if (this.alpha > 0f)
            {
                this.DrawTexture(0f, (float) this.LightWidth, (float) (this.LightLength-4) + (float)Math.Sin((double)(this.timer * 2f)) * 4f, 0.4f);
                for (int i = 0; i < this.LightLength; i++)
                {
                    float num = this.timer + (float)i * 0.64f;
                    float num2 = 4f + (float)Math.Sin((double)(num * 0.5f + 1.2f)) * 4f;
                    float offset = (float)Math.Sin((double)((num + (float)(i * 32)) * 0.1f) + Math.Sin((double)(num * 0.05f + (float)i * 0.1f)) * 0.25) * ((float)this.LightWidth / 2f - num2 / 2f);
                    float length = (float)this.LightLength + (float)Math.Sin((double)(num * 0.25f)) * 8f;
                    float a = 0.6f + (float)Math.Sin((double)(num + 0.8f)) * 0.3f;
                    this.DrawTexture(offset, num2, length, a);
                }
            }
        }

        private void DrawTexture(float offset, float width, float length, float a)
        {
            float rotation = this.Rotation + ((float) Math.PI / 2);
            if (width >= 1f)
            {
                this.texture.Draw(this.Position + Calc.AngleToVector(this.Rotation, 1f) * offset, new Vector2(0f, 0.5f), this.color * a * this.alpha, new Vector2(1f / (float)this.texture.Width * length, width), rotation);
            }
        }
    }
}
