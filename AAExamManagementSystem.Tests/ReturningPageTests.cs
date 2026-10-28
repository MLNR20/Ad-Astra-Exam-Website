using System.Security.Claims;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Moq;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Tests;

public class ReturningPageTests
{
    private const string UserId = "returning-user";

    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Applicant Applicant { get; }

        public Fixture(bool pendingExam = false)
        {
            Db.QuestionTypes.Add(new QuestionType { Id = 1, Name = QuestionTypes.MultipleChoice });
            foreach (var name in new[] { "Web Development", "Marketing" })
            {
                var section = new Section { Name = name };
                section.Questions.Add(new Question { QuestionTitle = $"{name} question", QuestionTypeId = 1 });
                Db.Sections.Add(section);
            }

            Applicant = new Applicant
            {
                FirstName = "Ana", LastName = "Cruz", StudentNo = "2026-1", Email = "ana@example.test",
                AcademicYear = "2026-2027", UserId = UserId,
            };
            Applicant.Attempts.Add(new Attempt
            {
                Section = "Web Development", AcademicYear = "2026-2027", ChoiceNumber = 1, Password = "pw-1",
                DateTaken = pendingExam ? null : DateTime.UtcNow.ToString("o"),
            });
            Db.Applicants.Add(Applicant);
            Db.SaveChanges();
        }

        public ReturningModel Page(Action<ReturningApplicantDto>? configure = null)
        {
            var userManager = TestSupport.CreateUserManager();
            userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(UserId);

            var model = new ReturningModel(Db, userManager.Object)
            {
                Input = new ReturningApplicantDto
                {
                    StudentNo = "2026-1", FirstName = "Ana", LastName = "Cruz-Reyes", Email = "ana@example.test",
                    AcademicYear = "2027-2028", School = null, TermsRemaining = 3,
                    FirstChoice = "Marketing",
                    FirstExamDate = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
                    FirstExamTime = new TimeOnly(11, 0),
                },
            };
            configure?.Invoke(model.Input);
            TestSupport.Attach(model, TestSupport.PrincipalFor(AppRoles.Applicant));
            return model;
        }
    }

    [Fact]
    public async Task Post_UpdatesTheApplicantAndAddsAttemptsToTheSameApplicant()
    {
        var f = new Fixture();
        var page = f.Page();

        var result = await page.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var applicant = await f.Db.Applicants.Include(a => a.Attempts).SingleAsync();
        Assert.Equal("Cruz-Reyes", applicant.LastName);
        Assert.Equal("2027-2028", applicant.AcademicYear);
        Assert.Equal(2, applicant.Attempts.Count);

        var added = applicant.Attempts.Single(a => a.AcademicYear == "2027-2028");
        Assert.Equal("Marketing", added.Section);
        Assert.Equal(1, added.ChoiceNumber);
        Assert.Equal(new TimeOnly(11, 0), added.ExamTime);
        Assert.Equal(f.Applicant.Id, added.ApplicantId);
    }

    [Fact]
    public async Task Post_WithAnExamStillPending_IsRejected()
    {
        var f = new Fixture(pendingExam: true);
        var page = f.Page();

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Single(f.Db.Attempts);
    }

    [Fact]
    public async Task Post_ForADepartmentAlreadyAppliedToThatYear_IsRejected()
    {
        var f = new Fixture();
        var page = f.Page(i =>
        {
            i.AcademicYear = "2026-2027";
            i.FirstChoice = "Web Development";
        });

        await page.OnPostAsync();

        Assert.True(page.ModelState.ContainsKey("Input.FirstChoice"));
        Assert.Single(f.Db.Attempts);
    }

    [Fact]
    public async Task Post_WithAnotherApplicantsStudentNoOrEmail_IsRejected()
    {
        var f = new Fixture();
        f.Db.Applicants.Add(new Applicant
        {
            FirstName = "Ben", LastName = "Lee", StudentNo = "2026-2", Email = "ben@example.test",
            AcademicYear = "2026-2027", UserId = "someone-else",
        });
        f.Db.SaveChanges();
        var page = f.Page(i =>
        {
            i.StudentNo = "2026-2";
            i.Email = "BEN@example.test";
        });

        await page.OnPostAsync();

        Assert.True(page.ModelState.ContainsKey("Input.StudentNo"));
        Assert.True(page.ModelState.ContainsKey("Input.Email"));
    }

    [Fact]
    public async Task Post_WithNoDepartment_AsksForOne()
    {
        var f = new Fixture();
        var page = f.Page(i =>
        {
            i.FirstChoice = null;
            i.FirstExamDate = null;
            i.FirstExamTime = null;
        });

        await page.OnPostAsync();

        Assert.True(page.ModelState.ContainsKey("Input.FirstChoice"));
    }
}
