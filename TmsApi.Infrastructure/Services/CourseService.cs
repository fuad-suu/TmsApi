using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class CourseService(TmsDbContext context) : ICourseService
{
    public async Task<Course?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }
    public async Task<CourseDetailDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var courseData = await context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                EnrollmentCount = c.Enrollments.Count
            })
            .FirstOrDefaultAsync(ct);

        if (courseData is null)
        {
            return null;
        }

        // Base HATEOAS links
        var links = new List<LinkDto>
        {
            new($"/api/courses/{id}", "self", "GET"),
            new($"/api/courses/{id}/enrollments", "enrollments", "GET"),
            new($"/api/courses/{id}", "update", "PUT"),
            new($"/api/courses/{id}", "delete", "DELETE")
        };

        // Conditional Link: Add "enroll" action link ONLY if the course has available capacity
        if (courseData.EnrollmentCount < courseData.MaxCapacity)
        {
            links.Add(new($"/api/courses/{id}/enrollments", "enroll", "POST"));
        }

        return new CourseDetailDto(
            courseData.Id,
            courseData.Code,
            courseData.Title,
            courseData.MaxCapacity,
            courseData.EnrollmentCount,
            links
        );
    }

    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity
        };

        context.Courses.Add(course);
        await context.SaveChangesAsync(ct);

        return new CourseResponseDto(course.Id, course.Code, course.Title, course.MaxCapacity, 0);
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct)
    {
        return await context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Code == code, ct);
    }

    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct)
    {
        IQueryable<Course> query = context.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchPattern = $"%{request.Search}%";
            query = query.Where(c => EF.Functions.ILike(c.Title, searchPattern) || EF.Functions.ILike(c.Code, searchPattern));
        }

        var totalCount = await query.CountAsync(ct);

        query = request.OrderBy.ToLowerInvariant() switch
        {
            "code" => request.Descending ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code),
            "maxcapacity" => request.Descending ? query.OrderByDescending(c => c.MaxCapacity) : query.OrderBy(c => c.MaxCapacity),
            _ => request.Descending ? query.OrderByDescending(c => c.Title) : query.OrderBy(c => c.Title)
        };

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count))
            .ToListAsync(ct);

        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}