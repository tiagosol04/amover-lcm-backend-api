using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class ServicosPecasAlterada
{
    public int ID { get; set; }

    public int IDServico { get; set; }

    public int IDMotasPecasSN { get; set; }

    public string? Observacoes { get; set; }

    public virtual MotasPecasSN IDMotasPecasSNNavigation { get; set; } = null!;

    public virtual Servico IDServicoNavigation { get; set; } = null!;
}
