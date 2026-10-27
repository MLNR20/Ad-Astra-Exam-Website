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
    private const string DefaultHeading = "Multiple Choice Questions";
    private const string DefaultSubheading = "Answer every question below, then submit to see your score.";

    private readonly IGenericRepository<QuestionType> _questionTypeRepository;
    private readonly IGenericRepository<Section> _sectionRepository;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly QuestionChoiceService _choiceService;
    private readonly IMapper _mapper;

    public SimulateAllModel(
        IGenericRepository<QuestionType> questionTypeRepository,
        IGenericRepository<Section> sectionRepository,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        QuestionChoiceService choiceService,
        IMapper mapper)
    {
        _questionTypeRepository = questionTypeRepository;
        _sectionRepository = sectionRepository;
        _context = context;
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

    // The department whose banner the page shows: the exam's own, or General Knowledge on its closing pages.
    public string StageHeroDepartment { get; set; } = string.Empty;

    public string StageHeading { get; set; } = DefaultHeading;

    public string StageSubheading { get; set; } = DefaultSubheading;

    public IList<QuestionDto> Questions { get; set; } = new List<QuestionDto>();

    // Keyed by the first question of each section that has its own instruction text.
    public IDictionary<Guid, SectionHeadingDto> SectionHeadings { get; set; } = new Dictionary<Guid, SectionHeadingDto>();

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

        // The same stages the applicant's exam walks through: the department's pages, then General Knowledge.
        var stages = (await new ExamStageBuilder(_context).BuildAsync(section.Name)).ToList();
        if (stages.Count == 0)
        {
            stages.Add(new ExamStage(
                section.Name,
                $"This section aims to gauge your familiarity in {section.Name} concepts.",
                section.Name,
                null,
                null,
                new List<Question>(),
                new Dictionary<Guid, SectionHeadingDto>()));
        }

        TotalPages = stages.Count;
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        var currentStage = stages[PageNumber - 1];

        StageTitle = currentStage.Title;
        StageSubtitle = currentStage.Subtitle;
        StageHeroDepartment = currentStage.HeroDepartment;
        StageHeading = string.IsNullOrWhiteSpace(currentStage.Heading) ? DefaultHeading : currentStage.Heading;
        StageSubheading = string.IsNullOrWhiteSpace(currentStage.Subheading) ? DefaultSubheading : currentStage.Subheading;
        Questions = _mapper.Map<IList<QuestionDto>>(currentStage.Questions.ToList());
        SectionHeadings = currentStage.SectionHeadings;
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
