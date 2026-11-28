namespace OnlineSignature.Models;

public class Signature
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public byte[] SignatureImage { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}