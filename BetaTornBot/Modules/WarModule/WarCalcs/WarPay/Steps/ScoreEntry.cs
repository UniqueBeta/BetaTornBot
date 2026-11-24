using BetaTornBot.Modules.WarModule.WarCalcs.WarPay;

namespace WarPay;

/// <summary>Whether a member got a score entry for landing the hit or for assisting it.</summary>
internal enum EntryRole { Hitter, Assister }

/// <summary>
/// One line on the score sheet: one member's cut of one hit. The whole calculation is a list of these,
/// so to see why someone got their score, look at their entries.
/// </summary>
/// <param name="PlayerId">Who the entry is for.</param>
/// <param name="Name">That member's name.</param>
/// <param name="Role">Landed the hit, or assisted it.</param>
/// <param name="Kind">What kind of hit it was.</param>
/// <param name="HitValue">What the whole hit was worth, before splitting.</param>
/// <param name="Amount">This member's cut of the hit.</param>
/// <param name="IsShared">True if the hit was split with at least one assister.</param>
internal sealed record ScoreEntry(
    long PlayerId,
    string Name,
    EntryRole Role,
    HitKind Kind,
    double HitValue,
    double Amount,
    bool IsShared);
