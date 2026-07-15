using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer.Entities {
    [Mod.Entities.CustomEntity("randomizer/DestroyDashBlock")]
    public class DestroyDashBlock : Trigger {

        //private string type;
        private int entID;
        public DestroyDashBlock(EntityData data, Vector2 offset) : base(data, offset) {

            //type = data.Values["type"].ToString();
            entID = data.Int("entID");
        }

        public override void OnEnter(Player player) {
            DashBlock block = base.Scene.Tracker.GetEntities<DashBlock>().FirstOrDefault(b => DynamicData.For(b).Get<EntityID>("id").ID == entID) as DashBlock;
            if (!block.Any()) {
                Logger.Log("Randomizer", $"No dashBlock:{entID} found in {(Engine.Scene as Level).Session.LevelData.Name}!");
            } else {
                block.Break(Vector2.Zero, Vector2.Zero, true, true);
            }
            RemoveSelf();
        }
    }
}