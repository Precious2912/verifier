namespace CrudSystem.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; protected set; }
    public void MarkAsModified()
    {
        ModifiedAt = DateTime.UtcNow;
    }
}
