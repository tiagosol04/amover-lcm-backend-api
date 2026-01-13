using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class ChecklistMontagem
{
    public int IDChecklistMontagem { get; set; }

    public int IDChecklist { get; set; }

    public int IDOrdemProducao { get; set; }

    public int Verificado { get; set; }

    public virtual Checklist IDChecklistNavigation { get; set; } = null!;

    public virtual OrdemProducao IDOrdemProducaoNavigation { get; set; } = null!;
}
