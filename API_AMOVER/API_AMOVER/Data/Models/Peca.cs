using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class Peca
{
    public int IDPeca { get; set; }

    public string PartNumber { get; set; } = null!;

    public string Descricao { get; set; } = null!;

    public virtual ICollection<ModeloPecasFixa> ModeloPecasFixas { get; set; } = new List<ModeloPecasFixa>();

    public virtual ICollection<ModeloPecasSN> ModeloPecasSNs { get; set; } = new List<ModeloPecasSN>();

    public virtual ICollection<MotasPecasInfo> MotasPecasInfos { get; set; } = new List<MotasPecasInfo>();

    public virtual ICollection<MotasPecasSN> MotasPecasSNs { get; set; } = new List<MotasPecasSN>();
}
