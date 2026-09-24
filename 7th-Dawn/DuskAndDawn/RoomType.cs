using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public enum RoomType
    {
        Supplies,
        Encounter,
        Special,
        Empty,     // quiet hall - flavor, maybe a stray scrap
        Entrance,  // where the night starts; nothing to resolve
        Hoard      // the farthest room from the entrance - the night's big prize
    }

    public static class RoomTypeInfo
    {
        public static string Name(RoomType type) => type switch
        {
            RoomType.Supplies => "Supply Cache",
            RoomType.Encounter => "Something Waits",
            RoomType.Special => "Strange Room",
            RoomType.Empty => "Quiet Hall",
            RoomType.Entrance => "Entrance",
            RoomType.Hoard => "The Hoard",
            _ => type.ToString()
        };

        public static string Description(RoomType type) => type switch
        {
            RoomType.Supplies => "Food, planks and scraps - if you take the time.",
            RoomType.Encounter => "A corrupted thing. Fighting costs time.",
            RoomType.Special => "Could be a quiet find, could be a weapon.",
            RoomType.Empty => "Nothing stirs. Probably.",
            RoomType.Entrance => "The way you came in.",
            RoomType.Hoard => "The deepest room. Someone hid a lot here.",
            _ => ""
        };
    }
}
