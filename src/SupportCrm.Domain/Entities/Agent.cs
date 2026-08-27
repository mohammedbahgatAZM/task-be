namespace SupportCrm.Domain.Entities;

// Stand-in for a real user/identity record — no authentication/user-management system
// exists yet in this codebase. Replace with a real user reference once one does.
public class Agent
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsAvailable { get; private set; } = true;
    public bool CanViewSensitiveData { get; private set; } // defaults false — masking is observable without an explicit grant
    public bool IsSupervisor { get; private set; }
    public bool IsKnowledgeBaseEditor { get; private set; }
    public string PreferredLanguage { get; private set; } = "en"; // "en" | "ar"
    public Guid? DepartmentId { get; private set; }
    public Guid? BranchId { get; private set; }

    private Agent() { } // EF Core

    public Agent(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name is required.", nameof(name));
        Id = Guid.NewGuid();
        Name = name;
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;

    public void SetSensitiveDataAccess(bool canView) => CanViewSensitiveData = canView;

    public void SetSupervisor(bool isSupervisor) => IsSupervisor = isSupervisor;

    public void SetKnowledgeBaseEditor(bool isEditor) => IsKnowledgeBaseEditor = isEditor;

    public void SetPreferredLanguage(string language) => PreferredLanguage = language is "en" or "ar" ? language : "en";

    public void SetDepartment(Guid? departmentId) => DepartmentId = departmentId;

    public void SetBranch(Guid? branchId) => BranchId = branchId;
}
