using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Moq;

namespace AAExamManagementSystem.Tests;

public class ChoiceServiceTests
{
    private static (ChoiceService Service, Mock<IUnitOfWork> UnitOfWork, Mock<IGenericRepository<Choice>> Choices) CreateService(
        ApplicationDbContext? db = null)
    {
        var choices = new Mock<IGenericRepository<Choice>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Choices).Returns(choices.Object);
        var validator = new ActiveCorrectChoiceValidator(db ?? TestSupport.CreateContext());
        return (new ChoiceService(unitOfWork.Object, CreateMapper(), validator), unitOfWork, choices);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(
            cfg =>
            {
                cfg.CreateMap<Choice, ChoiceDto>();
                cfg.CreateMap<ChoiceCreateUpdateDto, Choice>();
            },
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    [Fact]
    public async Task GetAll_ReturnsAllChoices()
    {
        var (service, _, choices) = CreateService();
        choices.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
        {
            new Choice { ChoiceText = "A" },
            new Choice { ChoiceText = "B" },
        });

        Assert.Equal(2, (await service.GetAllAsync()).Count());
    }

    [Fact]
    public async Task GetById_WithMissingId_ReturnsNull()
    {
        var (service, _, choices) = CreateService();
        choices.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Choice?)null);

        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Create_AddsChoiceAndSaves()
    {
        var (service, unitOfWork, choices) = CreateService();

        var result = await service.CreateAsync(new ChoiceCreateUpdateDto { ChoiceText = "Yes", IsCorrect = true });

        Assert.Equal("Yes", result.ChoiceText);
        choices.Verify(r => r.AddAsync(It.Is<Choice>(c => c.ChoiceText == "Yes")), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithExistingId_UpdatesChoice()
    {
        var (service, unitOfWork, choices) = CreateService();
        var choice = new Choice { ChoiceText = "Old" };
        choices.Setup(r => r.GetByIdAsync(choice.Id)).ReturnsAsync(choice);

        var result = await service.UpdateAsync(choice.Id, new ChoiceCreateUpdateDto { ChoiceText = "New", IsCorrect = true, IsActive = false });

        Assert.True(result.Succeeded);
        Assert.Equal("New", choice.ChoiceText);
        Assert.True(choice.IsCorrect);
        Assert.False(choice.IsActive);
        choices.Verify(r => r.Update(choice), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_MakingASecondActiveChoiceCorrect_ReturnsValidationFailed_AndDoesNotSave()
    {
        var db = TestSupport.CreateContext();
        var question = new Question { QuestionTitle = "Pick one" };
        var right = new Choice { ChoiceText = "Right", IsCorrect = true };
        var other = new Choice { ChoiceText = "Other" };
        db.Questions.Add(question);
        db.QuestionAndChoices.AddRange(
            new QuestionAndChoice { Question = question, Choice = right },
            new QuestionAndChoice { Question = question, Choice = other });
        db.SaveChanges();
        var (service, unitOfWork, choices) = CreateService(db);
        choices.Setup(r => r.GetByIdAsync(other.Id)).ReturnsAsync(other);

        var result = await service.UpdateAsync(other.Id, new ChoiceCreateUpdateDto { ChoiceText = "Other", IsCorrect = true, IsActive = true });

        Assert.Equal(ServiceStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey(nameof(ChoiceCreateUpdateDto.IsCorrect)));
        Assert.False(other.IsCorrect);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Update_MakingAChoiceCorrect_IsAllowedOnceTheOldCorrectOneIsInactive()
    {
        var db = TestSupport.CreateContext();
        var question = new Question { QuestionTitle = "Pick one" };
        var retired = new Choice { ChoiceText = "Retired", IsCorrect = true, IsActive = false };
        var other = new Choice { ChoiceText = "Other" };
        db.Questions.Add(question);
        db.QuestionAndChoices.AddRange(
            new QuestionAndChoice { Question = question, Choice = retired },
            new QuestionAndChoice { Question = question, Choice = other });
        db.SaveChanges();
        var (service, _, choices) = CreateService(db);
        choices.Setup(r => r.GetByIdAsync(other.Id)).ReturnsAsync(other);

        var result = await service.UpdateAsync(other.Id, new ChoiceCreateUpdateDto { ChoiceText = "Other", IsCorrect = true, IsActive = true });

        Assert.True(result.Succeeded);
        Assert.True(other.IsCorrect);
    }

    [Fact]
    public async Task Update_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, choices) = CreateService();
        choices.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Choice?)null);

        var result = await service.UpdateAsync(Guid.NewGuid(), new ChoiceCreateUpdateDto { ChoiceText = "New" });

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Delete_WithExistingId_RemovesChoice()
    {
        var (service, unitOfWork, choices) = CreateService();
        var choice = new Choice { ChoiceText = "A" };
        choices.Setup(r => r.GetByIdAsync(choice.Id)).ReturnsAsync(choice);

        var result = await service.DeleteAsync(choice.Id);

        Assert.True(result.Succeeded);
        choices.Verify(r => r.Remove(choice), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, choices) = CreateService();
        choices.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Choice?)null);

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
