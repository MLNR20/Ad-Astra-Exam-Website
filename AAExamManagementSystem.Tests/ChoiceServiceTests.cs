using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Moq;

namespace AAExamManagementSystem.Tests;

public class ChoiceServiceTests
{
    private static (ChoiceService Service, Mock<IUnitOfWork> UnitOfWork, Mock<IGenericRepository<Choice>> Choices) CreateService()
    {
        var choices = new Mock<IGenericRepository<Choice>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Choices).Returns(choices.Object);
        return (new ChoiceService(unitOfWork.Object, CreateMapper()), unitOfWork, choices);
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
