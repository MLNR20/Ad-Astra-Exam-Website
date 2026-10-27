using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;

namespace AAExamManagementSystem.Tests;

public class QuestionServiceTests
{
    private class Fixture
    {
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IGenericRepository<Question>> Questions { get; } = new();
        public Mock<IGenericRepository<QuestionType>> QuestionTypes { get; } = new();
        public Mock<IGenericRepository<Section>> Sections { get; } = new();
        public Mock<IQuestionChoiceService> Choices { get; } = new();
        public QuestionService Service { get; }

        public Fixture()
        {
            UnitOfWork.Setup(u => u.Questions).Returns(Questions.Object);
            UnitOfWork.Setup(u => u.QuestionTypes).Returns(QuestionTypes.Object);
            UnitOfWork.Setup(u => u.Sections).Returns(Sections.Object);
            QuestionTypes.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { new QuestionType { Id = 1, Name = "Multiple Choice" } });
            Sections.Setup(r => r.GetAllIncludingInactiveAsync()).ReturnsAsync(new[] { new Section { Id = 2, Name = "Basics" } });
            Choices.Setup(c => c.GetChoicesAsync(It.IsAny<Guid>())).ReturnsAsync(new List<ChoiceDto>());
            Service = new QuestionService(UnitOfWork.Object, Choices.Object, CreateMapper());
        }
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(
            cfg =>
            {
                cfg.CreateMap<Question, QuestionDto>()
                    .ForMember(d => d.QuestionTypeName, o => o.Ignore())
                    .ForMember(d => d.SectionName, o => o.Ignore())
                    .ForMember(d => d.Choices, o => o.Ignore());
                cfg.CreateMap<QuestionCreateUpdateDto, Question>();
            },
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    [Fact]
    public async Task GetAll_PopulatesTypeAndSectionNames()
    {
        var f = new Fixture();
        f.Questions.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
        {
            new Question { QuestionTypeId = 1, SectionId = 2, QuestionTitle = "Q1" },
        });

        var result = (await f.Service.GetAllAsync()).Single();

        Assert.Equal("Multiple Choice", result.QuestionTypeName);
        Assert.Equal("Basics", result.SectionName);
    }

    [Fact]
    public async Task GetById_WithMissingId_ReturnsNull()
    {
        var f = new Fixture();
        f.Questions.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Question?)null);

        Assert.Null(await f.Service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetById_LoadsChoices()
    {
        var f = new Fixture();
        var question = new Question { QuestionTypeId = 1, SectionId = 2 };
        f.Questions.Setup(r => r.GetByIdAsync(question.Id)).ReturnsAsync(question);
        f.Choices.Setup(c => c.GetChoicesAsync(question.Id)).ReturnsAsync(new List<ChoiceDto> { new() { ChoiceText = "A" } });

        var result = await f.Service.GetByIdAsync(question.Id);

        Assert.Single(result!.Choices);
    }

    [Fact]
    public async Task Create_WithInvalidChoices_ReturnsValidationFailed_AndDoesNotSave()
    {
        var f = new Fixture();
        f.Choices.Setup(c => c.NormalizeAndValidateAsync(It.IsAny<QuestionCreateUpdateDto>(), It.IsAny<ModelStateDictionary>(), string.Empty))
            .Callback<QuestionCreateUpdateDto, ModelStateDictionary, string>((_, ms, _) => ms.AddModelError("Choices", "bad"))
            .Returns(Task.CompletedTask);

        var result = await f.Service.CreateAsync(new QuestionCreateUpdateDto { QuestionTypeId = 1, SectionId = 2, QuestionTitle = "Q" });

        Assert.Equal(ServiceStatus.ValidationFailed, result.Status);
        Assert.NotNull(result.ValidationErrors);
        f.Questions.Verify(r => r.AddAsync(It.IsAny<Question>()), Times.Never);
        f.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WithValidDto_AddsQuestionReplacesChoicesAndSaves()
    {
        var f = new Fixture();
        var dto = new QuestionCreateUpdateDto { QuestionTypeId = 1, SectionId = 2, QuestionTitle = "Q" };

        var result = await f.Service.CreateAsync(dto);

        Assert.True(result.Succeeded);
        Assert.Equal("Q", result.Value!.QuestionTitle);
        Assert.Equal("Basics", result.Value.SectionName);
        f.Questions.Verify(r => r.AddAsync(It.Is<Question>(q => q.QuestionTitle == "Q")), Times.Once);
        f.Choices.Verify(c => c.ReplaceChoicesAsync(It.IsAny<Question>(), dto.Choices), Times.Once);
        f.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithMissingId_ReturnsNotFound()
    {
        var f = new Fixture();
        f.Questions.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Question?)null);

        var result = await f.Service.UpdateAsync(Guid.NewGuid(), new QuestionCreateUpdateDto());

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        f.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Update_WithExistingId_UpdatesFieldsAndSaves()
    {
        var f = new Fixture();
        var question = new Question { QuestionTitle = "Old", Score = 1 };
        f.Questions.Setup(r => r.GetByIdAsync(question.Id)).ReturnsAsync(question);

        var result = await f.Service.UpdateAsync(question.Id,
            new QuestionCreateUpdateDto { QuestionTypeId = 1, SectionId = 2, QuestionTitle = "New", Score = 5, IsActive = false });

        Assert.True(result.Succeeded);
        Assert.Equal("New", question.QuestionTitle);
        Assert.Equal(5, question.Score);
        Assert.False(question.IsActive);
        f.Questions.Verify(r => r.Update(question), Times.Once);
        f.Choices.Verify(c => c.ReplaceChoicesAsync(question, It.IsAny<IEnumerable<ChoiceCreateUpdateDto>>()), Times.Once);
        f.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithExistingId_RemovesChoicesAndQuestion()
    {
        var f = new Fixture();
        var question = new Question();
        f.Questions.Setup(r => r.GetByIdAsync(question.Id)).ReturnsAsync(question);

        var result = await f.Service.DeleteAsync(question.Id);

        Assert.True(result.Succeeded);
        f.Choices.Verify(c => c.RemoveChoicesAsync(question.Id), Times.Once);
        f.Questions.Verify(r => r.Remove(question), Times.Once);
        f.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithMissingId_ReturnsNotFound()
    {
        var f = new Fixture();
        f.Questions.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Question?)null);

        var result = await f.Service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        f.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
