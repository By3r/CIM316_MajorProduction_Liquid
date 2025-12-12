public enum LiquidGoalType
{
    None = 0,

    #region Calm Goals
    GoToPond,
    RelaxInPond,
    EmergeFromPond,
    #endregion

    #region PLayer focused Goals
    ChasePlayer,
    HoldPlayer,
    SwallowPlayer,
    #endregion

    #region Liquid to Liquid Goals
    Duplicate,
    AskForMerge,
    LookForMergePartner,
    MergeWithLiquid
    #endregion
}