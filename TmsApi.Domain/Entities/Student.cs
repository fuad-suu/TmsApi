namespace TmsApi.Domain.Entities;

public class Student
{
    public int Id { get; set; } // Surrogate Primary Key
    public required string RegistrationNumber { get; set; } // Natural Key
    public required string Name { get; set; }
    public decimal GPA { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public bool IsDeleted { get; set; } = false;
    public uint Version { get; set; }
}