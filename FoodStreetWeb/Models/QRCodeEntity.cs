public class QRCodeEntity
{
    public int Id { get; set; }

    public string Code { get; set; }

    public int PoiId { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpireAt { get; set; }

    public DateTime? UsedAt { get; set; }
}