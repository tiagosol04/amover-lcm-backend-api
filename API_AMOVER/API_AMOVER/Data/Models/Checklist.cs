using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class Checklist
{
    public int IDChecklist { get; set; }

    public string Nome { get; set; } = null!;

    public string Descricao { get; set; } = null!;

    public int Tipo { get; set; }

    public virtual ICollection<ChecklistControlo> ChecklistControlos { get; set; } = new List<ChecklistControlo>();

    public virtual ICollection<ChecklistEmbalagem> ChecklistEmbalagems { get; set; } = new List<ChecklistEmbalagem>();

    public virtual ICollection<ChecklistModelo> ChecklistModelos { get; set; } = new List<ChecklistModelo>();

    public virtual ICollection<ChecklistMontagem> ChecklistMontagems { get; set; } = new List<ChecklistMontagem>();
}
