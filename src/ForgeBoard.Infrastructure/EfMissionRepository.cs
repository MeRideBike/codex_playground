using ForgeBoard.Application;
using ForgeBoard.Domain;
using Microsoft.EntityFrameworkCore;

namespace ForgeBoard.Infrastructure;

public sealed class EfMissionRepository(ForgeBoardDbContext dbContext) : IMissionRepository
{
    public Task AddAsync(Mission mission, CancellationToken cancellationToken) => dbContext.Missions.AddAsync(mission, cancellationToken).AsTask();

    public Task AddApprovalAsync(ApprovalRecord approval, CancellationToken cancellationToken) => dbContext.ApprovalRecords.AddAsync(approval, cancellationToken).AsTask();

    public Task AddStepAsync(RunStep runStep, CancellationToken cancellationToken) => dbContext.RunSteps.AddAsync(runStep, cancellationToken).AsTask();

    public Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken) => dbContext.Missions
        .Include(mission => mission.Steps).Include(mission => mission.Approvals)
        .SingleOrDefaultAsync(mission => mission.Id == missionId, cancellationToken);

    public async Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken)
    {
        var missions = await dbContext.Missions
            .Include(mission => mission.Steps).Include(mission => mission.Approvals)
            .ToListAsync(cancellationToken);
        return missions.OrderByDescending(mission => mission.CreatedAt).ToArray();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
