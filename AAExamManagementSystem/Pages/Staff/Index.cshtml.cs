using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages.Staff;

[Authorize(Roles = AppRoles.Editor)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGenericRepository<Section> _sectionRepository;
    private readonly IMapper _mapper;

    public IndexModel(UserManager<ApplicationUser> userManager, IGenericRepository<Section> sectionRepository, IMapper mapper)
    {
        _userManager = userManager;
        _sectionRepository = sectionRepository;
        _mapper = mapper;
    }

    public IList<UserDto> Staff { get; set; } = new List<UserDto>();
    public string? DepartmentName { get; set; }
    public string? CurrentUserId { get; set; }

    public async Task OnGetAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        CurrentUserId = currentUser?.Id;
        if (currentUser?.SectionId is null)
        {
            return;
        }

        var sections = await _sectionRepository.GetAllAsync();
        DepartmentName = sections.FirstOrDefault(s => s.Id == currentUser.SectionId)?.Name;

        var departmentUsers = _userManager.Users
            .Where(u => u.SectionId == currentUser.SectionId)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToList();

        var staff = new List<UserDto>();
        foreach (var user in departmentUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(AppRoles.Applicant))
            {
                continue;
            }

            var dto = _mapper.Map<UserDto>(user);
            dto.Role = roles.Count > 0 ? string.Join(", ", roles) : "-";
            dto.IsActive = !(user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow);
            dto.SectionName = DepartmentName ?? "—";
            staff.Add(dto);
        }

        Staff = staff;
    }
}
