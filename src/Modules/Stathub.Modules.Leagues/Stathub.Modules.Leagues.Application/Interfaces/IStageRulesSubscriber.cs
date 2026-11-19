namespace Stathub.Modules.Leagues.Application.Interfaces;

/// <summary>
/// Получатель изменённых правил этапа. Объявлен здесь, реализуется модулями-потребителями
/// (матчи копируют регламент этапа при создании): Leagues не знает про них, но после сохранения
/// этапа сообщает, что правила могли измениться, — новые правила потребитель читает сам
/// через <see cref="IStageReader"/>.
/// </summary>
public interface IStageRulesSubscriber
{
    /// <summary>Правила этапа сохранены; применить их к тому, что ещё не начато.</summary>
    Task ApplyStageRulesAsync(Guid stageId, CancellationToken cancellationToken = default);
}
