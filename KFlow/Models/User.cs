using System.ComponentModel.DataAnnotations.Schema;

namespace Scheletro.Models;

public partial class User
{
    public Guid Guid { get; set; }

    public int IDUser { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime LastModify { get; set; }

    public int Sector { get; set; }

    [NotMapped]
    public int Id
    {
        get => IDUser;
        set => IDUser = value;
    }

    [NotMapped]
    public string Nome
    {
        get => Name;
        set => Name = value;
    }

    [NotMapped]
    public string Cognome
    {
        get => Surname;
        set => Surname = value;
    }
}
