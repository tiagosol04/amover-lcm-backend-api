using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class Servico
{
    public int IDServico { get; set; }

    public int IDMota { get; set; }

    public int Tipo { get; set; }

    public string? Descricao { get; set; }

    public int Estado { get; set; }

    public DateTime DataServico { get; set; }

    public DateTime? DataConclusao { get; set; }

    public string? NotasServico { get; set; }

    public virtual Mota IDMotaNavigation { get; set; } = null!;

    public virtual ICollection<ServicosPecasAlterada> ServicosPecasAlterada { get; set; } = new List<ServicosPecasAlterada>();
}
