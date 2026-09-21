namespace Stathub.Modules.Leagues.Domain.Enums;

public enum TournamentStatus
{
    Scheduled, // Турнир запланирован, но еще не начался
    Active, // Турнир в процессе проведения
    Completed,  // Турнир завершен
    Cancelled // Турнир отменен
}
