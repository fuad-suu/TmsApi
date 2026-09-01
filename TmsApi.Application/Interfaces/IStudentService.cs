namespace TmsApi.Application.Interfaces;

public interface IStudentService
{
    Task<bool> ExistsAsync(int studentId, CancellationToken ct = default);
}