using FluentValidation;
using MediatR;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;

namespace RequestDesk.Application.Auth;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResult>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(256);
    }
}

internal sealed class LoginCommandHandler(IAuthenticator authenticator) : IRequestHandler<LoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken cancellationToken) =>
        await authenticator.LoginAsync(command.Email, command.Password, cancellationToken)
            ?? throw new AuthenticationFailedException();
}

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResult>;

internal sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

internal sealed class RefreshTokenCommandHandler(IAuthenticator authenticator) : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshTokenCommand command, CancellationToken cancellationToken) =>
        await authenticator.RefreshAsync(command.RefreshToken, cancellationToken)
            ?? throw new AuthenticationFailedException();
}
