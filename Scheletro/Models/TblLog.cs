namespace Scheletro.Models;

public partial class TblLog
{
    public int Id { get; set; }

    public string ODP { get; set; } = string.Empty;

    public string IDMix { get; set; } = string.Empty;

    public string IDRows { get; set; } = string.Empty;

    public string IDUser { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime PostingDate { get; set; }
}
