using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;
using Microsoft.AspNetCore.RateLimiting;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/enrollments")]
[ApiVersion("2.0")]
public class EnrollmentsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("strict-concurrency")] // Limits simultaneous POST operations
    public async Task<IActionResult> Enroll(
        EnrollStudentCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.Match<IActionResult>(
            onSuccess: created => CreatedAtAction(
                nameof(GetSchedule),
                new { studentId = created.StudentId },
                created),
            onFailure: error =>
            {
                var status = error.Code switch
                {
                    "course_not_found" or "student_not_found" => StatusCodes.Status404NotFound,
                    "course_full" or "already_enrolled" => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest
                };

                return Problem(
                    statusCode: status,
                    title: "Enrollment rejected",
                    detail: error.Message,
                    type: $"https://tms.local/errors/{error.Code}");
            });
    }

    [HttpGet]
    [EnableRateLimiting("fixed-by-ip")]
    public async Task<IActionResult> GetEnrollments(CancellationToken ct)
    {
        var enrollments = await mediator.Send(new GetAllEnrollmentsQuery(), ct);
        return Ok(enrollments);
    }

    [HttpGet("{studentId}/schedule")]
    [EnableRateLimiting("fixed-by-ip")]
    public async Task<IActionResult> GetSchedule(
        int studentId, CancellationToken ct)
    {
        var schedule = await mediator.Send(new GetStudentScheduleQuery(studentId), ct);
        return Ok(schedule);
    }

    [HttpPut("{id:int}/approve")]
    [HttpPost("{id:int}/approve")]
    [EnableRateLimiting("strict-concurrency")]
    public async Task<IActionResult> ApproveEnrollment(int id, CancellationToken ct)
    {
        var result = await mediator.Send(new ApproveEnrollmentCommand(id), ct);

        return result.Match<IActionResult>(
            onSuccess: _ => NoContent(),
            onFailure: error => Problem(
                statusCode: error.Code == "enrollment_not_found"
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest,
                title: "Approval failed",
                detail: error.Message,
                type: $"https://tms.local/errors/{error.Code}")
        );
    }
}