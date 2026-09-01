using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IEnrollmentService
{
    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct);
    Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct = default);
    Task AddAsync(Enrollment enrollment, CancellationToken ct = default);
    Task<List<Enrollment>> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
    Task<IEnumerable<EnrollmentResponseDto>> GetAllEnrollmentsAsync(CancellationToken ct = default);
    Task<Result<bool, EnrollmentError>> ApproveEnrollmentAsync(int enrollmentId, CancellationToken ct = default);
}