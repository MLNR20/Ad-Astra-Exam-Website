using AAExamManagementSystem.Controllers;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;

namespace AAExamManagementSystem.Tests;

// The controllers only translate service outcomes into HTTP results.
public class ServiceControllerTests
{
    [Fact]
    public async Task Roles_GetById_WithMissingRole_ReturnsNotFound()
    {
        var service = new Mock<IRoleService>();
        service.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((RoleDto?)null);

        var result = await new RolesController(service.Object).GetById("missing");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Roles_Create_WithInvalidResult_ReturnsBadRequest()
    {
        var service = new Mock<IRoleService>();
        service.Setup(s => s.CreateAsync(It.IsAny<RoleCreateUpdateDto>()))
            .ReturnsAsync(ServiceResult<RoleDto>.Invalid("exists"));

        var result = await new RolesController(service.Object).Create(new RoleCreateUpdateDto { Name = "Admin" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Roles_Create_WithSuccess_ReturnsCreatedAtAction()
    {
        var service = new Mock<IRoleService>();
        service.Setup(s => s.CreateAsync(It.IsAny<RoleCreateUpdateDto>()))
            .ReturnsAsync(ServiceResult<RoleDto>.Success(new RoleDto { Id = "r1", Name = "Grader" }));

        var result = await new RolesController(service.Object).Create(new RoleCreateUpdateDto { Name = "Grader" });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal("Grader", Assert.IsType<RoleDto>(created.Value).Name);
    }

    [Theory]
    [InlineData(ServiceStatus.Success, typeof(NoContentResult))]
    [InlineData(ServiceStatus.NotFound, typeof(NotFoundResult))]
    [InlineData(ServiceStatus.Invalid, typeof(BadRequestObjectResult))]
    public async Task Roles_Update_MapsServiceStatusToHttpResult(ServiceStatus status, Type expected)
    {
        var service = new Mock<IRoleService>();
        service.Setup(s => s.UpdateAsync("r1", It.IsAny<RoleCreateUpdateDto>()))
            .ReturnsAsync(new ServiceResult { Status = status, Error = "error" });

        var result = await new RolesController(service.Object).Update("r1", new RoleCreateUpdateDto { Name = "Grader" });

        Assert.IsType(expected, result);
    }

    [Fact]
    public async Task Sections_Delete_WithMissingSection_ReturnsNotFound()
    {
        var service = new Mock<ISectionService>();
        service.Setup(s => s.DeleteAsync(99)).ReturnsAsync(ServiceResult.NotFound());

        var result = await new SectionsController(service.Object).Delete(99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Sections_Create_ReturnsCreatedAtAction()
    {
        var service = new Mock<ISectionService>();
        service.Setup(s => s.CreateAsync(It.IsAny<SectionCreateUpdateDto>()))
            .ReturnsAsync(new SectionDto { Id = 1, Name = "New Section" });

        var result = await new SectionsController(service.Object).Create(new SectionCreateUpdateDto { Name = "New Section" });

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Departments_Update_WithExistingDepartment_ReturnsNoContent()
    {
        var service = new Mock<IDepartmentService>();
        service.Setup(s => s.UpdateAsync("d1", It.IsAny<DepartmentCreateUpdateDto>())).ReturnsAsync(ServiceResult.Success());

        var result = await new DepartmentsController(service.Object).Update("d1", new DepartmentCreateUpdateDto { Name = "A" });

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Departments_GetById_WithMissingDepartment_ReturnsNotFound()
    {
        var service = new Mock<IDepartmentService>();
        service.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((DepartmentDto?)null);

        var result = await new DepartmentsController(service.Object).GetById("missing");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Courses_Update_WithMissingCourse_ReturnsNotFound()
    {
        var service = new Mock<ICourseService>();
        service.Setup(s => s.UpdateAsync(99, It.IsAny<CourseCreateUpdateDto>())).ReturnsAsync(ServiceResult.NotFound());

        var result = await new CoursesController(service.Object).Update(99, new CourseCreateUpdateDto { Name = "A" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Choices_Delete_WithExistingChoice_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IChoiceService>();
        service.Setup(s => s.DeleteAsync(id)).ReturnsAsync(ServiceResult.Success());

        var result = await new ChoicesController(service.Object).Delete(id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Questions_Create_WithValidationFailure_ReturnsValidationProblem()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Choices", "bad");
        var service = new Mock<IQuestionService>();
        service.Setup(s => s.CreateAsync(It.IsAny<QuestionCreateUpdateDto>()))
            .ReturnsAsync(ServiceResult<QuestionDto>.ValidationFailed(modelState));

        var result = await new QuestionsController(service.Object).Create(new QuestionCreateUpdateDto());

        Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(((ObjectResult)result.Result!).Value);
    }

    [Fact]
    public async Task Questions_Update_WithMissingQuestion_ReturnsNotFound()
    {
        var service = new Mock<IQuestionService>();
        service.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<QuestionCreateUpdateDto>())).ReturnsAsync(ServiceResult.NotFound());

        var result = await new QuestionsController(service.Object).Update(Guid.NewGuid(), new QuestionCreateUpdateDto());

        Assert.IsType<NotFoundResult>(result);
    }
}
