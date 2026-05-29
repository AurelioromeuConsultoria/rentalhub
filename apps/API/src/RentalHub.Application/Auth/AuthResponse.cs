namespace RentalHub.Application.Auth;

public sealed record AuthResponse(
    string Token,
    string RefreshToken,
    UsuarioAuthDto Usuario);

