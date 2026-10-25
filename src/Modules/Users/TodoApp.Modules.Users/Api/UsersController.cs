using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Modules.Users.Application.Abstractions;
using TodoApp.Modules.Users.Application.GetUser;
using TodoApp.Modules.Users.Application.Login;
using TodoApp.Modules.Users.Application.RegisterUser;
using TodoApp.Modules.Users.Domain;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Infrastructure.Api;

namespace TodoApp.Modules.Users.Api;

// Request DTOs are the HTTP contract. They are separate from commands, so the API and the
// application layer can change independently. Attributes catch malformed input before any handler runs.
internal sealed record RegisterUserRequest(
    [Required, EmailAddress, MaxLength(User.EmailMaxLength)] string Email,
    [Required, MaxLength(User.DisplayNameMaxLength)] string DisplayName,
    [Required, MinLength(8), MaxLength(128)] string Password);

internal sealed record LoginRequest([Required] string Email, [Required] string Password);

internal sealed record RegisterUserResponse(Guid Id);

internal sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);

[Route("api/users")]
internal sealed class UsersController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<RegisterUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Register(
        RegisterUserRequest request,
        [FromServices] ICommandHandler<RegisterUserCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterUserCommand(request.Email, request.DisplayName, request.Password),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Me), new RegisterUserResponse(result.Value))
            : ToProblem(result.Error);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Login(
        LoginRequest request,
        [FromServices] ICommandHandler<LoginCommand, AccessToken> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);

        return result.IsSuccess
            ? Ok(new LoginResponse(result.Value.Token, result.Value.ExpiresAtUtc))
            : ToProblem(result.Error);
    }

    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Me(
        [FromServices] IQueryHandler<GetUserQuery, UserResponse> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(new GetUserQuery(CurrentUserId), cancellationToken));
}
