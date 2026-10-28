using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IRecruitmentService
{
    Task<IReadOnlyList<JobOfferDto>> ListOffersAsync(CancellationToken ct);
    Task<JobOfferDto> CreateOfferAsync(UpsertJobOfferRequest request, CancellationToken ct);
    Task<JobOfferDto> UpdateOfferAsync(Guid id, UpsertJobOfferRequest request, CancellationToken ct);
    Task<IReadOnlyList<CandidateDto>> ListCandidatesAsync(Guid offerId, CancellationToken ct);
    Task<CandidateDto> AddCandidateAsync(Guid offerId, CreateCandidateRequest request, Stream? cv, string? fileName, CancellationToken ct);
    Task<CandidateDto> SetStatusAsync(Guid candidateId, CandidateStatus status, CancellationToken ct);
}

public sealed class RecruitmentService(
    AppDbContext db,
    ICurrentUser current,
    IFileStorage storage,
    INotificationService notifications,
    IAuditWriter audit) : IRecruitmentService
{
    public async Task<IReadOnlyList<JobOfferDto>> ListOffersAsync(CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageRecruitment(current.Roles));
        var offers = await db.JobOffers.AsNoTracking().Include(offer => offer.Position).Include(offer => offer.Candidates)
            .OrderByDescending(offer => offer.CreatedAt).ToListAsync(ct);
        return offers.Select(ToDto).ToArray();
    }

    public async Task<JobOfferDto> CreateOfferAsync(UpsertJobOfferRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageRecruitment(current.Roles));
        await EnsurePositionAsync(request.PositionId, ct);
        var offer = new JobOffer
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Requirements = request.Requirements.Trim(),
            PositionId = request.PositionId,
            Status = request.Status,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.JobOffers.Add(offer);
        audit.Write("offer.create", "JobOffer", offer.Id.ToString(), offer.Title);
        await db.SaveChangesAsync(ct);
        offer.Position = request.PositionId is null ? null : await db.Positions.FindAsync([request.PositionId], ct);
        return ToDto(offer);
    }

    public async Task<JobOfferDto> UpdateOfferAsync(Guid id, UpsertJobOfferRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageRecruitment(current.Roles));
        var offer = await db.JobOffers.Include(item => item.Position).Include(item => item.Candidates).FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Offre introuvable.");
        await EnsurePositionAsync(request.PositionId, ct);
        offer.Title = request.Title.Trim();
        offer.Description = request.Description.Trim();
        offer.Requirements = request.Requirements.Trim();
        offer.PositionId = request.PositionId;
        offer.Status = request.Status;
        audit.Write("offer.update", "JobOffer", offer.Id.ToString(), offer.Status.ToString());
        await db.SaveChangesAsync(ct);
        return ToDto(offer);
    }

    public async Task<IReadOnlyList<CandidateDto>> ListCandidatesAsync(Guid offerId, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageRecruitment(current.Roles));
        if (!await db.JobOffers.AnyAsync(offer => offer.Id == offerId, ct))
            throw new NotFoundException("Offre introuvable.");
        return await db.Candidates.AsNoTracking()
            .Where(candidate => candidate.JobOfferId == offerId)
            .OrderByDescending(candidate => candidate.AppliedAt)
            .Select(candidate => new CandidateDto(
                candidate.Id,
                candidate.JobOfferId,
                candidate.FirstName,
                candidate.LastName,
                candidate.Email,
                candidate.CvOriginalFileName,
                candidate.Status,
                candidate.AppliedAt))
            .ToListAsync(ct);
    }

    public async Task<CandidateDto> AddCandidateAsync(Guid offerId, CreateCandidateRequest request, Stream? cv, string? fileName, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageRecruitment(current.Roles));
        var offer = await db.JobOffers.FirstOrDefaultAsync(item => item.Id == offerId, ct)
            ?? throw new NotFoundException("Offre introuvable.");
        if (offer.Status == JobOfferStatus.Closed)
            throw new BusinessRuleException("Cette offre est clôturée.");

        string? stored = null;
        string? original = null;
        if (cv is not null && !string.IsNullOrWhiteSpace(fileName))
        {
            var saved = await storage.SaveAsync(cv, fileName, "cv", ct);
            stored = saved.StoredFileName;
            original = Path.GetFileName(fileName);
        }

        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            JobOfferId = offer.Id,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CvStoredFileName = stored,
            CvOriginalFileName = original,
            Status = CandidateStatus.Received,
            AppliedAt = DateTimeOffset.UtcNow
        };
        db.Candidates.Add(candidate);
        audit.Write("candidate.create", "Candidate", candidate.Id.ToString(), candidate.Email);
        await db.SaveChangesAsync(ct);

        var body = $"{candidate.FirstName} {candidate.LastName} — offre « {offer.Title} ».";
        var rhIds = await db.Users.AsNoTracking()
            .Where(user => user.Role == AppRoles.Rh && user.IsActive && user.Id != current.UserId)
            .Select(user => user.Id)
            .ToListAsync(ct);
        foreach (var rhId in rhIds)
            await notifications.NotifyAsync(rhId, $"candidate:{candidate.Id}:{rhId}", "Nouvelle candidature", body, ct);

        return ToDto(candidate);
    }

    public async Task<CandidateDto> SetStatusAsync(Guid candidateId, CandidateStatus status, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageRecruitment(current.Roles));
        var candidate = await db.Candidates.FirstOrDefaultAsync(item => item.Id == candidateId, ct)
            ?? throw new NotFoundException("Candidature introuvable.");
        candidate.Status = status;
        audit.Write("candidate.status", "Candidate", candidate.Id.ToString(), status.ToString());
        await db.SaveChangesAsync(ct);
        return ToDto(candidate);
    }

    private async Task EnsurePositionAsync(Guid? positionId, CancellationToken ct)
    {
        if (positionId is Guid id && !await db.Positions.AnyAsync(position => position.Id == id, ct))
            throw new NotFoundException("Poste introuvable.");
    }

    private static JobOfferDto ToDto(JobOffer offer) => new(
        offer.Id,
        offer.Title,
        offer.Description,
        offer.Requirements,
        offer.PositionId,
        offer.Position?.Title,
        offer.Status,
        offer.CreatedAt,
        offer.Candidates.Count);

    private static CandidateDto ToDto(Candidate candidate) => new(
        candidate.Id,
        candidate.JobOfferId,
        candidate.FirstName,
        candidate.LastName,
        candidate.Email,
        candidate.CvOriginalFileName,
        candidate.Status,
        candidate.AppliedAt);
}
