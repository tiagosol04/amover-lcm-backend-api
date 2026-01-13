using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class MotasPecasSN
{
    public int IDMotasPecasSN { get; set; }

    public int IDMota { get; set; }

    public int IDPeca { get; set; }

    public string NumeroSerie { get; set; } = null!;

    public virtual Mota IDMotaNavigation { get; set; } = null!;

    public virtual Peca IDPecaNavigation { get; set; } = null!;

    public virtual ICollection<ServicosPecasAlterada> ServicosPecasAlterada { get; set; } = new List<ServicosPecasAlterada>();
}
