namespace SealHunter.Scheduler;

public enum BotState
{
    Idle,
    NextTarget,
    Teleporting,
    Navigating,
    Locating,
    Engaging,
    Recovering,
    PausedForDuty,
    Done,
    Error,
}
