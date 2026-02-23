namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Defines the architectural category of a room prefab.
    /// Used for weighted selection and floor layout control.
    /// </summary>
    public enum RoomCategory
    {
        /// <summary>
        /// Simple connector room with 2 doors (straight or L-shaped).
        /// Most common room type for basic pathways.
        /// </summary>
        Corridor = 0,

        /// <summary>
        /// T-junction or four-way crossing with 3-4 doors.
        /// Creates branching paths in the layout.
        /// </summary>
        Intersection = 1,

        /// <summary>
        /// Large significant room with multiple doors (4+ sockets).
        /// Acts as central gathering point for floor layout.
        /// Good location for objectives, loot, or encounters.
        /// </summary>
        Hub = 2,

        /// <summary>
        /// Room with unique static gameplay elements or distinct visuals.
        /// Examples: Arena, treasure room, puzzle room, safe room.
        /// </summary>
        Feature = 3,

        /// <summary>
        /// Small room with single door, designed to cap off a path.
        /// Used for dead-end handling instead of sealing doors.
        /// Examples: Storage closet, collapsed tunnel, loot cache.
        /// </summary>
        Terminus = 4,

        /// <summary>
        /// Special room containing elevator for player entry.
        /// Only used at floor start (except Floor 1).
        /// Excluded from random generation pool.
        /// </summary>
        Entry_Elevator = 100,

        /// <summary>
        /// Special room containing elevator for floor exit.
        /// Placed at furthest point from entry during finalization.
        /// Excluded from random generation pool.
        /// </summary>
        Exit_Elevator = 101,

        /// <summary>
        /// Special sanctuary room for saving, shopping, and healing.
        /// On Floor 1: Used as entry point.
        /// On other floors: Optional hidden discovery.
        /// Excluded from random generation pool.
        /// </summary>
        Safe_Room = 102
    }
}