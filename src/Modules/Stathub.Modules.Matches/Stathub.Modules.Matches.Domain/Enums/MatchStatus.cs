namespace Stathub.Modules.Matches.Domain.Enums;

public enum MatchStatus
{
    Scheduled = 0, // Запланирован
    InProgress = 1, // В процессе
    Finished = 2, // Завершён
    Cancelled = 3 // Отменён
}