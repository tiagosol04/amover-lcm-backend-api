using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class UtilizadorMotum
{
    public int IDUtilizadorMota { get; set; }

    public int IDMota { get; set; }

    public int IdUtilizador { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataInativacao { get; set; }

    public int Estado { get; set; }

    public string? MotivoInativacao { get; set; }

    public virtual Mota IDMotaNavigation { get; set; } = null!;

    public virtual Utilizadore IdUtilizadorNavigation { get; set; } = null!;
}
