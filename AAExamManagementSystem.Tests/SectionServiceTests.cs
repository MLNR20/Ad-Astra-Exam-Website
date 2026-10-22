using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Moq;

namespace AAExamManagementSystem.Tests;

public class SectionServiceTests
{
    private static (SectionService Service, Mock<IUnitOfWork> UnitOfWork, Mock<IGenericRepository<Section>> Sections) CreateService()
    {
        var sections = new Mock<IGenericRepository<Section>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Sections).Returns(sections.Object);
        return (new SectionService(unitOfWork.Object, CreateMapper()), unitOfWork, sections);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(
            cfg =>
            {
                cfg.CreateMap<Section, SectionDto>();
                cfg.CreateMap<SectionCreateUpdateDto, Section>();
            },
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    [Fact]
    public async Task GetAll_ReturnsAllSections()
    {
        var (service, _, sections) = CreateService();
        sections.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
        {
            new Section { Id = 1, Name = "Section A" },
            new Section { Id = 2, Name = "Section B" },
        });

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetById_WithExistingId_ReturnsSection()
    {
        var (service, _, sections) = CreateService();
        sections.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Section { Id = 1, Name = "Section A" });

        var result = await service.GetByIdAsync(1);

        Assert.Equal("Section A", result!.Name);
    }

    [Fact]
    public async Task GetById_WithMissingId_ReturnsNull()
    {
        var (service, _, sections) = CreateService();
        sections.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Section?)null);

        Assert.Null(await service.GetByIdAsync(99));
    }

    [Fact]
    public async Task Create_AddsSectionAndSaves()
    {
        var (service, unitOfWork, sections) = CreateService();

        var result = await service.CreateAsync(new SectionCreateUpdateDto { Name = "New Section" });

        Assert.Equal("New Section", result.Name);
        sections.Verify(r => r.AddAsync(It.Is<Section>(s => s.Name == "New Section")), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithExistingId_UpdatesSection()
    {
        var (service, unitOfWork, sections) = CreateService();
        var section = new Section { Id = 1, Name = "Old Name" };
        sections.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(section);

        var result = await service.UpdateAsync(1, new SectionCreateUpdateDto { Name = "New Name" });

        Assert.True(result.Succeeded);
        Assert.Equal("New Name", section.Name);
        sections.Verify(r => r.Update(section), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, sections) = CreateService();
        sections.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Section?)null);

        var result = await service.UpdateAsync(99, new SectionCreateUpdateDto { Name = "New Name" });

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Delete_WithExistingId_SoftDeletesSection()
    {
        var (service, unitOfWork, sections) = CreateService();
        var section = new Section { Id = 1, Name = "Section A", IsActive = true };
        sections.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(section);

        var result = await service.DeleteAsync(1);

        Assert.True(result.Succeeded);
        Assert.False(section.IsActive);
        sections.Verify(r => r.Update(section), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, sections) = CreateService();
        sections.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Section?)null);

        var result = await service.DeleteAsync(99);

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
