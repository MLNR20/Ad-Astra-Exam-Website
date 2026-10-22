using AAExamManagementSystem.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Repository;

public class RoleRepository : GenericRepository<ApplicationRole>, IRoleRepository
{
    private readonly DbContext _context;
    private readonly ILookupNormalizer _normalizer;

    public RoleRepository(DbContext context, ILookupNormalizer normalizer) : base(context)
    {
        _context = context;
        _normalizer = normalizer;
    }

    public async Task<bool> NameExistsAsync(string name)
    {
        var normalizedName = _normalizer.NormalizeName(name);
        return await _context.Set<ApplicationRole>().AnyAsync(r => r.NormalizedName == normalizedName);
    }

    // Identity looks roles up by NormalizedName, so keep it in sync with Name on every write.
    public override async Task AddAsync(ApplicationRole entity)
    {
        entity.NormalizedName = _normalizer.NormalizeName(entity.Name);
        await base.AddAsync(entity);
    }

    public override void Update(ApplicationRole entity)
    {
        entity.NormalizedName = _normalizer.NormalizeName(entity.Name);
        base.Update(entity);
    }
}
