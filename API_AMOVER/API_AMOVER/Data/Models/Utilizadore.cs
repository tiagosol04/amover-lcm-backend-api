using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class Utilizadore
{
    public int IdUtilizador { get; set; }

    public string Nome { get; set; } = null!;

    public string Email { get; set; } = null!;

    public int Estado { get; set; }

    public DateTime DataCriacao { get; set; }

    public string? KeycloakId { get; set; }

    public string Telefone { get; set; } = null!;

    public virtual ICollection<UtilizadorMotum> UtilizadorMota { get; set; } = new List<UtilizadorMotum>();
}
