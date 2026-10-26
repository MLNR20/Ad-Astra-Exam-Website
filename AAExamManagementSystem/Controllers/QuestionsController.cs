using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAExamManagementSystem.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Editor}")]
public class QuestionsController : ControllerBase
{
    private readonly IQuestionService _questionService;

    public QuestionsController(IQuestionService questionService)
    {
        _questionService = questionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuestionDto>>> GetAll()
    {
        return Ok(await _questionService.GetAllAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuestionDto>> GetById(Guid id)
    {
        var question = await _questionService.GetByIdAsync(id);
        if (question is null) return NotFound();
        return Ok(question);
    }

    [HttpPost]
    public async Task<ActionResult<QuestionDto>> Create(QuestionCreateUpdateDto dto)
    {
        var result = await _questionService.CreateAsync(dto);
        if (result.Status == ServiceStatus.ValidationFailed) return ValidationProblem(result.ValidationErrors!);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id, version = "1.0" }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, QuestionCreateUpdateDto dto)
    {
        var result = await _questionService.UpdateAsync(id, dto);
        return result.Status switch
        {
            ServiceStatus.NotFound => NotFound(),
            ServiceStatus.ValidationFailed => ValidationProblem(result.ValidationErrors!),
            _ => NoContent()
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _questionService.DeleteAsync(id);
        return result.Succeeded ? NoContent() : NotFound();
    }
}
