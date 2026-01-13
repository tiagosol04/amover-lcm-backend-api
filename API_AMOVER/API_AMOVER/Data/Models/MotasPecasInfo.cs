using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class MotasPecasInfo
{
    public int IDMotasPecasInfo { get; set; }

    public int IDMota { get; set; }

    public int IDPeca { get; set; }

    public string? InformacaoAdicional { get; set; }

    public virtual Mota IDMotaNavigation { get; set; } = null!;

    public virtual Peca IDPecaNavigation { get; set; } = null!;
}
