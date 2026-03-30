namespace PedalAcrossCanada.Shared.DTOs.Activities;

public class CreateActivityResponse
{
    public ActivityDto Activity { get; set; } = null!;
    public bool DuplicateWarning { get; set; }
    public Guid? CandidateActivityId { get; set; }
}
