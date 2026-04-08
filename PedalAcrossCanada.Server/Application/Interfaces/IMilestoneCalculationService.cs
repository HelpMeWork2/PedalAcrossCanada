namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IMilestoneCalculationService
{
    Task RecalculateMilestonesAsync(Guid eventId);
}
