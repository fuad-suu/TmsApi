using MediatR;
using TmsApi.Application.Common;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Commands;

public class ApproveEnrollmentCommandHandler(IEnrollmentService enrollmentService)
    : IRequestHandler<ApproveEnrollmentCommand, Result<bool, EnrollmentError>>
{
    public async Task<Result<bool, EnrollmentError>> Handle(
        ApproveEnrollmentCommand request, 
        CancellationToken ct)
    {
        return await enrollmentService.ApproveEnrollmentAsync(request.Id, ct);
    }
}