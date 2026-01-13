using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class ChecklistEmbalagem
{
    public int IDChecklistEmbalagem { get; set; }

    public int IDChecklist { get; set; }

    public int IDOrdemProducao { get; set; }

    public int Incluido { get; set; }

    public virtual Checklist IDChecklistNavigation { get; set; } = null!;

    public virtual OrdemProducao IDOrdemProducaoNavigation { get; set; } = null!;
}
