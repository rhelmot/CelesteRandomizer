using System;
namespace Celeste.Mod.Randomizer {
    public enum LogicType {
        Pathway,
        Labyrinth,
        LastLogic
    }

    public class RandoSettings {
        public int Seed;
        public bool RepeatRooms;
        public bool EnterUnknown;
        public LogicType Algorithm;
    }
}
