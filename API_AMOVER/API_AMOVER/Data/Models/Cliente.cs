using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class Cliente
{
    public int IDCliente { get; set; }

    public string Nome { get; set; } = null!;

    public int Tipo { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataModificacao { get; set; }

    public DateTime? UltimaEncomenda { get; set; }

    public virtual ICollection<Encomenda> Encomenda { get; set; } = new List<Encomenda>();

    public virtual ICollection<OrdemProducao> OrdemProducaos { get; set; } = new List<OrdemProducao>();
}
