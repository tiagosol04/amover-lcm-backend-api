using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class Documento
{
    public int IDDocumento { get; set; }

    public string Nome { get; set; } = null!;

    public virtual ICollection<DocumentosModelo> DocumentosModelos { get; set; } = new List<DocumentosModelo>();
}
