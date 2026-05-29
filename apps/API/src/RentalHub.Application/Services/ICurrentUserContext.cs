namespace RentalHub.Application.Services;

public interface ICurrentUserContext
{
    int? UserId { get; }
    string? UserName { get; }
    string? UserEmail { get; }
    bool IsPlatformAdmin { get; }
}

