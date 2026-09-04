using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using TmsApi.Api.Hubs;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;
using TmsApi.Application.Hubs;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/enrollments")]
[ApiVersion("2.0")]
public class EnrollmentsController(
    IMediator mediator,
    IHubContext<TmsHub, ITmsHubClient> hubContext) : ControllerBase
{
    [HttpPost]
[EnableRateLimiting("strict-concurrency")]
public async Task<IActionResult> Enroll(
    EnrollStudentCommand command, CancellationToken ct)
{
    var result = await mediator.Send(command, ct);

    return await result.Match<Task<IActionResult>>(
        onSuccess: async created =>
        {
            // Broadcast the new pending enrollment to all connected clients
            await hubContext.Clients.All.ReceiveEnrollmentCreated(new
            {
                id = created.EnrollmentId.ToString(),
                studentId = created.StudentId,
                courseCode = created.CourseCode,
                status = "Pending"
            });

            return CreatedAtAction(
                nameof(GetSchedule),
                new { studentId = created.StudentId },
                created);
        },
        onFailure: error =>
        {
            var status = error.Code switch
            {
                "course_not_found" or "student_not_found" => StatusCodes.Status404NotFound,
                "course_full" or "already_enrolled" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };

            return Task.FromResult<IActionResult>(Problem(
                statusCode: status,
                title: "Enrollment rejected",
                detail: error.Message,
                type: $"https://tms.local/errors/{error.Code}"));
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

        return await result.Match<Task<IActionResult>>(
            onSuccess: async _ =>
            {
                // Broadcast enrollment status update to all connected SignalR clients
                await hubContext.Clients.All.ReceiveEnrollmentStatusUpdated(id.ToString(), "Approved");
                return NoContent();
            },
            onFailure: error => Task.FromResult<IActionResult>(Problem(
                statusCode: error.Code == "enrollment_not_found"
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest,
                title: "Approval failed",
                detail: error.Message,
                type: $"https://tms.local/errors/{error.Code}"))
        );
    }
}