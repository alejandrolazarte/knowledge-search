namespace NotificationsMs.Handlers;

public class UserCreatedIntegrationEventHandler : IIntegrationEventHandler<UserCreatedIntegrationEvent>
{
    private readonly IEmailService _emailService;

    public UserCreatedIntegrationEventHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(UserCreatedIntegrationEvent integrationEvent)
    {
        await _emailService.SendWelcomeEmailAsync(integrationEvent.Email);
    }
}
