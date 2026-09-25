using System;
using System.Collections.Generic;

namespace Scheletro.Models;

public partial class User
{
    public Guid Guid { get; set; }

    public int IDUser { get; set; }

    public int Id => IDUser;
    public string Nome => Name;
    public string Cognome => Surname;

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string Department { get; set; } = null!;

    public bool Enabled { get; set; }

    public DateOnly CreateDate { get; set; }

    public DateTime LastModify { get; set; }

    public int Sector { get; set; }
}
