using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer.Entities
{
    [Mod.Entities.CustomEntity("randomizer/ActivateSwitches")]
    public class ActivateSwitches : Trigger
    {
        public ActivateSwitches(EntityData data, Vector2 offset) : base(data, offset){}

        public override void OnEnter(Player player)
        {
            var switches = (Engine.Scene as Level).Entities.OfType<TouchSwitch>();
            if (!switches.Any())
            {
                Logger.Log("Randomizer", $"No Switches found in {(Engine.Scene as Level).Session.LevelData.Name}!");
                return;
            }

            foreach (var s in switches)
            {
                s.TurnOn();
            }

        }
    }
}