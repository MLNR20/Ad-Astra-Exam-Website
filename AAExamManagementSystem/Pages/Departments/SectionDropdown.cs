namespace AAExamManagementSystem.Pages.Departments;

/// <summary>A section a department can hold. Only sections free or already in the department are offered.</summary>
public record SectionOption(int Id, string Name, bool Selected);

public record SectionDropdownModel(IList<SectionOption> Options, string IdPrefix);
