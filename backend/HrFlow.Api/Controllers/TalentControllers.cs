using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recruitment/offers")]
public class RecruitmentController(IRecruitmentService recruitment) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobOfferDto>>> List(CancellationToken ct) =>
        Ok(await recruitment.ListOffersAsync(ct));

    [HttpPost]
    public async Task<ActionResult<JobOfferDto>> Create(UpsertJobOfferRequest request, CancellationToken ct) =>
        Ok(await recruitment.CreateOfferAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobOfferDto>> Update(Guid id, UpsertJobOfferRequest request, CancellationToken ct) =>
        Ok(await recruitment.UpdateOfferAsync(id, request, ct));

    [HttpGet("{id:guid}/candidates")]
    public async Task<ActionResult<IReadOnlyList<CandidateDto>>> Candidates(Guid id, CancellationToken ct) =>
        Ok(await recruitment.ListCandidatesAsync(id, ct));

    [HttpPost("{id:guid}/candidates")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<CandidateDto>> AddCandidate(Guid id, [FromForm] CreateCandidateRequest request, IFormFile? cv, CancellationToken ct)
    {
        if (cv is { Length: > 5 * 1024 * 1024 })
            throw new Security.BusinessRuleException("Le CV dépasse 5 Mo.");
        if (cv is null || cv.Length == 0)
            return Ok(await recruitment.AddCandidateAsync(id, request, null, null, ct));

        await using var stream = cv.OpenReadStream();
        return Ok(await recruitment.AddCandidateAsync(id, request, stream, cv.FileName, ct));
    }
}

[ApiController]
[Authorize]
[Route("api/recruitment/candidates")]
public class CandidatesController(IRecruitmentService recruitment) : ControllerBase
{
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<CandidateDto>> Status(Guid id, SetCandidateStatusRequest request, CancellationToken ct) =>
        Ok(await recruitment.SetStatusAsync(id, request.Status, ct));
}

[ApiController]
[Authorize]
[Route("api/performance/goals")]
public class GoalsController(IPerformanceService performance) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GoalDto>>> List([FromQuery] Guid? employeeId, CancellationToken ct) =>
        Ok(await performance.ListGoalsAsync(employeeId, ct));

    [HttpPost]
    public async Task<ActionResult<GoalDto>> Create(UpsertGoalRequest request, CancellationToken ct) =>
        Ok(await performance.CreateGoalAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GoalDto>> Update(Guid id, UpsertGoalRequest request, CancellationToken ct) =>
        Ok(await performance.UpdateGoalAsync(id, request, ct));
}

[ApiController]
[Authorize]
[Route("api/performance/reviews")]
public class ReviewsController(IPerformanceService performance) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> List([FromQuery] Guid? employeeId, CancellationToken ct) =>
        Ok(await performance.ListReviewsAsync(employeeId, ct));

    [HttpPost]
    public async Task<ActionResult<ReviewDto>> Create(CreateReviewRequest request, CancellationToken ct) =>
        Ok(await performance.CreateReviewAsync(request, ct));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ReviewDto>> Complete(Guid id, CompleteReviewRequest request, CancellationToken ct) =>
        Ok(await performance.CompleteReviewAsync(id, request, ct));
}
