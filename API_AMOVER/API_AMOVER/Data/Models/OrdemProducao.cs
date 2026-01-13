using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class OrdemProducao
{
    public int IDOrdemProducao { get; set; }

    public int IDEncomenda { get; set; }

    public string NumeroOrdem { get; set; } = null!;

    public int Estado { get; set; }

    public string PaisDestino { get; set; } = null!;

    public DateTime DataCriacao { get; set; }

    public DateTime? DataConclusao { get; set; }

    public int? ClienteIDCliente { get; set; }

    public int? ModeloMotaIDModelo { get; set; }

    public int? EncomendaIDEncomenda { get; set; }

    public virtual ICollection<ChecklistControlo> ChecklistControlos { get; set; } = new List<ChecklistControlo>();

    public virtual ICollection<ChecklistEmbalagem> ChecklistEmbalagems { get; set; } = new List<ChecklistEmbalagem>();

    public virtual ICollection<ChecklistMontagem> ChecklistMontagems { get; set; } = new List<ChecklistMontagem>();

    public virtual Cliente? ClienteIDClienteNavigation { get; set; }

    public virtual Encomenda? EncomendaIDEncomendaNavigation { get; set; }

    public virtual Encomenda IDEncomendaNavigation { get; set; } = null!;

    public virtual ModelosMotum? ModeloMotaIDModeloNavigation { get; set; }

    public virtual Mota? Mota { get; set; }
}
