namespace UsersMs.Events;

public class UserCreatedIntegrationEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
