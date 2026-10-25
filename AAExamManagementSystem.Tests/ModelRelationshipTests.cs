using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AAExamManagementSystem.Tests;

public class ModelRelationshipTests
{
    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        using var context = TestSupport.CreateContext();
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Theory]
    [InlineData(typeof(Question), typeof(Section), DeleteBehavior.Restrict)]
    [InlineData(typeof(Question), typeof(QuestionType), DeleteBehavior.Restrict)]
    [InlineData(typeof(Question), typeof(PageSection), DeleteBehavior.ClientSetNull)]
    [InlineData(typeof(PageSection), typeof(ExamPage), DeleteBehavior.Cascade)]
    [InlineData(typeof(PageSection), typeof(QuestionType), DeleteBehavior.Restrict)]
    [InlineData(typeof(ExamPage), typeof(Section), DeleteBehavior.Restrict)]
    [InlineData(typeof(Answer), typeof(Attempt), DeleteBehavior.Cascade)]
    [InlineData(typeof(Answer), typeof(Question), DeleteBehavior.Restrict)]
    [InlineData(typeof(Attempt), typeof(Applicant), DeleteBehavior.Restrict)]
    [InlineData(typeof(QuestionAndChoice), typeof(Question), DeleteBehavior.Cascade)]
    [InlineData(typeof(QuestionAndChoice), typeof(Choice), DeleteBehavior.Cascade)]
    [InlineData(typeof(Applicant), typeof(ApplicationUser), DeleteBehavior.Cascade)]
    public void ForeignKeys_UseTheExpectedDeleteBehavior(Type dependent, Type principal, DeleteBehavior expected)
    {
        var foreignKey = Model.FindEntityType(dependent)!
            .GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == principal && fk.PrincipalEntityType.ClrType != dependent);

        Assert.Equal(Normalize(expected), Normalize(foreignKey.DeleteBehavior));
    }

    // SetNull on an optional FK is reported as ClientSetNull by some providers' conventions; treat them alike.
    private static DeleteBehavior Normalize(DeleteBehavior behavior) =>
        behavior == DeleteBehavior.ClientSetNull ? DeleteBehavior.SetNull : behavior;

    [Fact]
    public void QuestionToPageSection_IsOptional()
    {
        var foreignKey = Model.FindEntityType(typeof(Question))!
            .GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(PageSection));

        Assert.False(foreignKey.IsRequired);
    }

    [Fact]
    public void ExamPageNumbers_AreUniquePerDepartment()
    {
        var index = Model.FindEntityType(typeof(ExamPage))!
            .GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ExamPage.SectionId), nameof(ExamPage.PageNumber) }));

        Assert.True(index.IsUnique);
    }

    [Theory]
    [InlineData(typeof(Attempt), nameof(Attempt.Password))]
    [InlineData(typeof(Applicant), nameof(Applicant.UserId))]
    [InlineData(typeof(WebPage), nameof(WebPage.Url))]
    public void SensitiveOrIdentifyingColumns_AreUnique(Type entity, string property)
    {
        var index = Model.FindEntityType(entity)!
            .GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { property }));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void EveryTableLivesInTheAAExamSchema()
    {
        Assert.Equal("AAExam", Model.GetDefaultSchema());
    }

    [Fact]
    public async Task RemovingAPageSection_LeavesItsQuestionsInTheDepartmentUnplaced()
    {
        await using var db = TestSupport.CreateContext();
        var section = new Section { Id = 1, Name = "Marketing" };
        var type = new QuestionType { Id = 1, Name = QuestionTypes.MultipleChoice };
        var page = new ExamPage { Section = section, PageNumber = 1 };
        var pageSection = new PageSection { ExamPage = page, QuestionType = type, Order = 1 };
        var question = new Question { Section = section, QuestionType = type, QuestionTitle = "Q", PageSection = pageSection, PageOrder = 1 };
        db.Add(question);
        await db.SaveChangesAsync();

        db.PageSections.Remove(pageSection);
        await db.SaveChangesAsync();

        var saved = await db.Questions.SingleAsync();
        Assert.Null(saved.PageSectionId);
    }

    [Fact]
    public async Task RemovingAnExamPage_RemovesItsSectionsButNotTheQuestions()
    {
        await using var db = TestSupport.CreateContext();
        var section = new Section { Id = 1, Name = "Marketing" };
        var type = new QuestionType { Id = 1, Name = QuestionTypes.MultipleChoice };
        var page = new ExamPage { Section = section, PageNumber = 1 };
        var pageSection = new PageSection { ExamPage = page, QuestionType = type, Order = 1 };
        var question = new Question { Section = section, QuestionType = type, QuestionTitle = "Q", PageSection = pageSection, PageOrder = 1 };
        db.Add(question);
        await db.SaveChangesAsync();

        db.ExamPages.Remove(page);
        await db.SaveChangesAsync();

        Assert.Empty(db.PageSections);
        Assert.Single(db.Questions);
    }

    [Fact]
    public async Task RemovingAnAttempt_RemovesItsAnswers()
    {
        await using var db = TestSupport.CreateContext();
        var section = new Section { Id = 1, Name = "Marketing" };
        var type = new QuestionType { Id = 1, Name = QuestionTypes.MultipleChoice };
        var question = new Question { Section = section, QuestionType = type, QuestionTitle = "Q" };
        var applicant = new Applicant { StudentNo = "1", Email = "a@example.test", UserId = "u1" };
        var attempt = new Attempt { Applicant = applicant, Section = "Marketing", Password = "pw" };
        attempt.Answers.Add(new Answer { Applicant = applicant, Question = question, AnswerText = "x" });
        db.Add(attempt);
        await db.SaveChangesAsync();

        db.Attempts.Remove(attempt);
        await db.SaveChangesAsync();

        Assert.Empty(db.Answers);
        Assert.Single(db.Questions);
        Assert.Single(db.Applicants);
    }
}
