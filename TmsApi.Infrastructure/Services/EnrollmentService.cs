using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Domain.Entities;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Application.Common;

namespace TmsApi.Infrastructure.Services;

public class EnrollmentService(TmsDbContext context, ILogger<EnrollmentService> logger) : IEnrollmentService
{
    public async Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct = default)
    {
        return await context.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.Course.Code == courseCode, ct);
    }

    public async Task AddAsync(Enrollment enrollment, CancellationToken ct = default)
    {
        await context.Enrollments.AddAsync(enrollment, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<List<Enrollment>> GetByStudentIdAsync(int studentId, CancellationToken ct = default)
    {
        return await context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId)
            .ToListAsync(ct);
    }
    public Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
        context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
            .FirstOrDefaultAsync(ct);

    public async Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Enrolled student {StudentId} into course {CourseId}", request.StudentId, courseId);

        return (await GetByIdAsync(courseId, enrollment.Id, ct))!;
    }

    public async Task<IEnumerable<EnrollmentResponseDto>> GetAllEnrollmentsAsync(CancellationToken ct = default)
    {
        return await context.Enrollments
            .AsNoTracking()
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.CourseId,
                e.StudentId,
                e.EnrolledAt,
                e.Student != null ? e.Student.Name : "Unknown Student",
                e.Course != null ? e.Course.Title : "Unknown Course",
                e.Status
            ))
            .ToListAsync(ct);
    }

    public async Task<Result<bool, EnrollmentError>> ApproveEnrollmentAsync(
    int enrollmentId, 
    CancellationToken ct = default)
    {
        var enrollment = await context.Enrollments.FindAsync([enrollmentId], ct);

        if (enrollment is null)
        {
            return Result<bool, EnrollmentError>.Failure(
                EnrollmentError.EnrollmentNotFound(enrollmentId));
        }

        enrollment.Status = "Approved";
        await context.SaveChangesAsync(ct);

        return Result<bool, EnrollmentError>.Success(true);
    }
}