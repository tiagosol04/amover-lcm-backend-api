using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class DocumentosModelo
{
    public int IDDocumentosModelo { get; set; }

    public int IDModelo { get; set; }

    public int IDDocumento { get; set; }

    public string Caminho { get; set; } = null!;

    public virtual ModelosMotum IDModelo1 { get; set; } = null!;

    public virtual Documento IDModeloNavigation { get; set; } = null!;
}
