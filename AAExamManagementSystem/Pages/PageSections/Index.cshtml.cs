using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages.PageSections;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Instructor},{AppRoles.Staffer},{AppRoles.Editor}")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public int? SectionId { get; set; }

    public bool IsAdmin { get; set; }

    public bool RequiresSectionSelection { get; set; }

    public bool NoDepartmentAssigned { get; set; }

    public IList<Section> SelectableSections { get; set; } = new List<Section>();

    public string CurrentSectionName { get; set; } = string.Empty;

    public List<IGrouping<int, PageSectionDto>> Pages { get; set; } = new();

    public IList<PageSectionDto> UnassignedQuestions { get; set; } = new List<PageSectionDto>();

    public IList<PageSectionDto> AssignableQuestions { get; set; } = new List<PageSectionDto>();

    public SelectList QuestionTypeOptions { get; set; } = new(new List<QuestionType>(), "Id", "Name");

    public async Task<IActionResult> OnGetAsync()
    {
        IsAdmin = User.IsInRole(AppRoles.Admin);

        var allSections = await _context.Sections
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        int? effectiveSectionId;

        if (IsAdmin)
        {
            SelectableSections = allSections;

            if (SectionId is null)
            {
                RequiresSectionSelection = true;
                return Page();
            }

            effectiveSectionId = SectionId;
        }
        else
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SectionId is null)
            {
                NoDepartmentAssigned = true;
                return Page();
            }

            effectiveSectionId = currentUser.SectionId;
        }

        var section = allSections.FirstOrDefault(s => s.Id == effectiveSectionId);
        if (section is null)
        {
            NoDepartmentAssigned = true;
            return Page();
        }

        CurrentSectionName = section.Name;
        SectionId = section.Id;

        var questionTypes = await _context.QuestionTypes
            .AsNoTracking()
            .Where(qt => qt.IsActive)
            .OrderBy(qt => qt.Name)
            .ToListAsync();
        var typeNamesById = questionTypes.ToDictionary(qt => qt.Id, qt => qt.Name);
        QuestionTypeOptions = new SelectList(questionTypes, "Id", "Name");

        var examPages = await _context.ExamPages
            .AsNoTracking()
            .Include(p => p.PageSections)
            .ThenInclude(ps => ps.Questions)
            .Where(p => p.IsActive && p.SectionId == section.Id)
            .ToListAsync();

        var pageSectionDtos = examPages
            .SelectMany(p => p.PageSections
                .Where(ps => ps.IsActive)
                .OrderBy(ps => ps.Order)
                .ThenBy(ps => ps.Id)
                .Select((ps, index) => (PageSection: ps, SectionNumber: index + 1))
                .SelectMany(entry => entry.PageSection.Questions
                    .Where(q => q.IsActive)
                    .Select(q => new PageSectionDto
                    {
                        PageSectionId = entry.PageSection.Id,
                        ExamPageId = p.Id,
                        QuestionId = q.Id,
                        QuestionTitle = q.QuestionTitle,
                        QuestionTypeId = entry.PageSection.QuestionTypeId,
                        QuestionTypeName = typeNamesById.GetValueOrDefault(entry.PageSection.QuestionTypeId, "—"),
                        SectionId = q.SectionId,
                        Score = q.Score,
                        IsUpToEvaluation = q.IsUpToEvaluation,
                        PageNumber = p.PageNumber,
                        SectionOrder = entry.PageSection.Order,
                        SectionNumber = entry.SectionNumber,
                        Order = q.PageOrder,
                        Instruction = p.Instruction,
                        SubInstruction = p.SubInstruction,
                    })))
            .OrderBy(d => d.PageNumber)
            .ThenBy(d => d.SectionOrder)
            .ThenBy(d => d.PageSectionId)
            .ThenBy(d => d.Order)
            .ToList();

        Pages = pageSectionDtos.GroupBy(d => d.PageNumber).OrderBy(g => g.Key).ToList();

        var placementByQuestionId = pageSectionDtos.ToDictionary(d => d.QuestionId);

        var departmentQuestions = await _context.Questions
            .AsNoTracking()
            .Where(q => q.IsActive && q.SectionId == section.Id)
            .OrderBy(q => q.DateCreated)
            .ToListAsync();

        AssignableQuestions = departmentQuestions
            .Select(q =>
            {
                placementByQuestionId.TryGetValue(q.Id, out var placement);
                return new PageSectionDto
                {
                    PageSectionId = placement?.PageSectionId ?? 0,
                    QuestionId = q.Id,
                    QuestionTitle = q.QuestionTitle,
                    QuestionTypeId = q.QuestionTypeId,
                    QuestionTypeName = typeNamesById.GetValueOrDefault(q.QuestionTypeId, "—"),
                    SectionId = q.SectionId,
                    Score = q.Score,
                    IsUpToEvaluation = q.IsUpToEvaluation,
                    PageNumber = placement?.PageNumber ?? 0,
                    SectionNumber = placement?.SectionNumber ?? 0,
                };
            })
            .ToList();

        UnassignedQuestions = AssignableQuestions.Where(q => q.PageSectionId == 0).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostBundleAsync(int questionTypeId, int pageNumber)
    {
        var effectiveSectionId = await ResolveEffectiveSectionIdAsync();
        if (effectiveSectionId is null)
        {
            return Forbid();
        }

        if (pageNumber < 1)
        {
            pageNumber = 1;
        }

        var matchingQuestions = await _context.Questions
            .Where(q => q.IsActive && q.SectionId == effectiveSectionId.Value && q.QuestionTypeId == questionTypeId)
            .OrderBy(q => q.DateCreated)
            .ToListAsync();

        if (matchingQuestions.Count == 0)
        {
            TempData["ErrorMessage"] = "No active questions of that type were found in this section.";
            return RedirectToPage(new { SectionId = effectiveSectionId });
        }

        var examPage = await _context.ExamPages
            .Include(p => p.PageSections)
            .FirstOrDefaultAsync(p => p.SectionId == effectiveSectionId.Value && p.PageNumber == pageNumber);

        if (examPage is null)
        {
            examPage = new ExamPage { SectionId = effectiveSectionId.Value, PageNumber = pageNumber };
            _context.ExamPages.Add(examPage);
        }

        var pageSection = examPage.PageSections.FirstOrDefault(ps => ps.QuestionTypeId == questionTypeId);
        if (pageSection is null)
        {
            pageSection = new PageSection
            {
                QuestionTypeId = questionTypeId,
                Order = (examPage.PageSections.Max(ps => (int?)ps.Order) ?? 0) + 1,
            };
            examPage.PageSections.Add(pageSection);
        }

        await AppendQuestionsAsync(pageSection, matchingQuestions);
        RenumberSections(examPage);

        var now = DateTime.UtcNow;
        examPage.DateUpdated = now;
        pageSection.DateUpdated = now;

        await _context.SaveChangesAsync();
        await RemoveEmptyPagesAndSectionsAsync(effectiveSectionId.Value);

        TempData["SuccessMessage"] = $"Bundled {matchingQuestions.Count} question(s) onto page {pageNumber}.";
        return RedirectToPage(new { SectionId = effectiveSectionId });
    }

    public async Task<IActionResult> OnPostAddSectionAsync(int pageNumber, int questionTypeId, List<Guid>? questionIds)
    {
        var effectiveSectionId = await ResolveEffectiveSectionIdAsync();
        if (effectiveSectionId is null)
        {
            return Forbid();
        }

        var examPage = await _context.ExamPages
            .Include(p => p.PageSections)
            .FirstOrDefaultAsync(p => p.SectionId == effectiveSectionId.Value && p.PageNumber == pageNumber);

        if (examPage is null)
        {
            TempData["ErrorMessage"] = $"Page {pageNumber} no longer exists.";
            return RedirectToPage(new { SectionId = effectiveSectionId });
        }

        if (!await _context.QuestionTypes.AnyAsync(qt => qt.Id == questionTypeId && qt.IsActive))
        {
            TempData["ErrorMessage"] = "Pick a question type for the new section.";
            return RedirectToPage(new { SectionId = effectiveSectionId });
        }

        var questions = await LoadAssignableQuestionsAsync(effectiveSectionId.Value, questionTypeId, questionIds);
        if (questions.Count == 0)
        {
            TempData["ErrorMessage"] = "Select at least one active question of the chosen type to put in the new section.";
            return RedirectToPage(new { SectionId = effectiveSectionId });
        }

        var pageSection = new PageSection { QuestionTypeId = questionTypeId };
        examPage.PageSections.Add(pageSection);

        await AppendQuestionsAsync(pageSection, questions);

        var orderedSections = examPage.PageSections
            .Where(ps => ps != pageSection)
            .OrderBy(ps => ps.Order)
            .ThenBy(ps => ps.Id)
            .ToList();
        orderedSections.Add(pageSection);
        RenumberSections(orderedSections);

        var now = DateTime.UtcNow;
        examPage.DateUpdated = now;
        pageSection.DateUpdated = now;

        await _context.SaveChangesAsync();
        await RemoveEmptyPagesAndSectionsAsync(effectiveSectionId.Value);

        TempData["SuccessMessage"] = $"Added a new section with {questions.Count} question(s) to page {pageNumber}.";
        return RedirectToPage(new { SectionId = effectiveSectionId });
    }

    public async Task<IActionResult> OnPostAssignQuestionsAsync(int pageSectionId, List<Guid>? questionIds)
    {
        var effectiveSectionId = await ResolveEffectiveSectionIdAsync();
        if (effectiveSectionId is null)
        {
            return Forbid();
        }

        var pageSection = await _context.PageSections
            .Include(ps => ps.ExamPage)
            .FirstOrDefaultAsync(ps => ps.Id == pageSectionId);

        if (pageSection is null)
        {
            return NotFound();
        }

        if (pageSection.ExamPage.SectionId != effectiveSectionId.Value)
        {
            return Forbid();
        }

        var questions = await LoadAssignableQuestionsAsync(effectiveSectionId.Value, pageSection.QuestionTypeId, questionIds);
        if (questions.Count == 0)
        {
            TempData["ErrorMessage"] = "Select at least one active question of this section's type to assign.";
            return RedirectToPage(new { SectionId = effectiveSectionId });
        }

        await AppendQuestionsAsync(pageSection, questions);

        var now = DateTime.UtcNow;
        pageSection.ExamPage.DateUpdated = now;
        pageSection.DateUpdated = now;

        await _context.SaveChangesAsync();
        await RemoveEmptyPagesAndSectionsAsync(effectiveSectionId.Value);

        TempData["SuccessMessage"] = $"Assigned {questions.Count} question(s) to the section on page {pageSection.ExamPage.PageNumber}.";
        return RedirectToPage(new { SectionId = effectiveSectionId });
    }

    public async Task<IActionResult> OnPostReorderAsync(int pageNumber, Dictionary<Guid, int> order)
    {
        var effectiveSectionId = await ResolveEffectiveSectionIdAsync();
        if (effectiveSectionId is null)
        {
            return Forbid();
        }

        var ids = order.Keys.ToList();
        var questions = await _context.Questions
            .Include(q => q.PageSection)
            .Where(q => ids.Contains(q.Id)
                && q.SectionId == effectiveSectionId.Value
                && q.PageSection != null
                && q.PageSection.ExamPage.PageNumber == pageNumber)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var question in questions)
        {
            if (order.TryGetValue(question.Id, out var newOrder))
            {
                question.PageOrder = newOrder;
                question.PageSection!.DateUpdated = now;
            }
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Order updated.";
        return RedirectToPage(new { SectionId = effectiveSectionId });
    }

    public async Task<IActionResult> OnPostSetInstructionAsync(int pageNumber, string? instruction, string? subInstruction)
    {
        var effectiveSectionId = await ResolveEffectiveSectionIdAsync();
        if (effectiveSectionId is null)
        {
            return Forbid();
        }

        var examPage = await _context.ExamPages
            .FirstOrDefaultAsync(p => p.SectionId == effectiveSectionId.Value && p.PageNumber == pageNumber);

        if (examPage is null)
        {
            TempData["ErrorMessage"] = $"Page {pageNumber} no longer exists.";
            return RedirectToPage(new { SectionId = effectiveSectionId });
        }

        examPage.Instruction = instruction;
        examPage.SubInstruction = subInstruction;
        examPage.DateUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Instructions updated for page {pageNumber}.";
        return RedirectToPage(new { SectionId = effectiveSectionId });
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid questionId)
    {
        var effectiveSectionId = await ResolveEffectiveSectionIdAsync();
        if (effectiveSectionId is null)
        {
            return Forbid();
        }

        var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId);

        if (question is null || question.PageSectionId is null)
        {
            return NotFound();
        }

        if (question.SectionId != effectiveSectionId.Value)
        {
            return Forbid();
        }

        question.PageSectionId = null;
        question.PageOrder = 0;
        await _context.SaveChangesAsync();
        await RemoveEmptyPagesAndSectionsAsync(effectiveSectionId.Value);

        TempData["SuccessMessage"] = "Question removed from its page.";
        return RedirectToPage(new { SectionId = effectiveSectionId });
    }

    private async Task<List<Question>> LoadAssignableQuestionsAsync(int departmentSectionId, int questionTypeId, List<Guid>? questionIds)
    {
        if (questionIds is null || questionIds.Count == 0)
        {
            return new List<Question>();
        }

        var requestedIds = questionIds.Distinct().ToList();
        var questions = await _context.Questions
            .Where(q => requestedIds.Contains(q.Id)
                && q.IsActive
                && q.SectionId == departmentSectionId
                && q.QuestionTypeId == questionTypeId)
            .ToListAsync();

        // Keep the order the checkboxes were submitted in.
        return questions.OrderBy(q => requestedIds.IndexOf(q.Id)).ToList();
    }

    // Appends the questions that aren't already in the section after its existing ones (moving them out of
    // wherever they were), then renumbers the section's questions 1..n.
    private async Task AppendQuestionsAsync(PageSection pageSection, IEnumerable<Question> questions)
    {
        var orderedQuestions = pageSection.Id == 0
            ? new List<Question>()
            : await _context.Questions
                .Where(q => q.PageSectionId == pageSection.Id)
                .OrderBy(q => q.PageOrder)
                .ThenBy(q => q.DateCreated)
                .ToListAsync();

        var alreadyInSection = orderedQuestions.Select(q => q.Id).ToHashSet();
        foreach (var question in questions.Where(q => !alreadyInSection.Contains(q.Id)))
        {
            question.PageSection = pageSection;
            orderedQuestions.Add(question);
        }

        for (var i = 0; i < orderedQuestions.Count; i++)
        {
            orderedQuestions[i].PageOrder = i + 1;
        }
    }

    private static void RenumberSections(ExamPage examPage) =>
        RenumberSections(examPage.PageSections.OrderBy(ps => ps.Order).ThenBy(ps => ps.Id).ToList());

    private static void RenumberSections(IList<PageSection> orderedSections)
    {
        for (var i = 0; i < orderedSections.Count; i++)
        {
            orderedSections[i].Order = i + 1;
        }
    }

    private async Task RemoveEmptyPagesAndSectionsAsync(int sectionId)
    {
        var emptySections = await _context.PageSections
            .Where(ps => ps.ExamPage.SectionId == sectionId && !ps.Questions.Any())
            .ToListAsync();
        _context.PageSections.RemoveRange(emptySections);
        await _context.SaveChangesAsync();

        var emptyPages = await _context.ExamPages
            .Where(p => p.SectionId == sectionId && !p.PageSections.Any())
            .ToListAsync();
        _context.ExamPages.RemoveRange(emptyPages);
        await _context.SaveChangesAsync();
    }

    private async Task<int?> ResolveEffectiveSectionIdAsync()
    {
        if (User.IsInRole(AppRoles.Admin))
        {
            return SectionId;
        }

        var currentUser = await _userManager.GetUserAsync(User);
        return currentUser?.SectionId;
    }
}
