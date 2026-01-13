using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class ModeloPecasSN
{
    public int IDModeloPSN { get; set; }

    public int IDModelo { get; set; }

    public int IDPeca { get; set; }

    public int? ModeloMotaIDModelo { get; set; }

    public string? EspecificacaoPadrao { get; set; }

    public virtual ModelosMotum IDModeloNavigation { get; set; } = null!;

    public virtual Peca IDPecaNavigation { get; set; } = null!;

    public virtual ModelosMotum? ModeloMotaIDModeloNavigation { get; set; }
}
