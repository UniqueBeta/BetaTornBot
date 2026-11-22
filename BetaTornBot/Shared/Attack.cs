namespace BetaTornBot.Shared;

/// <summary>One attack from the faction attack log, reduced to what the calculation needs.</summary>
internal sealed record Attack(
    long Id,
    long Started,
    long Ended,
    long? AttackerId,
    string AttackerName,
    long? AttackerFactionId,
    long? DefenderId,
    string Result,
    double RespectGain,
    int Chain,
    bool IsRankedWar,
    string DefenderName = "");
