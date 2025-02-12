using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.Randomizer.Entities
{
    [Mod.Entities.CustomEntity("randomizer/ActivateSwitches")]
    public class ActivateSwitches : Trigger
    {
        private string type;
        private int entID;
        public ActivateSwitches(EntityData data, Vector2 offset) : base(data, offset)
        {
            type = data.Values["type"].ToString();
            if (type == "button") entID = data.Int("entID");
        }

        public override void OnEnter(Player player)
        {
            switch (type)
            {
                case "touchswitch":
                    var switches = (Engine.Scene as Level).Entities.OfType<TouchSwitch>();
                    if (!switches.Any())
                    {
                        Logger.Log("Randomizer", $"No Touch Switches found in {(Engine.Scene as Level).Session.LevelData.Name}!");
                        return;
                    }

                    foreach (var s in switches)
                    {
                        s.TurnOn();
                    }
                    RemoveSelf();
                    break;

                case "button":
                    var buttons = (Engine.Scene as Level).Entities.OfType<DashSwitch>().Where(btn =>
                    {
                        DynamicData btnData = DynamicData.For(btn);
                        return btnData.Get<EntityID>("id").ID == entID;
                    });
                    if (!buttons.Any())
                    {
                        Logger.Log("Randomizer", $"No Button found with id {entID} in {(Engine.Scene as Level).Session.LevelData.Name}!");
                        return;
                    }
                    DynamicData buttonData = DynamicData.For(buttons.First());
                    buttonData.Invoke("OnDashed", player, buttonData.Get("pressDirection"));
                    RemoveSelf();
                    break;

                default:
                    Logger.Log("Randomizer", $"No type specified for ActivateSwitch trigger in {(Engine.Scene as Level).Session.LevelData.Name}");
                    break;
            }
            

        }
    }
}