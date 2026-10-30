using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages.Departments;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Tests;

public class DepartmentSectionsTests
{
    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Department Academics { get; } = new() { Name = "Academics" };
        public Department Operations { get; } = new() { Name = "Operations" };
        public Section Web { get; } = new() { Name = "Web Development" };
        public Section Photo { get; } = new() { Name = "Photo" };
        public Section Video { get; } = new() { Name = "Video" };

        public Fixture()
        {
            Photo.DepartmentId = Academics.Id;
            Video.DepartmentId = Operations.Id;
            Db.Departments.AddRange(Academics, Operations);
            Db.Sections.AddRange(Web, Photo, Video);
            Db.SaveChanges();
        }

        public EditModel EditFor(Department department, params int[] selected)
        {
            var model = new EditModel(new GenericRepository<Department>(Db), Db, null!)
            {
                Id = department.Id,
                Department = new DepartmentCreateUpdateDto { Name = department.Name, IsActive = true },
                SelectedSectionIds = selected.ToList(),
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));
            return model;
        }

        public string? DepartmentOf(Section section) => Db.Sections.AsNoTracking().Single(s => s.Id == section.Id).DepartmentId;
    }

    [Fact]
    public async Task Saving_AssignsTickedSectionsAndReleasesUntickedOnes()
    {
        var f = new Fixture();

        var result = await f.EditFor(f.Academics, f.Web.Id).OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(f.Academics.Id, f.DepartmentOf(f.Web));
        Assert.Null(f.DepartmentOf(f.Photo));
    }

    [Fact]
    public async Task Saving_NeverTakesASectionFromAnotherDepartment()
    {
        var f = new Fixture();

        await f.EditFor(f.Academics, f.Photo.Id, f.Video.Id).OnPostAsync();

        Assert.Equal(f.Academics.Id, f.DepartmentOf(f.Photo));
        Assert.Equal(f.Operations.Id, f.DepartmentOf(f.Video));
    }

    [Fact]
    public async Task Editing_OffersOnlyFreeSectionsAndThisDepartmentsOwn()
    {
        var f = new Fixture();
        var model = f.EditFor(f.Academics);

        await model.OnGetAsync();

        Assert.Equal(new[] { f.Photo.Id }, model.SelectedSectionIds);
        Assert.Equal(new[] { f.Photo.Id, f.Web.Id }.Order(), model.SectionOptions.Select(o => o.Id).Order());
        Assert.DoesNotContain(model.SectionOptions, o => o.Id == f.Video.Id);
        Assert.True(model.SectionOptions.Single(o => o.Id == f.Photo.Id).Selected);
        Assert.False(model.SectionOptions.Single(o => o.Id == f.Web.Id).Selected);
    }

    private static IndexModel CreateIndex(Fixture f, params int[] selected)
    {
        var configuration = new MapperConfiguration(
            cfg =>
            {
                cfg.CreateMap<Department, DepartmentDto>();
                cfg.CreateMap<DepartmentCreateUpdateDto, Department>();
            },
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var model = new IndexModel(new GenericRepository<Department>(f.Db), f.Db, configuration.CreateMapper())
        {
            NewDepartment = new DepartmentCreateUpdateDto { Name = "Creative", IsActive = true },
            SelectedSectionIds = selected.ToList(),
        };
        TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));
        return model;
    }

    [Fact]
    public async Task Creating_OffersOnlyUnassignedSections()
    {
        var f = new Fixture();
        var model = CreateIndex(f);

        await model.OnGetAsync();

        Assert.Equal(new[] { f.Web.Id }, model.SectionOptions.Select(o => o.Id));
    }

    [Fact]
    public async Task Creating_AssignsFreeSectionsAndIgnoresOnesHeldElsewhere()
    {
        var f = new Fixture();

        await CreateIndex(f, f.Web.Id, f.Video.Id).OnPostCreateAsync();

        var created = f.Db.Departments.Single(d => d.Name == "Creative");
        Assert.Equal(created.Id, f.DepartmentOf(f.Web));
        Assert.Equal(f.Operations.Id, f.DepartmentOf(f.Video));
    }

    [Fact]
    public async Task SectionsIndex_ReportsEachSectionsDepartmentIncludingInactiveOnes()
    {
        var f = new Fixture();
        f.Operations.IsActive = false;
        f.Db.SaveChanges();
        var configuration = new MapperConfiguration(
            cfg => cfg.CreateMap<Section, SectionDto>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var model = new AAExamManagementSystem.Pages.Sections.IndexModel(new GenericRepository<Section>(f.Db), f.Db, configuration.CreateMapper());
        TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));

        await model.OnGetAsync();

        Assert.Equal("Academics", model.DepartmentNames[f.Photo.Id]);
        Assert.Equal("Operations", model.DepartmentNames[f.Video.Id]);
        Assert.False(model.DepartmentNames.ContainsKey(f.Web.Id));
    }
}
