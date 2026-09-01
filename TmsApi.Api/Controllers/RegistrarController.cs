using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/registrar")]
public class RegistrarController(TmsDbContext context) : ControllerBase
{
    // 1. How many active students have GPA >= 3.0?
    [HttpGet("active-high-gpa-count")]
    public async Task<IActionResult> GetActiveHighGpaCount()
    {
        var count = await context.Students
            .Where(s => s.IsActive && s.GPA >= 3.0m)
            .CountAsync();

        return Ok(new { ActiveHighGpaCount = count });
    }

    // 2. Which courses have the most enrollments, sorted descending?
    [HttpGet("popular-courses")]
    public async Task<IActionResult> GetPopularCourses()
    {
        var list = await context.Courses
            .Select(c => new
            {
                c.Title,
                EnrollmentCount = c.Enrollments.Count
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .ToListAsync();

        return Ok(list);
    }

    // 3. What is the average GPA per course?
    [HttpGet("course-average-gpas")]
    public async Task<IActionResult> GetCourseAverageGpas()
    {
        var list = await context.Enrollments
            .GroupBy(e => e.Course.Title)
            .Select(g => new
            {
                Course = g.Key,
                AverageGPA = g.Average(e => e.Student.GPA)
            })
            .ToListAsync();

        return Ok(list);
    }

    // 4. Which students have zero enrollments? (Comparing Subquery vs EF Core 10 LeftJoin)
    [HttpGet("uninrolled-students")]
    public async Task<IActionResult> GetUnenrolledStudents([FromQuery] string method = "subquery")
    {
        if (method.ToLower() == "leftjoin")
        {
            // Approach B: EF Core 10 LeftJoin
            var leftJoinList = await context.Students
                .LeftJoin(context.Enrollments,
                    s => s.Id,
                    e => e.StudentId,
                    (s, e) => new { s, e })
                .Where(x => x.e == null)
                .Select(x => x.s.Name)
                .ToListAsync();

            return Ok(new { Method = "EF Core 10 LeftJoin", Students = leftJoinList });
        }

        // Approach A: Subquery (NOT EXISTS)
        var subqueryList = await context.Students
            .Where(s => !s.Enrollments.Any())
            .Select(s => s.Name)
            .ToListAsync();

        return Ok(new { Method = "Subquery (NOT EXISTS)", Students = subqueryList });
    }
}