using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class Mota
{
    public int IDMota { get; set; }

    public int IDModelo { get; set; }

    public DateTime DataRegisto { get; set; }

    public string Cor { get; set; } = null!;

    public double Quilometragem { get; set; }

    public int Estado { get; set; }

    public int IDOrdemProducao { get; set; }

    public string NumeroIdentificacao { get; set; } = null!;

    public virtual ModelosMotum IDModeloNavigation { get; set; } = null!;

    public virtual OrdemProducao IDOrdemProducaoNavigation { get; set; } = null!;

    public virtual ICollection<MotasPecasInfo> MotasPecasInfos { get; set; } = new List<MotasPecasInfo>();

    public virtual ICollection<MotasPecasSN> MotasPecasSNs { get; set; } = new List<MotasPecasSN>();

    public virtual ICollection<Servico> Servicos { get; set; } = new List<Servico>();

    public virtual ICollection<UtilizadorMotum> UtilizadorMota { get; set; } = new List<UtilizadorMotum>();
}
