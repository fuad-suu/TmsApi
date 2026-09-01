using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface ICourseService
{
    Task<CourseDetailDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct);
    Task<Course?> GetByCodeAsync(string code, CancellationToken ct = default);
}