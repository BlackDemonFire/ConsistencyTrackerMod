using System;

namespace Celeste.Mod.ConsistencyTracker.Enums;

[Serializable]
public enum RoomNameDisplayType {
    //Types: 1 ->  | 2 ->
    AbbreviationAndRoomNumberInCP = 1, //EH-3
    FullNameAndRoomNumberInCP = 2, //Event-Horizon-3
    DebugRoomName = 3, //f07
}