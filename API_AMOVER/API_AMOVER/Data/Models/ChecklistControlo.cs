using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class ChecklistControlo
{
    public int IDChecklistControlo { get; set; }

    public int IDChecklist { get; set; }

    public int IDOrdemProducao { get; set; }

    public int ControloFinal { get; set; }

    public virtual Checklist IDChecklistNavigation { get; set; } = null!;

    public virtual OrdemProducao IDOrdemProducaoNavigation { get; set; } = null!;
}
