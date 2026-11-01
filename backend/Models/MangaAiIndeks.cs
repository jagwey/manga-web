using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Kage95.Backend.Models;
[Table("MangaAiIndeks")]
public class MangaAiIndeks
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int MangaId { get; set; }
    [MaxLength(64)] public string IcerikHash { get; set; } = "";
    public string Vektor { get; set; } = "";
}

