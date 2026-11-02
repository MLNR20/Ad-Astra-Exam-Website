using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AAExamManagementSystem.Models.Validation;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages.Courses;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly IGenericRepository<Course> _repository;
    private readonly IMapper _mapper;
    private readonly ActiveNameValidator _names;

    public EditModel(IGenericRepository<Course> repository, IMapper mapper, ActiveNameValidator names)
    {
        _repository = repository;
        _mapper = mapper;
        _names = names;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public CourseCreateUpdateDto Course { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var course = await _repository.GetByIdAsync(Id);
        if (course is null)
        {
            return NotFound();
        }

        Course = new CourseCreateUpdateDto { Name = course.Name, SchoolName = course.SchoolName, IsActive = course.IsActive };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid && Course.IsActive)
        {
            await _names.ValidateAsync(ModelState, "Course.Name", NamedEntityKind.Course, Course.Name, Id.ToString());
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var course = await _repository.GetByIdAsync(Id);
        if (course is null)
        {
            return NotFound();
        }

        course.Name = Course.Name;
        course.SchoolName = Course.SchoolName;
        course.IsActive = Course.IsActive;
        course.DateUpdated = DateTime.UtcNow;
        _repository.Update(course);
        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Course '{course.Name}' updated successfully.";
        return RedirectToPage("Index");
    }
}
