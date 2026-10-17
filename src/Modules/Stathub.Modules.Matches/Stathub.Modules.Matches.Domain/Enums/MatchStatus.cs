namespace Stathub.Modules.Matches.Domain.Enums;

public enum MatchStatus
{
    Scheduled = 0, // Запланирован
    InProgress = 1, // В процессе
    Pause = 2, // Перерыв
    Finished = 3, // Завершён
    Cancelled = 4 // Отменён
}