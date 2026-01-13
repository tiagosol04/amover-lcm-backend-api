using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class ChecklistModelo
{
    public int ID { get; set; }

    public int IDChecklist { get; set; }

    public int IDModelo { get; set; }

    public virtual Checklist IDChecklistNavigation { get; set; } = null!;

    public virtual ModelosMotum IDModeloNavigation { get; set; } = null!;
}
