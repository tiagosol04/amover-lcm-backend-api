using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class ModelosMotum
{
    public int IDModelo { get; set; }

    public string CodigoProduto { get; set; } = null!;

    public string Nome { get; set; } = null!;

    public DateTime DataInicioProducao { get; set; }

    public DateTime? DataLancamento { get; set; }

    public DateTime? DataDescontinuacao { get; set; }

    public int Estado { get; set; }

    public virtual ICollection<ChecklistModelo> ChecklistModelos { get; set; } = new List<ChecklistModelo>();

    public virtual ICollection<DocumentosModelo> DocumentosModelos { get; set; } = new List<DocumentosModelo>();

    public virtual ICollection<Encomenda> Encomenda { get; set; } = new List<Encomenda>();

    public virtual ICollection<ModeloPecasFixa> ModeloPecasFixas { get; set; } = new List<ModeloPecasFixa>();

    public virtual ICollection<ModeloPecasSN> ModeloPecasSNIDModeloNavigations { get; set; } = new List<ModeloPecasSN>();

    public virtual ICollection<ModeloPecasSN> ModeloPecasSNModeloMotaIDModeloNavigations { get; set; } = new List<ModeloPecasSN>();

    public virtual ICollection<Mota> Mota { get; set; } = new List<Mota>();

    public virtual ICollection<OrdemProducao> OrdemProducaos { get; set; } = new List<OrdemProducao>();
}
