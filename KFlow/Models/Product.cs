namespace Scheletro.Models;

public partial class Product
{
    public Guid Guid { get; set; }

    public string ID { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string UMBase { get; set; } = string.Empty;
}
