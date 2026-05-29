namespace RentalHub.Application.Security;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool Verify(string password, string passwordHash);
}

