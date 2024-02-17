using System;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer.Entities
{
    public class BerrySet
    {
        public int Carrying;
        public HashSet<EntityID> TakenIDs = new HashSet<EntityID>();
    }

    [Mod.Entities.CustomEntity("randomizer/LifeBerry")]
    public class LifeBerry : Entity
    {
        // persistence bits copied from https://github.com/EverestAPI/SpringCollab2020/blob/master/Entities/MultiRoomStrawberrySeed.cs
        public static void Load()
        {
            On.Celeste.Level.LoadLevel += onLoadLevel;
        }

        public static void Unload()
        {
            On.Celeste.Level.LoadLevel -= onLoadLevel;
        }

        public static BerrySet GrabbedLifeBerries
        {
            get
            {
                var dyn = new DynData<Session>(SaveData.Instance.CurrentSession);
                var val = dyn.Get<BerrySet>("GrabbedLifeBerries");
                if (val == null)
                {
                    val = new BerrySet();
                    dyn.Set<BerrySet>("GrabbedLifeBerries", val);
                }
                return val;
            }
        }

        private static void onLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
        {
            orig(self, playerIntro, isFromLoader);

            if (playerIntro != Player.IntroTypes.Transition)
            {
                Player player = self.Tracker.GetEntity<Player>();

                if (player != null)
                {
                    Vector2 position = player.Position;

                    var grabbed = GrabbedLifeBerries;
                    for (int i = 0; i < grabbed.Carrying; i++)
                    {
                        position += new Vector2(-12 * (int)player.Facing, -8f);
                        self.Add(new LifeBerry(player, position, new EntityID("CARRYING", i)));
                    }
                }
            }
        }

        private bool AutoBubble;
        private Sprite Sprite;
        private Wiggler Wiggler;
        private VertexLight Light;
        private BloomPoint Bloom;
        private Tween LightTween;
        private float Wobble;

        public Follower Follower;
        public EntityID ID;
        public bool IsFirstStrawberry;

        public LifeBerry(Vector2 position, EntityID id, bool AutoBubble) : base(position)
        {
            this.AutoBubble = AutoBubble;
            this.ID = id;

            this.Collider = new Hitbox(14f, 14f, -7f, -7f);
            this.Add(new PlayerCollider(this.OnPlayer));
            this.Add(new MirrorReflection());
            this.Add(this.Follower = new Follower(this.ID, onLoseLeader: this.OnLoseLeader));
        }

        public LifeBerry(Player player, Vector2 position, EntityID id) : this(position, id, false)
        {
            player.Leader.GainFollower(this.Follower);
            this.IsFirstStrawberry = this.CheckFirstStrawberry;
            this.Collidable = false;
            this.Depth = -1000000;
        }

        public LifeBerry(EntityData data, Vector2 offset, EntityID id) : this(data.Position + offset, id, data.Bool("AutoBubble")) { }

        public override void Added(Scene scene)
        {
            base.Added(scene);

            var berrySet = GrabbedLifeBerries;
            if (!this.Follower.HasLeader && berrySet.TakenIDs.Contains(this.ID))
            {
                this.RemoveSelf();
                return;
            }

            this.Add(this.Sprite = GFX.SpriteBank.Create("strawberry"));
            this.Sprite.OnFrameChange = this.OnAnimate;
            this.Add(this.Wiggler = Wiggler.Create(0.4f, 4f, v => this.Sprite.Scale = Vector2.One * (float)(1.0 + (double)v * 0.3499999940395355)));
            this.Add(Wiggler.Create(0.5f, 4f, v => this.Sprite.Rotation = (float)((double)v * 30.0 * (Math.PI / 180.0))));
            this.Add(this.Bloom = new BloomPoint(1f, 12f));
            this.Add(this.Light = new VertexLight(Color.White, 1f, 16, 24));
            this.Add(this.LightTween = this.Light.CreatePulseTween());
        }

        public void OnPlayer(Player player)
        {
            if (this.Follower.HasLeader)
            {
                return;
            }

            Audio.Play("event:/game/general/strawberry_touch", this.Position);
            player.Leader.GainFollower(this.Follower);
            this.IsFirstStrawberry = this.CheckFirstStrawberry;
            this.Wiggler.Start();
            this.Depth = -1000000;

            var berrySet = GrabbedLifeBerries;
            berrySet.Carrying++;
            berrySet.TakenIDs.Add(this.ID);

            if (this.AutoBubble)
            {
                player.Add(new Coroutine(RandoModule.AutoBubbleCoroutine(player)));
            }
        }

        public override void Update()
        {
            base.Update();
            if (!this.Follower.HasLeader)
            {
                this.Wobble += Engine.DeltaTime * 4f;
                this.Sprite.Y = this.Bloom.Y = this.Light.Y = (float)Math.Sin(this.Wobble) * 2f;
            }
        }

        private bool CheckFirstStrawberry
        {
            get
            {
                for (int index = this.Follower.FollowIndex - 1; index >= 0; --index)
                {
                    if (this.Follower.Leader.Followers[index].Entity is LifeBerry)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private void OnLoseLeader()
        {
            if (this.IsFirstStrawberry)
            {
                var track = GrabbedLifeBerries;
                new DynData<Session>(SaveData.Instance.CurrentSession).Set<bool?>("SavedByTheBell", true);
                GrabbedLifeBerries.Carrying--;

                Audio.Play("event:/game/general/strawberry_get", this.Position, "colour", 0, "count", 0);
                this.Sprite.Play("collect");
                Alarm.Set(this, 0.3f, () =>
                {
                    this.Scene.Add(new StrawberryPoints(this.Position, false, 6, false));
                    this.RemoveSelf();
                });
            }
        }

        private void OnAnimate(string id)
        {
            if (this.Sprite.CurrentAnimationFrame != 35)
            {
                return;
            }
            this.LightTween.Start();
            if (!this.Follower.HasLeader && (this.CollideCheck<FakeWall>() || this.CollideCheck<Solid>()))
            {
                Audio.Play("event:/game/general/strawberry_pulse", this.Position);
                this.SceneAs<Level>().Displacement.AddBurst(this.Position, 0.6f, 4f, 28f, 0.1f);
            }
            else
            {
                Audio.Play("event:/game/general/strawberry_pulse", this.Position);
                this.SceneAs<Level>().Displacement.AddBurst(this.Position, 0.6f, 4f, 28f, 0.2f);
            }
        }
    }
}
