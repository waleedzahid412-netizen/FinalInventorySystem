namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Marker interface for soft-delete support.
    /// All master and transactional entities that carry IsDeleted must implement this.
    /// The ApplicationDbContext global query filter automatically excludes IsDeleted=true records.
    /// </summary>
    public interface IHasIsDeleted
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
    }
}
