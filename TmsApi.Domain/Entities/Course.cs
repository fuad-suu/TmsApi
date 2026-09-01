namespace TmsApi.Domain.Entities;

public class Course
{
    public int Id { get; set; } // Surrogate Primary Key
    public required string Code { get; set; } // Natural Key
    public required string Title { get; set; }
    public int MaxCapacity { get; set; }

    // Navigation property
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}