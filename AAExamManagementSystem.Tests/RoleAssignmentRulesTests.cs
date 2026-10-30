using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Services;

namespace AAExamManagementSystem.Tests;

public class RoleAssignmentRulesTests
{
    [Theory]
    [InlineData(Roles.Editor)]
    [InlineData(Roles.OIC)]
    public void DepartmentRole_WithoutDepartment_IsRejected(string role)
    {
        var error = RoleAssignmentRules.Validate("jdoe", null, new[] { role });

        Assert.NotNull(error);
        Assert.Contains(role, error);
    }

    [Theory]
    [InlineData(Roles.Editor)]
    [InlineData(Roles.OIC)]
    public void DepartmentRole_WithDepartment_IsAllowed(string role)
    {
        Assert.Null(RoleAssignmentRules.Validate("jdoe", 3, new[] { role }));
    }

    [Theory]
    [InlineData(Roles.Staffer)]
    [InlineData(Roles.Instructor)]
    [InlineData(Roles.Admin)]
    public void OtherRoles_DoNotNeedDepartment(string role)
    {
        Assert.Null(RoleAssignmentRules.Validate("jdoe", null, new[] { role }));
    }

    [Fact]
    public void MixedRoles_WithoutDepartment_AreRejectedWhenAnyIsDepartmentRole()
    {
        Assert.NotNull(RoleAssignmentRules.Validate("jdoe", null, new[] { Roles.Staffer, Roles.Editor }));
    }
}
