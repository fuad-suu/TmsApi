using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs; // Make sure your DTO namespace is imported!
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/courses")]
[ApiVersion("2.0")]
[EnableRateLimiting("fixed-by-ip")] // Enables 60 requests/min rate limiting
public class CoursesController(
    TmsDbContext context,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = "CoursesCachePolicy")] // Caching added here!
    public async Task<IActionResult> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var baseQuery = context.Courses.AsNoTracking();
        var totalCount = await baseQuery.CountAsync(ct);

        var rows = await baseQuery
            .OrderBy(c => c.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Code,
                c.MaxCapacity,
                c.InstructorId,
                EnrollmentCount = c.Enrollments.Count
            })
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var hasNext = page < totalPages;
        var hasPrevious = page > 1;

        return Ok(new
        {
            data = rows,
            meta = new
            {
                totalCount,
                page,
                pageSize,
                totalPages,
                hasNext,
                hasPrevious
            },
            links = new
            {
                self = $"/api/v2/courses?page={page}&pageSize={pageSize}",
                next = hasNext ? $"/api/v2/courses?page={page + 1}&pageSize={pageSize}" : (string?)null,
                prev = hasPrevious ? $"/api/v2/courses?page={page - 1}&pageSize={pageSize}" : (string?)null,
                enroll = "/api/v2/enrollments"
            }
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Instructor, Admin")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseDto dto, CancellationToken ct)
    {
        var course = await context.Courses.FindAsync([id], ct);
        if (course is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Course Not Found",
                Detail = $"Course with ID {id} was not found."
            });
        }

        // Evaluate Resource-Based Ownership Policy
        var authResult = await authorizationService.AuthorizeAsync(User, course, "CanEditCourse");
        if (!authResult.Succeeded)
        {
            // HTTP 403 Forbidden when caller doesn't own the resource
            return Forbid();
        }

        course.Title = dto.Title;
        course.MaxCapacity = dto.MaxCapacity;
        await context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCourse(int id, CancellationToken ct)
    {
        var course = await context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Course Not Found",
                Detail = $"Course with ID {id} was not found."
            });
        }

        // Business Rule: Prevent deletion if active enrollments exist
        if (course.Enrollments.Count > 0)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Course Deletion Failed",
                Detail = "Cannot delete course: active student enrollments exist."
            });
        }

        context.Courses.Remove(course);
        await context.SaveChangesAsync(ct);

        return NoContent();
    }
}