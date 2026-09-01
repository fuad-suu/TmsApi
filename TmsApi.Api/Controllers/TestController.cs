using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/test")]
public class TestController(TmsDbContext context) : ControllerBase
{
    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine("\n>>> STEP 1: Building the query object (no database contact)...");
        var query = context.Students.Where(s => s.GPA >= 3.0m);

        Console.WriteLine(">>> STEP 2: Appending a sorting clause...");
        var orderedQuery = query.OrderBy(s => s.Name);

        Console.WriteLine(">>> STEP 3: Materializing query into a C# List...");
        var results = orderedQuery.ToList();

        Console.WriteLine(">>> STEP 4: Materialization finished. List populated.\n");
        return Ok(results);
    }

    // Non-translatable C# helper method
    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }

    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine("\n>>> STEP 1: Running non-translatable query...");
        try
        {
            var students = context.Students
                .Where(s => IsHonorRoll(s.GPA)) // EF Core cannot translate IsHonorRoll into SQL!
                .ToList();

            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> EXCEPTION CAUGHT: {ex.Message}\n");
            return BadRequest(new { Message = ex.Message });
        }
    }
    [HttpGet("students/paged")]
    public async Task<IActionResult> GetPagedStudents(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Ensure valid non-negative paging parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var students = await context.Students
            .OrderBy(s => s.Name) // Stable sort required BEFORE Skip/Take
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(students);
    }
    [HttpGet("courses/top-5")]
    public async Task<IActionResult> GetTop5Courses(CancellationToken cancellationToken = default)
    {
        var topCourses = await context.Courses
            .Select(c => new
            {
                CourseTitle = c.Title,
                EnrollmentCount = c.Enrollments.Count
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .Take(5)
            .ToListAsync(cancellationToken);

        return Ok(topCourses);
    }
}