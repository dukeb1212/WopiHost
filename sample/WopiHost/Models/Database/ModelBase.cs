#nullable enable
using System.ComponentModel.DataAnnotations;

namespace WopiHost.Models.Database;

public class ModelBase : ViewModelBase
{
    [Key]
    public int Id { get; set; }
    public int Active { get; set; } = 1;
    public string? Version { get; set; }
    public bool? MarkAsClose { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow; // Use UTC for PostgreSQL compatibility
    public DateTime WriteDate { get; set; } = DateTime.UtcNow;  // Use UTC for PostgreSQL compatibility
    public int? CreateUid { get; set; }
    public int? WriteUid { get; set; }
    public string? TrangThai { get; set; }
    public string? GhiChu { get; set; }
}
