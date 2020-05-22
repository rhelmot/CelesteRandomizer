using System;
namespace Celeste.Mod.Randomizer {
    public enum LogicType {
        Pathway,
        Labyrinth,
        Last
    }

    public enum MapLength {
        Short,
        Medium,
        Long,
        Enormous,
        Last
    }

    public class RandoSettings {
        public int Seed;
        public bool RepeatRooms;
        public bool EnterUnknown;
        public LogicType Algorithm;
        public MapLength Length;
    }
}
