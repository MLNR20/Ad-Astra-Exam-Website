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
            Academics.Sections.Add(Photo);
            Operations.Sections.Add(Video);
            Db.Departments.AddRange(Academics, Operations);
            Db.Sections.Add(Web);
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

        public IndexModel IndexFor(params int[] selected)
        {
            var configuration = new MapperConfiguration(
                cfg =>
                {
                    cfg.CreateMap<Department, DepartmentDto>();
                    cfg.CreateMap<DepartmentCreateUpdateDto, Department>();
                },
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            var model = new IndexModel(new GenericRepository<Department>(Db), Db, configuration.CreateMapper())
            {
                NewDepartment = new DepartmentCreateUpdateDto { Name = "Creative", IsActive = true },
                SelectedSectionIds = selected.ToList(),
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor(Roles.Admin));
            return model;
        }

        public List<string> SectionsOf(string departmentId) => Db.Departments
            .AsNoTracking()
            .Where(d => d.Id == departmentId)
            .SelectMany(d => d.Sections)
            .Select(s => s.Name)
            .ToList()
            .Order()
            .ToList();
    }

    [Fact]
    public async Task Saving_AssignsTickedSectionsAndReleasesUntickedOnes()
    {
        var f = new Fixture();

        var result = await f.EditFor(f.Academics, f.Web.Id).OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(new[] { "Web Development" }, f.SectionsOf(f.Academics.Id));
    }

    [Fact]
    public async Task ASectionCanBelongToSeveralDepartments()
    {
        var f = new Fixture();

        await f.EditFor(f.Academics, f.Photo.Id, f.Video.Id).OnPostAsync();

        Assert.Equal(new[] { "Photo", "Video" }, f.SectionsOf(f.Academics.Id));
        Assert.Equal(new[] { "Video" }, f.SectionsOf(f.Operations.Id));
    }

    [Fact]
    public async Task Saving_WithNothingTickedReleasesEverySection()
    {
        var f = new Fixture();

        await f.EditFor(f.Academics).OnPostAsync();

        Assert.Empty(f.SectionsOf(f.Academics.Id));
        Assert.Equal(new[] { "Video" }, f.SectionsOf(f.Operations.Id));
    }

    [Fact]
    public async Task Editing_OffersEverySectionWithTheDepartmentsOwnTicked()
    {
        var f = new Fixture();
        var model = f.EditFor(f.Academics);

        await model.OnGetAsync();

        Assert.Equal(new[] { f.Photo.Id }, model.SelectedSectionIds);
        Assert.Equal(3, model.SectionOptions.Count);
        Assert.True(model.SectionOptions.Single(o => o.Id == f.Photo.Id).Selected);
        Assert.False(model.SectionOptions.Single(o => o.Id == f.Video.Id).Selected);
    }

    [Fact]
    public async Task Creating_OffersEverySection()
    {
        var f = new Fixture();
        var model = f.IndexFor();

        await model.OnGetAsync();

        Assert.Equal(3, model.SectionOptions.Count);
    }

    [Fact]
    public async Task Creating_AssignsTickedSectionsWithoutTakingThemFromOthers()
    {
        var f = new Fixture();

        await f.IndexFor(f.Web.Id, f.Video.Id).OnPostCreateAsync();

        var created = f.Db.Departments.Single(d => d.Name == "Creative");
        Assert.Equal(new[] { "Video", "Web Development" }, f.SectionsOf(created.Id));
        Assert.Equal(new[] { "Video" }, f.SectionsOf(f.Operations.Id));
    }

    [Fact]
    public async Task DepartmentsIndex_ListsEachDepartmentsSectionsAlphabetically()
    {
        var f = new Fixture();
        f.Academics.Sections.Add(f.Web);
        f.Db.SaveChanges();
        var model = f.IndexFor();

        await model.OnGetAsync();

        Assert.Equal("Photo, Web Development", model.SectionNames[f.Academics.Id]);
        Assert.Equal("Video", model.SectionNames[f.Operations.Id]);
    }

    [Fact]
    public async Task DepartmentsIndex_HasNoEntryForADepartmentWithoutSections()
    {
        var f = new Fixture();
        var empty = new Department { Name = "Empty" };
        f.Db.Departments.Add(empty);
        f.Db.SaveChanges();
        var model = f.IndexFor();

        await model.OnGetAsync();

        Assert.False(model.SectionNames.ContainsKey(empty.Id));
    }

    [Fact]
    public async Task SectionDetails_ListsEveryDepartmentTheSectionBelongsTo()
    {
        var f = new Fixture();
        f.Operations.Sections.Add(f.Photo);
        f.Db.SaveChanges();
        var configuration = new MapperConfiguration(
            cfg => cfg.CreateMap<Section, SectionDto>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var model = new AAExamManagementSystem.Pages.Sections.DetailsModel(new GenericRepository<Section>(f.Db), f.Db, configuration.CreateMapper());

        await model.OnGetAsync(f.Photo.Id);

        Assert.Equal("Academics, Operations", model.DepartmentNames);
    }
}
