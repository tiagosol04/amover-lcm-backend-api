using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class Encomenda
{
    public int IDEncomenda { get; set; }

    public int IDModelo { get; set; }

    public int IDCliente { get; set; }

    public DateTime DateCriacao { get; set; }

    public DateTime? DataEntrega { get; set; }

    public int Quantidade { get; set; }

    public int Estado { get; set; }

    public virtual Cliente IDClienteNavigation { get; set; } = null!;

    public virtual ModelosMotum IDModeloNavigation { get; set; } = null!;

    public virtual ICollection<OrdemProducao> OrdemProducaoEncomendaIDEncomendaNavigations { get; set; } = new List<OrdemProducao>();

    public virtual ICollection<OrdemProducao> OrdemProducaoIDEncomendaNavigations { get; set; } = new List<OrdemProducao>();
}
