namespace Liquid.NPC
{
    public enum OccupationType
    {
        None = 0,

        // Space / ship life
        Pilot = 1,
        Navigator = 2,
        Engineer = 3,
        Mechanic = 4,
        LifeSupportTech = 5,

        // Science / exploration
        Scientist = 6,
        Biologist = 7,
        Geologist = 8,
        Xenologist = 9,

        // Operations / logistics
        Quartermaster = 10,
        CargoHandler = 11,
        Salvager = 12,
        Trader = 13,

        // Security / authority
        SecurityOfficer = 14,
        BountyHunter = 15,
        Captain = 16,

        // Medical / welfare
        Medic = 17,
        Therapist = 18,

        // Social / culture
        Diplomat = 19,
        Entertainer = 20,

        // “Civilian-ish” space roles
        Miner = 21,
        Botanist = 22
    }
}