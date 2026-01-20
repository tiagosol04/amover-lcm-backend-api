using System;
using System.Collections.Generic;

namespace API_AMOVER.Data.Models;

public partial class DocumentosModelo
{
    public int IDDocumentosModelo { get; set; }

    public int IDModelo { get; set; }

    public int IDDocumento { get; set; }

    public string Caminho { get; set; } = null!;

    // Navegações corretas
    public virtual ModelosMotum Modelo { get; set; } = null!;

    public virtual Documento Documento { get; set; } = null!;
}
