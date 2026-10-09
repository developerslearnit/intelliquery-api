using FluentAssertions;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Application.Features.Identity.Commands.RegisterUser;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Moq;

namespace IntelliQueryAI.Application.Tests;

public class RegisterUserCommandHandlerTests
{
    private readonly Mock<IUserStore> _userStoreMock = new();
    private readonly Mock<IWorkspaceStore> _workspaceStoreMock = new();
    private readonly Mock<ISubscriptionStore> _subscriptionStoreMock = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokenStoreMock = new();
    private readonly Mock<IAuditLogStore> _auditLogStoreMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGeneratorMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _passwordHasherMock
            .Setup(p => p.Hash(It.IsAny<string>()))
            .Returns("hashed_password_sample");

        _tokenGeneratorMock
            .Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Workspace>(), It.IsAny<string>()))
            .Returns("sample_jwt_access_token");

        _tokenGeneratorMock
            .Setup(t => t.GenerateRefreshToken())
            .Returns("sample_refresh_token_string");

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new RegisterUserCommandHandler(
            _userStoreMock.Object,
            _workspaceStoreMock.Object,
            _subscriptionStoreMock.Object,
            _refreshTokenStoreMock.Object,
            _auditLogStoreMock.Object,
            _passwordHasherMock.Object,
            _tokenGeneratorMock.Object,
            _emailSenderMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenValidInput_ShouldRegisterUserAndCreateWorkspace()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "admin@acme.com",
            "SuperPassword123!",
            "John",
            "Doe",
            "Acme Analytics",
            "acme");

        _userStoreMock
            .Setup(u => u.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _workspaceStoreMock
            .Setup(w => w.SlugExistsAsync(command.WorkspaceSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("sample_jwt_access_token");
        result.Value.RefreshToken.Should().Be("sample_refresh_token_string");
        result.Value.User.Email.Should().Be("admin@acme.com");
        result.Value.ActiveWorkspace.Should().NotBeNull();
        result.Value.ActiveWorkspace!.Slug.Should().Be("acme");

        _userStoreMock.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _workspaceStoreMock.Verify(w => w.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Once);
        _subscriptionStoreMock.Verify(s => s.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenStoreMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _auditLogStoreMock.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenEmailAlreadyInUse_ShouldReturnConflictFailure()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "existing@acme.com",
            "SuperPassword123!",
            "John",
            "Doe",
            "Acme Analytics",
            "acme");

        _userStoreMock
            .Setup(u => u.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(IdentityErrors.EmailAlreadyInUse.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenSlugAlreadyExists_ShouldReturnConflictFailure()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "admin@acme.com",
            "SuperPassword123!",
            "John",
            "Doe",
            "Acme Analytics",
            "acme");

        _userStoreMock
            .Setup(u => u.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _workspaceStoreMock
            .Setup(w => w.SlugExistsAsync(command.WorkspaceSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.SlugAlreadyExists.Code);
    }
}
