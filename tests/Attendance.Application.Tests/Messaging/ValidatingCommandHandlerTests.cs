using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity;
using Attendance.Application.Identity.Login;
using Attendance.Shared.Results;
using FluentAssertions;
using FluentValidation;

namespace Attendance.Application.Tests.Messaging;

public sealed class ValidatingCommandHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_invalid_command_never_reaches_the_handler()
    {
        var inner = new RecordingHandler();
        var pipeline = new ValidatingCommandHandler<LoginCommand, Result<AuthTokens>>(inner, [new LoginCommandValidator()]);

        var act = () => pipeline.HandleAsync(new LoginCommand("not-an-email", "", "10.0.0.1", null), Ct);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo([nameof(LoginCommand.Email), nameof(LoginCommand.Password)]);
        inner.Calls.Should().Be(0);
    }

    [Fact]
    public async Task A_valid_command_reaches_the_handler()
    {
        var inner = new RecordingHandler();
        var pipeline = new ValidatingCommandHandler<LoginCommand, Result<AuthTokens>>(inner, [new LoginCommandValidator()]);

        await pipeline.HandleAsync(new LoginCommand("rafiq.islam@nordwin.example", "secret", "10.0.0.1", null), Ct);

        inner.Calls.Should().Be(1);
    }

    [Fact]
    public void Every_command_has_a_validator()
    {
        var types = typeof(DependencyInjection).Assembly.GetTypes();
        var commands = types.Where(t => t.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));
        var validated = types
            .Where(t => !t.IsAbstract && t.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .Select(t => t.BaseType!.GenericTypeArguments[0]);

        commands.Should().NotBeEmpty().And.BeSubsetOf(validated, "FluentValidation for every command");
    }

    [Fact]
    public void Commands_and_tokens_keep_secrets_out_of_ToString()
    {
        new LoginCommand("rafiq.islam@nordwin.example", "p@ssw0rd", "10.0.0.1", null).ToString().Should().NotContain("p@ssw0rd");
        new AuthTokens("ACCESS", DateTime.UtcNow, "REFRESH", DateTime.UtcNow, false).ToString()
            .Should().NotContain("ACCESS").And.NotContain("REFRESH");
    }

    private sealed class RecordingHandler : ICommandHandler<LoginCommand, Result<AuthTokens>>
    {
        public int Calls { get; private set; }

        public Task<Result<AuthTokens>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Result<AuthTokens>>(AuthErrors.InvalidCredentials);
        }
    }
}
