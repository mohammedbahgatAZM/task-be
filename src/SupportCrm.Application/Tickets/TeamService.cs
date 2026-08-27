namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TeamService(ITeamRepository repository)
{
    public async Task<TeamDto> CreateAsync(CreateTeamRequest request, CancellationToken ct)
    {
        var team = new Team(request.Name.Trim());
        await repository.AddAsync(team, ct);
        await repository.SaveChangesAsync(ct);
        return new TeamDto(team.Id, team.Name);
    }

    public async Task<IReadOnlyList<TeamDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(t => new TeamDto(t.Id, t.Name, t.DepartmentId)).ToList();

    public async Task SetDepartmentAsync(Guid teamId, Guid? departmentId, CancellationToken ct)
    {
        var team = await repository.GetByIdAsync(teamId, ct) ?? throw new KeyNotFoundException($"Team '{teamId}' was not found.");
        team.SetDepartment(departmentId);
        await repository.SaveChangesAsync(ct);
    }
}
