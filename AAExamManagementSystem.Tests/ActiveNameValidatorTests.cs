using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Models.Validation;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using CoursesEdit = AAExamManagementSystem.Pages.Courses.EditModel;
using CoursesIndex = AAExamManagementSystem.Pages.Courses.IndexModel;
using DepartmentsIndex = AAExamManagementSystem.Pages.Departments.IndexModel;
using SectionsEdit = AAExamManagementSystem.Pages.Sections.EditModel;
using SectionsIndex = AAExamManagementSystem.Pages.Sections.IndexModel;

namespace AAExamManagementSystem.Tests;

public class ActiveNameValidatorTests
{
    private static IMapper Mapper() => new MapperConfiguration(
        cfg =>
        {
            cfg.CreateMap<Section, SectionDto>();
            cfg.CreateMap<SectionCreateUpdateDto, Section>();
            cfg.CreateMap<Course, CourseDto>();
            cfg.CreateMap<CourseCreateUpdateDto, Course>();
            cfg.CreateMap<Department, DepartmentDto>();
            cfg.CreateMap<DepartmentCreateUpdateDto, Department>();
        },
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();

    // ---------------------------------------------------------------- the utility

    [Theory]
    [InlineData("Photo")]
    [InlineData("photo")]
    [InlineData("  PHOTO  ")]
    public async Task AnActiveRecordWithTheSameName_BlocksIt_IgnoringCaseAndEdgeSpaces(string name)
    {
        var db = TestSupport.CreateContext();
        db.Sections.Add(new Section { Name = "Photo" });
        db.SaveChanges();

        Assert.False(await new ActiveNameValidator(db).IsAvailableAsync(NamedEntityKind.Section, name));
    }

    [Fact]
    public async Task AnInactiveRecordDoesNotBlockTheName()
    {
        var db = TestSupport.CreateContext();
        db.Sections.Add(new Section { Name = "Photo", IsActive = false });
        db.SaveChanges();

        Assert.True(await new ActiveNameValidator(db).IsAvailableAsync(NamedEntityKind.Section, "Photo"));
    }

    [Fact]
    public async Task ARecordDoesNotBlockItsOwnName()
    {
        var db = TestSupport.CreateContext();
        var section = new Section { Name = "Photo" };
        db.Sections.Add(section);
        db.SaveChanges();

        var validator = new ActiveNameValidator(db);

        Assert.True(await validator.IsAvailableAsync(NamedEntityKind.Section, "Photo", section.Id.ToString()));
        Assert.False(await validator.IsAvailableAsync(NamedEntityKind.Section, "Photo", "9999"));
    }

    [Fact]
    public async Task EachKindOfRecordHasItsOwnNames()
    {
        var db = TestSupport.CreateContext();
        db.Sections.Add(new Section { Name = "Photo" });
        db.Courses.Add(new Course { Name = "Design" });
        var department = new Department { Name = "Marketing" };
        db.Departments.Add(department);
        db.SaveChanges();
        var validator = new ActiveNameValidator(db);

        Assert.True(await validator.IsAvailableAsync(NamedEntityKind.Department, "Photo"));
        Assert.False(await validator.IsAvailableAsync(NamedEntityKind.Course, "design"));
        Assert.False(await validator.IsAvailableAsync(NamedEntityKind.Department, "marketing"));
        Assert.True(await validator.IsAvailableAsync(NamedEntityKind.Department, "marketing", department.Id));
    }

    [Fact]
    public async Task ABlankNameIsLeftToTheRequiredRule()
    {
        var db = TestSupport.CreateContext();

        Assert.True(await new ActiveNameValidator(db).IsAvailableAsync(NamedEntityKind.Course, "   "));
    }

    [Fact]
    public async Task Validate_AddsAnErrorUnderTheGivenKey()
    {
        var db = TestSupport.CreateContext();
        db.Courses.Add(new Course { Name = "Design" });
        db.SaveChanges();
        var modelState = new ModelStateDictionary();

        var ok = await new ActiveNameValidator(db).ValidateAsync(modelState, "Course.Name", NamedEntityKind.Course, "design");

        Assert.False(ok);
        Assert.Equal("An active course named 'design' already exists.", modelState["Course.Name"]!.Errors.Single().ErrorMessage);
    }

    // ---------------------------------------------------------------- the pages

    [Fact]
    public async Task CreatingASectionWithAnActiveName_IsRejectedAndNothingIsAdded()
    {
        var db = TestSupport.CreateContext();
        db.Sections.Add(new Section { Name = "Photo" });
        db.SaveChanges();
        var model = new SectionsIndex(new GenericRepository<Section>(db), Mapper(), new ActiveNameValidator(db))
        {
            NewSection = new SectionCreateUpdateDto { Name = "Photo" }
        };
        TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));

        var result = await model.OnPostCreateAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.True(model.ShowCreateModal);
        Assert.Contains(model.ModelState["NewSection.Name"]!.Errors, e => e.ErrorMessage.Contains("already exists"));
        Assert.Equal(1, db.Sections.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task CreatingASectionWithTheNameOfAnInactiveOne_IsAllowed()
    {
        var db = TestSupport.CreateContext();
        db.Sections.Add(new Section { Name = "Photo", IsActive = false });
        db.SaveChanges();
        var model = new SectionsIndex(new GenericRepository<Section>(db), Mapper(), new ActiveNameValidator(db))
        {
            NewSection = new SectionCreateUpdateDto { Name = "Photo" }
        };
        TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));

        var result = await model.OnPostCreateAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(1, db.Sections.Count());
    }

    [Fact]
    public async Task ActivatingASectionWhoseNameAnActiveOneHolds_IsBlocked()
    {
        var db = TestSupport.CreateContext();
        db.Sections.Add(new Section { Name = "Photo" });
        var inactive = new Section { Name = "Photo", IsActive = false };
        db.Sections.Add(inactive);
        db.SaveChanges();
        var model = new SectionsIndex(new GenericRepository<Section>(db), Mapper(), new ActiveNameValidator(db));
        TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));

        await model.OnPostActivateAsync(inactive.Id);

        Assert.False(db.Sections.IgnoreQueryFilters().Single(s => s.Id == inactive.Id).IsActive);
        Assert.Contains("already uses that name", (string)model.TempData["ErrorMessage"]!);
    }

    [Fact]
    public async Task CreatingADepartmentWithAnActiveName_IsRejected()
    {
        var db = TestSupport.CreateContext();
        db.Departments.Add(new Department { Name = "Marketing" });
        db.SaveChanges();
        var model = new DepartmentsIndex(new GenericRepository<Department>(db), db, Mapper(), new ActiveNameValidator(db))
        {
            NewDepartment = new DepartmentCreateUpdateDto { Name = "marketing", IsActive = true }
        };
        TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));

        var result = await model.OnPostCreateAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.Contains(model.ModelState["NewDepartment.Name"]!.Errors, e => e.ErrorMessage.Contains("already exists"));
    }

    [Fact]
    public async Task CreatingAnInactiveDepartmentWithATakenName_IsAllowed()
    {
        var db = TestSupport.CreateContext();
        db.Departments.Add(new Department { Name = "Marketing" });
        db.SaveChanges();
        var model = new DepartmentsIndex(new GenericRepository<Department>(db), db, Mapper(), new ActiveNameValidator(db))
        {
            NewDepartment = new DepartmentCreateUpdateDto { Name = "Marketing", IsActive = false }
        };
        TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));

        Assert.IsType<RedirectToPageResult>(await model.OnPostCreateAsync());
    }

    [Fact]
    public async Task RenamingACourseToAnActiveName_IsRejected_ButKeepingItsOwnNameIsFine()
    {
        var db = TestSupport.CreateContext();
        var design = new Course { Name = "Design" };
        var computing = new Course { Name = "Computing" };
        db.Courses.AddRange(design, computing);
        db.SaveChanges();

        CoursesEdit Edit(Course course, string name)
        {
            var model = new CoursesEdit(new GenericRepository<Course>(db), Mapper(), new ActiveNameValidator(db))
            {
                Id = course.Id,
                Course = new CourseCreateUpdateDto { Name = name, IsActive = true }
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));
            return model;
        }

        var clash = Edit(computing, "Design");
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await clash.OnPostAsync());
        Assert.Contains(clash.ModelState["Course.Name"]!.Errors, e => e.ErrorMessage.Contains("already exists"));
        Assert.Equal("Computing", db.Courses.Single(c => c.Id == computing.Id).Name);

        Assert.IsType<RedirectToPageResult>(await Edit(design, "Design").OnPostAsync());
    }

    [Fact]
    public async Task RenamingAnInactiveSection_NeedsNoUniqueName()
    {
        var db = TestSupport.CreateContext();
        db.Sections.Add(new Section { Name = "Photo" });
        var inactive = new Section { Name = "Old", IsActive = false };
        db.Sections.Add(inactive);
        db.SaveChanges();
        var model = new SectionsEdit(new GenericRepository<Section>(db), Mapper(), new ActiveNameValidator(db))
        {
            Id = inactive.Id,
            Section = new SectionCreateUpdateDto { Name = "Photo" }
        };
        TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));

        Assert.IsType<RedirectToPageResult>(await model.OnPostAsync());
    }

    // ---------------------------------------------------------------- the API filter

    private static async Task<(ActionExecutingContext Context, bool NextRan)> RunFilter(
        ApplicationDbContext db, NamedEntityKind kind, object body, string? id = null)
    {
        var route = new RouteData();
        if (id is not null)
        {
            route.Values["id"] = id;
        }

        var actionContext = new ActionContext(new DefaultHttpContext(), route, new ActionDescriptor(), new ModelStateDictionary());
        var executing = new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?> { ["dto"] = body }, new object());
        var nextRan = false;

        await new UniqueActiveNameFilter(new ActiveNameValidator(db), kind).OnActionExecutionAsync(executing, () =>
        {
            nextRan = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });
        return (executing, nextRan);
    }

    [Fact]
    public async Task TheApiFilter_RejectsADuplicateActiveName()
    {
        var db = TestSupport.CreateContext();
        db.Sections.Add(new Section { Name = "Photo" });
        db.SaveChanges();

        var (context, nextRan) = await RunFilter(db, NamedEntityKind.Section, new SectionCreateUpdateDto { Name = "photo" });

        Assert.False(nextRan);
        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    [Fact]
    public async Task TheApiFilter_LetsAnUpdateKeepItsOwnName_AndAnInactiveBodyThrough()
    {
        var db = TestSupport.CreateContext();
        var section = new Section { Name = "Photo" };
        var department = new Department { Name = "Marketing" };
        db.Sections.Add(section);
        db.Departments.Add(department);
        db.SaveChanges();

        var own = await RunFilter(db, NamedEntityKind.Section, new SectionCreateUpdateDto { Name = "Photo" }, section.Id.ToString());
        var inactive = await RunFilter(db, NamedEntityKind.Department,
            new DepartmentCreateUpdateDto { Name = "Marketing", IsActive = false });

        Assert.True(own.NextRan);
        Assert.True(inactive.NextRan);
    }

    // ---------------------------------------------------------------- the browser check

    [Theory]
    [InlineData(typeof(SectionCreateUpdateDto), NamedEntityKind.Section)]
    [InlineData(typeof(DepartmentCreateUpdateDto), NamedEntityKind.Department)]
    [InlineData(typeof(CourseCreateUpdateDto), NamedEntityKind.Course)]
    public void EachNameIsMarkedForLiveChecking(Type dto, NamedEntityKind kind)
    {
        var attribute = (UniqueActiveNameAttribute?)Attribute.GetCustomAttribute(dto.GetProperty("Name")!, typeof(UniqueActiveNameAttribute));

        Assert.NotNull(attribute);
        Assert.Equal(kind, attribute!.Kind);
    }

    [Theory]
    [InlineData(NamedEntityKind.Department, "An active department with this name already exists.")]
    [InlineData(NamedEntityKind.Section, "An active section with this name already exists.")]
    [InlineData(NamedEntityKind.Course, "An active course with this name already exists.")]
    public void TheBrowserMessageNamesTheKindOfRecord(NamedEntityKind kind, string message)
    {
        var adapter = new UniqueActiveNameAttributeAdapter(new UniqueActiveNameAttribute(kind), null);

        Assert.Equal(message, adapter.GetErrorMessage(null!));
    }

    [Fact]
    public void TheAdapterProviderRendersTheAttributeForTheBrowser_AndLeavesOthersToTheDefault()
    {
        var provider = new UniqueActiveNameAdapterProvider();

        var ours = provider.GetAttributeAdapter(new UniqueActiveNameAttribute(NamedEntityKind.Course), null);
        var theirs = provider.GetAttributeAdapter(new System.ComponentModel.DataAnnotations.RequiredAttribute(), null);

        Assert.IsType<UniqueActiveNameAttributeAdapter>(ours);
        Assert.NotNull(theirs);
        Assert.IsNotType<UniqueActiveNameAttributeAdapter>(theirs);
    }
}
