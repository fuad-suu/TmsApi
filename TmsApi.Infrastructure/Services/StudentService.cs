using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class StudentService(TmsDbContext context) : IStudentService
{
    public async Task<bool> ExistsAsync(int studentId, CancellationToken ct = default)
    {
        return await context.Students.AnyAsync(s => s.Id == studentId, ct);
    }
}