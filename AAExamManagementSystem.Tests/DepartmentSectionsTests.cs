using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages.Departments;
using AAExamManagementSystem.Repository;
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
    public async Task Editing_ListsSectionsHeldElsewhereAsUnavailable()
    {
        var f = new Fixture();
        var model = f.EditFor(f.Academics);

        await model.OnGetAsync();

        Assert.Equal(new[] { f.Photo.Id }, model.SelectedSectionIds);
        Assert.Equal("Operations", model.SectionOptions.Single(o => o.Id == f.Video.Id).OtherDepartment);
        Assert.Null(model.SectionOptions.Single(o => o.Id == f.Web.Id).OtherDepartment);
        Assert.True(model.SectionOptions.Single(o => o.Id == f.Photo.Id).Selected);
    }
}
