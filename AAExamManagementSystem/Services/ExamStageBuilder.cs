using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>One screen of an exam: a department exam page, a General Knowledge page, or a fallback group of questions.</summary>
/// <param name="Title">Hero title, the department name (or the SQL / debugging group).</param>
/// <param name="HeroDepartment">Department whose banner image the stage shows.</param>
/// <param name="Heading">The page's own instruction, or null to use the screen's default heading.</param>
/// <param name="SectionHeadings">Section instructions, keyed by the first question of each section.</param>
public sealed record ExamStage(
    string Title,
    string Subtitle,
    string HeroDepartment,
    string? Heading,
    string? Subheading,
    IList<Question> Questions,
    IDictionary<Guid, SectionHeadingDto> SectionHeadings);

/// <summary>
/// Lays an exam out as ordered pages, the way both the applicant's exam and the editor's preview show it: the
/// department's pages first, then General Knowledge, with each page's sections and questions in their set order.
/// </summary>
public class ExamStageBuilder
{
    private const string SqlSchemaImagePrefix = "/images/sql-schema-";
    private const string DebugImagePrefix = "/images/debug-";
    private const string GeneralKnowledgeSubtitle = "Wrap up with a few general knowledge questions about Ad Astra.";
    private const string DefaultAnswerSubheading = "Answer every question below.";

    private readonly ApplicationDbContext _context;

    public ExamStageBuilder(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IList<ExamStage>> BuildAsync(string departmentName)
    {
        var department = await _context.Sections
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsActive && s.Name == departmentName);
        if (department is null)
        {
            return new List<ExamStage>();
        }

        var answerableTypeIds = await _context.QuestionTypes
            .AsNoTracking()
            .Where(qt => qt.Name == QuestionTypes.MultipleChoice || qt.Name == QuestionTypes.Identification)
            .Select(qt => qt.Id)
            .ToListAsync();

        var stages = new List<ExamStage>();
        stages.AddRange(await BuildDepartmentStagesAsync(department, answerableTypeIds));

        if (department.Name != Departments.GeneralKnowledge)
        {
            var generalKnowledge = await _context.Sections
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IsActive && s.Name == Departments.GeneralKnowledge);
            if (generalKnowledge is not null)
            {
                stages.AddRange(await BuildGeneralKnowledgeStagesAsync(generalKnowledge, answerableTypeIds));
            }
        }

        return stages;
    }

    private async Task<IList<ExamStage>> BuildDepartmentStagesAsync(Section department, IList<int> answerableTypeIds)
    {
        var subtitle = $"This section aims to gauge your familiarity in {department.Name} concepts.";
        var pagedStages = await BuildPagedStagesAsync(department, department.Name, subtitle);
        if (pagedStages.Count > 0)
        {
            return pagedStages;
        }

        // No exam pages have been set up for this department yet: one group of questions, plus a separate
        // group for the SQL schema and debugging questions, which need their own wide layout.
        var questions = await LoadFallbackQuestionsAsync(department, answerableTypeIds);
        var sqlQuestions = questions.Where(q => HasImagePrefix(q, SqlSchemaImagePrefix)).ToList();
        var debugQuestions = questions.Where(q => HasImagePrefix(q, DebugImagePrefix)).ToList();
        var mainQuestions = questions.Except(sqlQuestions).Except(debugQuestions).ToList();

        var stages = new List<ExamStage>();
        if (mainQuestions.Count > 0)
        {
            stages.Add(new ExamStage(department.Name, subtitle, department.Name, null, null, mainQuestions, NoHeadings()));
        }

        var secondPageQuestions = sqlQuestions.Concat(debugQuestions).ToList();
        if (secondPageQuestions.Count > 0)
        {
            var hasSql = sqlQuestions.Count > 0;
            var hasDebug = debugQuestions.Count > 0;
            stages.Add(new ExamStage(
                hasSql && hasDebug ? "SQL Queries and Debugging" : hasSql ? "SQL Queries" : "Debugging",
                hasSql && hasDebug
                    ? "Write the SQL query for each schema shown, then find the errors in each code sample."
                    : hasSql
                        ? "Write the SQL query that answers each question about the schema shown."
                        : "Find the errors in each code sample.",
                department.Name,
                hasSql && hasDebug ? "SQL Query and Debugging Questions" : hasSql ? "SQL Query Questions" : "Debugging Questions",
                DefaultAnswerSubheading,
                secondPageQuestions,
                NoHeadings()));
        }

        return stages;
    }

    private async Task<IList<ExamStage>> BuildGeneralKnowledgeStagesAsync(Section generalKnowledge, IList<int> answerableTypeIds)
    {
        var pagedStages = await BuildPagedStagesAsync(generalKnowledge, Departments.GeneralKnowledge, GeneralKnowledgeSubtitle);
        if (pagedStages.Count > 0)
        {
            return pagedStages;
        }

        var questions = await LoadFallbackQuestionsAsync(generalKnowledge, answerableTypeIds);
        return questions.Count == 0
            ? new List<ExamStage>()
            : new List<ExamStage>
            {
                new(Departments.GeneralKnowledge, GeneralKnowledgeSubtitle, Departments.GeneralKnowledge, null, null, questions, NoHeadings()),
            };
    }

    // One stage per exam page, with the page's sections in order and each section's questions in order.
    private async Task<IList<ExamStage>> BuildPagedStagesAsync(Section department, string title, string subtitle)
    {
        var examPages = await _context.ExamPages
            .AsNoTracking()
            .Include(p => p.PageSections)
            .ThenInclude(ps => ps.Questions)
            .Where(p => p.IsActive && p.SectionId == department.Id)
            .OrderBy(p => p.PageNumber)
            .ToListAsync();

        var stages = new List<ExamStage>();
        foreach (var page in examPages)
        {
            var pageQuestions = new List<Question>();
            var headings = new Dictionary<Guid, SectionHeadingDto>();

            foreach (var pageSection in page.PageSections
                         .Where(ps => ps.IsActive)
                         .OrderBy(ps => ps.Order)
                         .ThenBy(ps => ps.Id))
            {
                var sectionQuestions = pageSection.Questions
                    .Where(q => q.IsActive)
                    .OrderBy(q => q.PageOrder)
                    .ThenBy(q => q.DateCreated)
                    .ToList();

                if (sectionQuestions.Count == 0)
                {
                    continue;
                }

                // The section's instruction is shown just above its first question.
                if (!string.IsNullOrWhiteSpace(pageSection.Instruction) || !string.IsNullOrWhiteSpace(pageSection.SubInstruction))
                {
                    headings[sectionQuestions[0].Id] = new SectionHeadingDto(pageSection.Instruction, pageSection.SubInstruction);
                }

                pageQuestions.AddRange(sectionQuestions);
            }

            if (pageQuestions.Count > 0)
            {
                stages.Add(new ExamStage(
                    title,
                    subtitle,
                    department.Name,
                    NullIfBlank(page.Instruction),
                    NullIfBlank(page.SubInstruction),
                    pageQuestions,
                    headings));
            }
        }

        return stages;
    }

    // Unpaged essays are left out: an essay only appears once an exam page places it.
    private async Task<List<Question>> LoadFallbackQuestionsAsync(Section department, IList<int> answerableTypeIds) =>
        await _context.Questions
            .AsNoTracking()
            .Where(q => q.IsActive && q.SectionId == department.Id && answerableTypeIds.Contains(q.QuestionTypeId))
            .OrderBy(q => q.DateCreated)
            .ToListAsync();

    private static bool HasImagePrefix(Question question, string prefix) =>
        question.Image is not null && question.Image.StartsWith(prefix);

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static Dictionary<Guid, SectionHeadingDto> NoHeadings() => new();
}
