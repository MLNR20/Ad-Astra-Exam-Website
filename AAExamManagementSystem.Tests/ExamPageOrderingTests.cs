using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages.PageSections;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Tests;

public class ExamPageOrderingTests
{
    private const int MarketingId = 1;
    private const int PhotoId = 2;
    private const int MultipleChoiceId = 1;
    private const int EssayId = 2;

    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();

        public Fixture()
        {
            Db.Sections.AddRange(
                new Section { Id = MarketingId, Name = "Marketing" },
                new Section { Id = PhotoId, Name = "Photo" });
            Db.QuestionTypes.AddRange(
                new QuestionType { Id = MultipleChoiceId, Name = QuestionTypes.MultipleChoice },
                new QuestionType { Id = EssayId, Name = QuestionTypes.Essay });
            Db.SaveChanges();
        }

        public Question AddQuestion(string title, int sectionId = MarketingId, int typeId = MultipleChoiceId, int minutesOld = 0)
        {
            var question = new Question
            {
                QuestionTitle = title,
                SectionId = sectionId,
                QuestionTypeId = typeId,
                DateCreated = DateTime.UtcNow.AddMinutes(-minutesOld),
            };
            Db.Questions.Add(question);
            Db.SaveChanges();
            return question;
        }

        public (ExamPage Page, PageSection Section) Place(
            int pageNumber, int order, int typeId, int sectionId = MarketingId, params (Question Question, int Order)[] placed)
        {
            var page = Db.ExamPages.FirstOrDefault(p => p.SectionId == sectionId && p.PageNumber == pageNumber);
            if (page is null)
            {
                page = new ExamPage { SectionId = sectionId, PageNumber = pageNumber };
                Db.ExamPages.Add(page);
            }

            var section = new PageSection { ExamPage = page, QuestionTypeId = typeId, Order = order };
            Db.PageSections.Add(section);
            foreach (var (question, questionOrder) in placed)
            {
                question.PageSection = section;
                question.PageOrder = questionOrder;
            }

            Db.SaveChanges();
            return (page, section);
        }

        public IndexModel EditorOf(int? sectionId) => Create(AppRoles.Editor,
            sectionId is null ? null : new ApplicationUser { Id = "editor", SectionId = sectionId });

        public IndexModel Admin(int? selectedSectionId = null)
        {
            var model = Create(AppRoles.Admin, null);
            model.SectionId = selectedSectionId;
            return model;
        }

        private IndexModel Create(string role, ApplicationUser? currentUser)
        {
            var model = new IndexModel(Db, TestSupport.UserManagerReturning(currentUser).Object);
            TestSupport.Attach(model, TestSupport.PrincipalFor(role));
            return model;
        }
    }

    [Fact]
    public async Task OnGet_ListsPagesInPageNumberOrderRegardlessOfInsertionOrder()
    {
        var f = new Fixture();
        var q1 = f.AddQuestion("On page two");
        var q2 = f.AddQuestion("On page one");
        f.Place(2, 1, MultipleChoiceId, MarketingId, (q1, 1));
        f.Place(1, 1, MultipleChoiceId, MarketingId, (q2, 1));
        var model = f.EditorOf(MarketingId);

        await model.OnGetAsync();

        Assert.Equal(new[] { 1, 2 }, model.Pages.Select(g => g.Key));
        Assert.Equal(new[] { "On page one", "On page two" }, model.Pages.SelectMany(g => g).Select(d => d.QuestionTitle));
    }

    [Fact]
    public async Task OnGet_OrdersSectionsWithinAPageThenQuestionsWithinASection()
    {
        var f = new Fixture();
        var a1 = f.AddQuestion("A first");
        var a2 = f.AddQuestion("A second");
        var b1 = f.AddQuestion("B first", typeId: EssayId);
        var b2 = f.AddQuestion("B second", typeId: EssayId);
        // Inserted out of order on purpose: the essay section is created first but ordered second.
        f.Place(1, 2, EssayId, MarketingId, (b2, 2), (b1, 1));
        f.Place(1, 1, MultipleChoiceId, MarketingId, (a2, 2), (a1, 1));
        var model = f.EditorOf(MarketingId);

        await model.OnGetAsync();

        var items = model.Pages.Single().ToList();
        Assert.Equal(new[] { "A first", "A second", "B first", "B second" }, items.Select(d => d.QuestionTitle));
        Assert.Equal(new[] { 1, 1, 2, 2 }, items.Select(d => d.SectionNumber));
    }

    [Fact]
    public async Task OnGet_LeavesOutInactiveQuestionsAndSections()
    {
        var f = new Fixture();
        var kept = f.AddQuestion("Kept");
        var inactive = f.AddQuestion("Inactive question");
        var hidden = f.AddQuestion("In inactive section");
        f.Place(1, 1, MultipleChoiceId, MarketingId, (kept, 1), (inactive, 2));
        var (_, hiddenSection) = f.Place(1, 2, MultipleChoiceId, MarketingId, (hidden, 1));
        inactive.IsActive = false;
        hiddenSection.IsActive = false;
        await f.Db.SaveChangesAsync();
        var model = f.EditorOf(MarketingId);

        await model.OnGetAsync();

        Assert.Equal(new[] { "Kept" }, model.Pages.SelectMany(g => g).Select(d => d.QuestionTitle));
    }

    [Fact]
    public async Task OnGet_ListsQuestionsThatAreNotOnAnyPageAsUnassigned()
    {
        var f = new Fixture();
        var placed = f.AddQuestion("Placed");
        f.AddQuestion("Loose", minutesOld: 5);
        f.Place(1, 1, MultipleChoiceId, MarketingId, (placed, 1));
        var model = f.EditorOf(MarketingId);

        await model.OnGetAsync();

        Assert.Equal(new[] { "Loose" }, model.UnassignedQuestions.Select(q => q.QuestionTitle));
        Assert.Equal(2, model.AssignableQuestions.Count);
    }

    [Fact]
    public async Task OnGet_ForAnEditor_ShowsOnlyTheirDepartment()
    {
        var f = new Fixture();
        var marketing = f.AddQuestion("Marketing question");
        var photo = f.AddQuestion("Photo question", PhotoId);
        f.Place(1, 1, MultipleChoiceId, MarketingId, (marketing, 1));
        f.Place(1, 1, MultipleChoiceId, PhotoId, (photo, 1));
        var model = f.EditorOf(PhotoId);

        await model.OnGetAsync();

        Assert.Equal("Photo", model.CurrentSectionName);
        Assert.Equal(new[] { "Photo question" }, model.Pages.SelectMany(g => g).Select(d => d.QuestionTitle));
    }

    [Fact]
    public async Task OnGet_ForAnEditorWithoutADepartment_FlagsThatNoneIsAssigned()
    {
        var f = new Fixture();
        var model = f.EditorOf(null);

        await model.OnGetAsync();

        Assert.True(model.NoDepartmentAssigned);
        Assert.Empty(model.Pages);
    }

    [Fact]
    public async Task OnGet_ForAnAdmin_AsksForADepartmentFirst()
    {
        var f = new Fixture();
        var model = f.Admin();

        await model.OnGetAsync();

        Assert.True(model.RequiresSectionSelection);
        Assert.Equal(2, model.SelectableSections.Count);
    }

    [Fact]
    public async Task Bundle_PutsQuestionsOfATypeOnAPageOldestFirstNumberedFromOne()
    {
        var f = new Fixture();
        var newest = f.AddQuestion("Newest", minutesOld: 1);
        var oldest = f.AddQuestion("Oldest", minutesOld: 30);
        var middle = f.AddQuestion("Middle", minutesOld: 10);
        var model = f.EditorOf(MarketingId);

        var result = await model.OnPostBundleAsync(MultipleChoiceId, 1);

        Assert.IsType<RedirectToPageResult>(result);
        var ordered = await f.Db.Questions.Where(q => q.PageSectionId != null).OrderBy(q => q.PageOrder).ToListAsync();
        Assert.Equal(new[] { oldest.Id, middle.Id, newest.Id }, ordered.Select(q => q.Id));
        Assert.Equal(new[] { 1, 2, 3 }, ordered.Select(q => q.PageOrder));
        Assert.Single(f.Db.ExamPages);
    }

    [Fact]
    public async Task Bundle_CreatesTheNextSectionAfterTheExistingOnes()
    {
        var f = new Fixture();
        var existing = f.AddQuestion("Existing essay", typeId: EssayId);
        f.Place(1, 1, EssayId, MarketingId, (existing, 1));
        f.AddQuestion("Fresh multiple choice");
        var model = f.EditorOf(MarketingId);

        await model.OnPostBundleAsync(MultipleChoiceId, 1);

        var sections = await f.Db.PageSections.OrderBy(ps => ps.Order).ToListAsync();
        Assert.Equal(new[] { EssayId, MultipleChoiceId }, sections.Select(ps => ps.QuestionTypeId));
        Assert.Equal(new[] { 1, 2 }, sections.Select(ps => ps.Order));
    }

    [Fact]
    public async Task Bundle_AppendsNewQuestionsAfterTheOnesAlreadyInTheSection()
    {
        var f = new Fixture();
        var first = f.AddQuestion("First", minutesOld: 20);
        var second = f.AddQuestion("Second", minutesOld: 10);
        f.Place(1, 1, MultipleChoiceId, MarketingId, (second, 1), (first, 2));
        var late = f.AddQuestion("Late arrival", minutesOld: 1);
        var model = f.EditorOf(MarketingId);

        await model.OnPostBundleAsync(MultipleChoiceId, 1);

        var ordered = await f.Db.Questions.OrderBy(q => q.PageOrder).ToListAsync();
        Assert.Equal(new[] { second.Id, first.Id, late.Id }, ordered.Select(q => q.Id));
        Assert.Equal(new[] { 1, 2, 3 }, ordered.Select(q => q.PageOrder));
    }

    [Fact]
    public async Task Bundle_WithoutMatchingQuestions_ReportsAnErrorAndCreatesNothing()
    {
        var f = new Fixture();
        var model = f.EditorOf(MarketingId);

        await model.OnPostBundleAsync(EssayId, 1);

        Assert.False(string.IsNullOrEmpty(model.TempData["ErrorMessage"] as string));
        Assert.Empty(f.Db.ExamPages);
    }

    [Fact]
    public async Task Bundle_TreatsAPageNumberBelowOneAsPageOne()
    {
        var f = new Fixture();
        f.AddQuestion("Only");
        var model = f.EditorOf(MarketingId);

        await model.OnPostBundleAsync(MultipleChoiceId, -4);

        Assert.Equal(1, Assert.Single(f.Db.ExamPages).PageNumber);
    }

    [Fact]
    public async Task Bundle_OnlyTouchesTheEditorsOwnDepartment()
    {
        var f = new Fixture();
        f.AddQuestion("Marketing question");
        var photo = f.AddQuestion("Photo question", PhotoId);
        var model = f.EditorOf(PhotoId);

        await model.OnPostBundleAsync(MultipleChoiceId, 1);

        var page = Assert.Single(f.Db.ExamPages);
        Assert.Equal(PhotoId, page.SectionId);
        Assert.NotNull((await f.Db.Questions.FindAsync(photo.Id))!.PageSectionId);
        Assert.Single(f.Db.Questions.Where(q => q.PageSectionId != null));
    }

    [Fact]
    public async Task Bundle_ForAnEditorWithoutADepartment_IsForbidden()
    {
        var f = new Fixture();
        f.AddQuestion("Q");
        var model = f.EditorOf(null);

        Assert.IsType<ForbidResult>(await model.OnPostBundleAsync(MultipleChoiceId, 1));
        Assert.Empty(f.Db.ExamPages);
    }

    [Fact]
    public async Task AddSection_PutsTheNewSectionLastAndKeepsTheCheckboxOrder()
    {
        var f = new Fixture();
        var existing = f.AddQuestion("Existing");
        f.Place(1, 1, MultipleChoiceId, MarketingId, (existing, 1));
        var e1 = f.AddQuestion("Essay one", typeId: EssayId, minutesOld: 20);
        var e2 = f.AddQuestion("Essay two", typeId: EssayId, minutesOld: 10);
        var model = f.EditorOf(MarketingId);

        await model.OnPostAddSectionAsync(1, EssayId, new List<Guid> { e2.Id, e1.Id }, "  Write an essay  ", null);

        var essaySection = await f.Db.PageSections.SingleAsync(ps => ps.QuestionTypeId == EssayId);
        Assert.Equal(2, essaySection.Order);
        Assert.Equal("Write an essay", essaySection.Instruction);
        Assert.Null(essaySection.SubInstruction);
        Assert.Equal(1, (await f.Db.Questions.FindAsync(e2.Id))!.PageOrder);
        Assert.Equal(2, (await f.Db.Questions.FindAsync(e1.Id))!.PageOrder);
    }

    [Fact]
    public async Task AddSection_IgnoresQuestionsOfTheWrongType()
    {
        var f = new Fixture();
        var existing = f.AddQuestion("Existing");
        f.Place(1, 1, MultipleChoiceId, MarketingId, (existing, 1));
        var wrongType = f.AddQuestion("Multiple choice, not essay");
        var model = f.EditorOf(MarketingId);

        await model.OnPostAddSectionAsync(1, EssayId, new List<Guid> { wrongType.Id }, null, null);

        Assert.Single(f.Db.PageSections);
        Assert.False(string.IsNullOrEmpty(model.TempData["ErrorMessage"] as string));
    }

    [Fact]
    public async Task AddSection_OnAPageThatDoesNotExist_ReportsAnError()
    {
        var f = new Fixture();
        var q = f.AddQuestion("Essay", typeId: EssayId);
        var model = f.EditorOf(MarketingId);

        await model.OnPostAddSectionAsync(7, EssayId, new List<Guid> { q.Id }, null, null);

        Assert.Empty(f.Db.PageSections);
        Assert.Contains("7", (string)model.TempData["ErrorMessage"]!);
    }

    [Fact]
    public async Task Reorder_RewritesTheQuestionOrder()
    {
        var f = new Fixture();
        var a = f.AddQuestion("A");
        var b = f.AddQuestion("B");
        var c = f.AddQuestion("C");
        f.Place(1, 1, MultipleChoiceId, MarketingId, (a, 1), (b, 2), (c, 3));
        var model = f.EditorOf(MarketingId);

        await model.OnPostReorderAsync(1, new Dictionary<Guid, int> { [a.Id] = 3, [b.Id] = 1, [c.Id] = 2 });

        var ordered = await f.Db.Questions.OrderBy(q => q.PageOrder).Select(q => q.QuestionTitle).ToListAsync();
        Assert.Equal(new[] { "B", "C", "A" }, ordered);
    }

    [Fact]
    public async Task Reorder_IgnoresQuestionsFromAnotherDepartmentOrPage()
    {
        var f = new Fixture();
        var mine = f.AddQuestion("Mine");
        var otherDepartment = f.AddQuestion("Other department", PhotoId);
        var otherPage = f.AddQuestion("Other page");
        f.Place(1, 1, MultipleChoiceId, MarketingId, (mine, 1));
        f.Place(1, 1, MultipleChoiceId, PhotoId, (otherDepartment, 1));
        f.Place(2, 1, MultipleChoiceId, MarketingId, (otherPage, 1));
        var model = f.EditorOf(MarketingId);

        await model.OnPostReorderAsync(1, new Dictionary<Guid, int>
        {
            [mine.Id] = 5,
            [otherDepartment.Id] = 9,
            [otherPage.Id] = 9,
        });

        Assert.Equal(5, (await f.Db.Questions.FindAsync(mine.Id))!.PageOrder);
        Assert.Equal(1, (await f.Db.Questions.FindAsync(otherDepartment.Id))!.PageOrder);
        Assert.Equal(1, (await f.Db.Questions.FindAsync(otherPage.Id))!.PageOrder);
    }

    [Fact]
    public async Task Remove_TakesTheQuestionOffItsPageAndDropsTheEmptyPage()
    {
        var f = new Fixture();
        var only = f.AddQuestion("Only");
        f.Place(1, 1, MultipleChoiceId, MarketingId, (only, 1));
        var model = f.EditorOf(MarketingId);

        var result = await model.OnPostRemoveAsync(only.Id);

        Assert.IsType<RedirectToPageResult>(result);
        var question = await f.Db.Questions.FindAsync(only.Id);
        Assert.Null(question!.PageSectionId);
        Assert.Equal(0, question.PageOrder);
        Assert.Empty(f.Db.PageSections);
        Assert.Empty(f.Db.ExamPages);
    }

    [Fact]
    public async Task Remove_KeepsThePageWhileItStillHasQuestions()
    {
        var f = new Fixture();
        var staying = f.AddQuestion("Staying");
        var leaving = f.AddQuestion("Leaving");
        f.Place(1, 1, MultipleChoiceId, MarketingId, (staying, 1), (leaving, 2));
        var model = f.EditorOf(MarketingId);

        await model.OnPostRemoveAsync(leaving.Id);

        Assert.Single(f.Db.ExamPages);
        Assert.Single(f.Db.PageSections);
        Assert.NotNull((await f.Db.Questions.FindAsync(staying.Id))!.PageSectionId);
    }

    [Fact]
    public async Task Remove_RefusesAQuestionFromAnotherDepartment()
    {
        var f = new Fixture();
        var photo = f.AddQuestion("Photo question", PhotoId);
        f.Place(1, 1, MultipleChoiceId, PhotoId, (photo, 1));
        var model = f.EditorOf(MarketingId);

        Assert.IsType<ForbidResult>(await model.OnPostRemoveAsync(photo.Id));
        Assert.NotNull((await f.Db.Questions.FindAsync(photo.Id))!.PageSectionId);
    }

    [Fact]
    public async Task Remove_ForAQuestionThatIsNotOnAPage_ReturnsNotFound()
    {
        var f = new Fixture();
        var loose = f.AddQuestion("Loose");
        var model = f.EditorOf(MarketingId);

        Assert.IsType<NotFoundResult>(await model.OnPostRemoveAsync(loose.Id));
    }

    [Fact]
    public async Task SetSectionInstruction_RefusesASectionOfAnotherDepartment()
    {
        var f = new Fixture();
        var photo = f.AddQuestion("Photo question", PhotoId);
        var (_, photoSection) = f.Place(1, 1, MultipleChoiceId, PhotoId, (photo, 1));
        var model = f.EditorOf(MarketingId);

        var result = await model.OnPostSetSectionInstructionAsync(photoSection.Id, "Hijacked", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Null((await f.Db.PageSections.FindAsync(photoSection.Id))!.Instruction);
    }

    [Fact]
    public async Task SetSectionInstruction_TrimsTextAndStoresBlankAsNull()
    {
        var f = new Fixture();
        var q = f.AddQuestion("Q");
        var (_, section) = f.Place(1, 1, MultipleChoiceId, MarketingId, (q, 1));
        var model = f.EditorOf(MarketingId);

        await model.OnPostSetSectionInstructionAsync(section.Id, "  Pick one  ", "   ");

        var saved = await f.Db.PageSections.FindAsync(section.Id);
        Assert.Equal("Pick one", saved!.Instruction);
        Assert.Null(saved.SubInstruction);
    }

    [Fact]
    public async Task AnAdmin_CanReorderInTheDepartmentTheyPicked()
    {
        var f = new Fixture();
        var a = f.AddQuestion("A", PhotoId);
        var b = f.AddQuestion("B", PhotoId);
        f.Place(1, 1, MultipleChoiceId, PhotoId, (a, 1), (b, 2));
        var model = f.Admin(PhotoId);

        await model.OnPostReorderAsync(1, new Dictionary<Guid, int> { [a.Id] = 2, [b.Id] = 1 });

        Assert.Equal(2, (await f.Db.Questions.FindAsync(a.Id))!.PageOrder);
        Assert.Equal(1, (await f.Db.Questions.FindAsync(b.Id))!.PageOrder);
    }

    [Fact]
    public async Task AnAdmin_WithoutAPickedDepartment_IsForbiddenFromChangingPages()
    {
        var f = new Fixture();
        f.AddQuestion("Q");
        var model = f.Admin();

        Assert.IsType<ForbidResult>(await model.OnPostBundleAsync(MultipleChoiceId, 1));
    }
}
