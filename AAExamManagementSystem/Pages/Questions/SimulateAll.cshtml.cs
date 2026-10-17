using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AppDepartments = AAExamManagementSystem.Models.Entities.Departments;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages.Questions;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Editor}")]
public class SimulateAllModel : PageModel
{
    private const string SqlSchemaImagePrefix = "/images/sql-schema-";
    private const string DefaultHeading = "Multiple Choice Questions";
    private const string DefaultSubheading = "Answer every question below, then submit to see your score.";

    private readonly IGenericRepository<Question> _repository;
    private readonly IGenericRepository<QuestionType> _questionTypeRepository;
    private readonly IGenericRepository<Section> _sectionRepository;
    private readonly IGenericRepository<ExamPage> _examPageRepository;
    private readonly IGenericRepository<PageSection> _pageSectionRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly QuestionChoiceService _choiceService;
    private readonly IMapper _mapper;

    private sealed record ExamStage(string Title, string Subtitle, string Heading, string Subheading, IList<QuestionDto> Questions);

    public SimulateAllModel(
        IGenericRepository<Question> repository,
        IGenericRepository<QuestionType> questionTypeRepository,
        IGenericRepository<Section> sectionRepository,
        IGenericRepository<ExamPage> examPageRepository,
        IGenericRepository<PageSection> pageSectionRepository,
        UserManager<ApplicationUser> userManager,
        QuestionChoiceService choiceService,
        IMapper mapper)
    {
        _repository = repository;
        _questionTypeRepository = questionTypeRepository;
        _sectionRepository = sectionRepository;
        _examPageRepository = examPageRepository;
        _pageSectionRepository = pageSectionRepository;
        _userManager = userManager;
        _choiceService = choiceService;
        _mapper = mapper;
    }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int? SectionId { get; set; }

    public bool IsAdmin { get; set; }

    public bool RequiresSectionSelection { get; set; }

    public bool NoDepartmentAssigned { get; set; }

    public IList<Section> SelectableSections { get; set; } = new List<Section>();

    public string CurrentSectionName { get; set; } = string.Empty;

    public string StageTitle { get; set; } = string.Empty;

    public string StageSubtitle { get; set; } = string.Empty;

    public string StageHeading { get; set; } = DefaultHeading;

    public string StageSubheading { get; set; } = DefaultSubheading;

    public IList<QuestionDto> Questions { get; set; } = new List<QuestionDto>();

    public int TotalPages { get; set; }

    public bool HasNextPage { get; set; }

    public bool HasPreviousPage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        IsAdmin = User.IsInRole(AppRoles.Admin);

        var allSections = (await _sectionRepository.GetAllAsync()).ToList();
        var selectableSections = allSections
            .Where(s => s.IsActive && s.Name != AppDepartments.GeneralKnowledge)
            .OrderBy(s => s.Name)
            .ToList();

        int? effectiveSectionId;

        if (IsAdmin)
        {
            SelectableSections = selectableSections;

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

        var section = allSections.FirstOrDefault(s => s.Id == effectiveSectionId && s.IsActive);
        if (section is null)
        {
            NoDepartmentAssigned = true;
            return Page();
        }

        CurrentSectionName = section.Name;
        SectionId = section.Id;

        var questionTypes = await _questionTypeRepository.GetAllAsync();
        var typeNamesById = questionTypes.ToDictionary(qt => qt.Id, qt => qt.Name);
        var simulatedTypeIds = questionTypes
            .Where(qt => qt.Name == QuestionTypes.MultipleChoice || qt.Name == QuestionTypes.Identification)
            .Select(qt => qt.Id)
            .ToHashSet();

        var simulatableQuestions = (await _repository.GetAllAsync())
            .Where(q => q.IsActive && simulatedTypeIds.Contains(q.QuestionTypeId))
            .ToList();

        var examPages = (await _examPageRepository.GetAllAsync())
            .Where(p => p.IsActive)
            .ToList();
        var activePageSections = (await _pageSectionRepository.GetAllAsync())
            .Where(ps => ps.IsActive)
            .ToList();
        var questionsByPageSectionId = simulatableQuestions
            .Where(q => q.PageSectionId.HasValue)
            .ToLookup(q => q.PageSectionId!.Value);

        // Mirrors the applicant's Take page: one stage per exam page, with the page's sections in order and each
        // section's questions in order.
        List<(ExamPage Page, List<Question> Questions)> GetPagedQuestions(Section department) =>
            examPages
                .Where(p => p.SectionId == department.Id)
                .OrderBy(p => p.PageNumber)
                .Select(p => (
                    Page: p,
                    Questions: activePageSections
                        .Where(ps => ps.ExamPageId == p.Id)
                        .OrderBy(ps => ps.Order)
                        .ThenBy(ps => ps.Id)
                        .SelectMany(ps => questionsByPageSectionId[ps.Id]
                            .OrderBy(q => q.PageOrder)
                            .ThenBy(q => q.DateCreated))
                        .ToList()))
                .Where(p => p.Questions.Count > 0)
                .ToList();

        IList<QuestionDto> ToDtos(IEnumerable<Question> questions) =>
            _mapper.Map<IList<QuestionDto>>(questions.ToList());

        static string TextOrDefault(string? text, string fallback) =>
            string.IsNullOrWhiteSpace(text) ? fallback : text;

        var stages = new List<ExamStage>();

        var departmentSubtitle = $"This section aims to gauge your familiarity in {section.Name} concepts.";
        var departmentPages = GetPagedQuestions(section);

        if (departmentPages.Count > 0)
        {
            foreach (var (page, pageQuestions) in departmentPages)
            {
                stages.Add(new ExamStage(
                    section.Name,
                    departmentSubtitle,
                    TextOrDefault(page.Instruction, DefaultHeading),
                    TextOrDefault(page.SubInstruction, DefaultSubheading),
                    ToDtos(pageQuestions)));
            }
        }
        else
        {
            // No exam pages have been set up for this department yet: fall back to a single stage, plus a
            // separate stage for the SQL schema questions.
            var departmentQuestions = ToDtos(simulatableQuestions
                .Where(q => q.SectionId == section.Id)
                .OrderBy(q => q.DateCreated));
            var sqlQuestions = departmentQuestions
                .Where(q => q.Image != null && q.Image.StartsWith(SqlSchemaImagePrefix))
                .ToList();
            var departmentMainQuestions = departmentQuestions.Except(sqlQuestions).ToList();

            stages.Add(new ExamStage(section.Name, departmentSubtitle, DefaultHeading, DefaultSubheading, departmentMainQuestions));

            if (sqlQuestions.Count > 0)
            {
                stages.Add(new ExamStage(
                    "SQL Queries",
                    "Write the SQL query that answers each question about the schema shown.",
                    "SQL Query Questions",
                    DefaultSubheading,
                    sqlQuestions));
            }
        }

        var generalKnowledgeSection = allSections.FirstOrDefault(s => s.Name == AppDepartments.GeneralKnowledge);
        if (generalKnowledgeSection is not null && generalKnowledgeSection.Id != section.Id)
        {
            const string generalKnowledgeSubtitle = "Wrap up with a few general knowledge questions about Ad Astra.";
            var generalKnowledgePages = GetPagedQuestions(generalKnowledgeSection);

            if (generalKnowledgePages.Count > 0)
            {
                foreach (var (page, pageQuestions) in generalKnowledgePages)
                {
                    stages.Add(new ExamStage(
                        AppDepartments.GeneralKnowledge,
                        generalKnowledgeSubtitle,
                        TextOrDefault(page.Instruction, DefaultHeading),
                        TextOrDefault(page.SubInstruction, DefaultSubheading),
                        ToDtos(pageQuestions)));
                }
            }
            else
            {
                var generalKnowledgeQuestions = ToDtos(simulatableQuestions
                    .Where(q => q.SectionId == generalKnowledgeSection.Id)
                    .OrderBy(q => q.DateCreated));

                if (generalKnowledgeQuestions.Count > 0)
                {
                    stages.Add(new ExamStage(
                        AppDepartments.GeneralKnowledge,
                        generalKnowledgeSubtitle,
                        DefaultHeading,
                        DefaultSubheading,
                        generalKnowledgeQuestions));
                }
            }
        }

        TotalPages = stages.Count;
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        var currentStage = stages[PageNumber - 1];

        StageTitle = currentStage.Title;
        StageSubtitle = currentStage.Subtitle;
        StageHeading = currentStage.Heading;
        StageSubheading = currentStage.Subheading;
        Questions = currentStage.Questions;
        HasNextPage = PageNumber < TotalPages;
        HasPreviousPage = PageNumber > 1;

        foreach (var question in Questions)
        {
            question.QuestionTypeName = typeNamesById.GetValueOrDefault(question.QuestionTypeId, string.Empty);
            question.Choices = await _choiceService.GetChoicesAsync(question.Id);
        }

        return Page();
    }
}
