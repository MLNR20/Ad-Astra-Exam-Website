using AAExamManagementSystem.Hubs;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IMapper _mapper;

    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationService(
        ApplicationDbContext dbContext,
        IHubContext<NotificationHub> hubContext,
        IMapper mapper,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _mapper = mapper;
        _userManager = userManager;
    }

    /// <summary>
    /// Sends a notification to every editor assigned to the given department (section) and returns how many
    /// editors were notified.
    /// </summary>
    public async Task<int> SendToDepartmentEditorsAsync(string sectionName, string title, string message, string? url = null)
    {
        var section = await _dbContext.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.Name == sectionName);
        if (section is null)
        {
            return 0;
        }

        var editors = (await _userManager.GetUsersInRoleAsync(AAExamManagementSystem.Models.Entities.Roles.Editor))
            .Where(u => u.SectionId == section.Id)
            .ToList();

        foreach (var editor in editors)
        {
            await SendAsync(editor.Id, title, message, url);
        }

        return editors.Count;
    }

    public async Task<NotificationDto> SendAsync(string userId, string title, string message, string? url = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Url = url
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        var dto = _mapper.Map<NotificationDto>(notification);
        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", dto);

        return dto;
    }

    public async Task<List<NotificationDto>> GetForUserAsync(string userId, bool unreadOnly = false)
    {
        var query = _dbContext.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query
            .OrderByDescending(n => n.DateCreated)
            .ToListAsync();

        return _mapper.Map<List<NotificationDto>>(notifications);
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _dbContext.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(string userId, Guid notificationId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification is null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.DateUpdated = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var now = DateTime.UtcNow;
        await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.DateUpdated, now));
    }
}
