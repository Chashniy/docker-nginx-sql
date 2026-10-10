namespace Backend.Models;

public class TimeRecord
{
    public Guid Id { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}