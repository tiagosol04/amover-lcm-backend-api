# API AMOVER — Notas de Backend

## Estados confirmados

### OrdemProducao.Estado (int)
| Valor | Nome         | Notas                                         |
|-------|--------------|-----------------------------------------------|
| 0     | Aberta       | Criada mas não iniciada em produção            |
| 1     | Em Produção  | Iniciada via POST /iniciar; checklists criados |
| 2     | Concluída    | Finalizada via POST /finalizar                 |
| 3     | Bloqueada    | Adicionado nesta fase; sem campo Motivo na BD  |

### Mota.Estado (int)
| Valor | Nome           |
|-------|----------------|
| 0     | Em Produção    |
| 1     | Ativa          |
| 2     | Em Manutenção  |
| 3     | Descontinuada  |

### Servico.Estado (int)
| Valor | Nome       |
|-------|------------|
| 0     | Agendado   |
| 1     | Em Curso   |
| 2     | Concluído  |

### Servico.Tipo (int)
| Valor | Nome                  |
|-------|-----------------------|
| 1     | Manutenção            |
| 2     | Avaria                |
| 3     | Garantia              |
| 4     | Inspeção              |
| 5     | Diagnóstico           |
| 6     | Preparação / Entrega  |
| 7     | Campanha Técnica      |
| 8     | Outro                 |

### Checklist.Tipo (int)
| Valor | Nome      |
|-------|-----------|
| 1     | Montagem  |
| 2     | Embalagem |
| 3     | Controlo  |

### Utilizadore.Estado / UtilizadorMotum.Estado (int)
| Valor | Nome    |
|-------|---------|
| 0     | Inativo |
| 1     | Ativo   |

---

## Endpoints — Fase 1 (sessão anterior)

### OrdensController — `/api/ordens`
| Método | Rota                              | Descrição                                                         |
|--------|-----------------------------------|-------------------------------------------------------------------|
| GET    | `/api/ordens?estado=X&includeNomes=true` | Lista de ordens; `includeNomes=true` adiciona clienteNome, modeloNome, modeloCodigo |
| GET    | `/api/ordens/{id}`                | Detalhe básico de uma ordem                                       |
| GET    | `/api/ordens/{id}/ficha`          | Ficha operacional completa: ordem + encomenda + cliente + modelo + mota + checklists + pecas-SN + utilizadores ativos + serviços |
| GET    | `/api/ordens/{id}/resumo`         | Resumo de progresso da ordem                                      |
| GET    | `/api/ordens/{id}/motas`          | Lista de motas da ordem                                           |
| GET    | `/api/ordens/{id}/utilizadores?ativasOnly=true` | Utilizadores atribuídos à mota desta ordem       |
| GET    | `/api/ordens/{id}/checklists`     | Checklists da ordem (via ChecklistsController)                    |
| POST   | `/api/ordens/from-encomenda/{encomendaId}` | Criar ordem a partir de encomenda                        |
| POST   | `/api/ordens/{id}/iniciar`        | Inicia a ordem (cria checklists, valida modelo)                   |
| POST   | `/api/ordens/{id}/finalizar`      | Finaliza a ordem (valida checklists, VIN, peças-SN)               |
| POST   | `/api/ordens/{id}/reabrir`        | Reabre ordem CONCLUÍDA → EM_PRODUÇÃO                              |
| PUT    | `/api/ordens/{id}/estado`         | Só permite repor para ABERTA (design intencional)                  |

### MotasController — `/api/motas`
| Método | Rota                              | Descrição                                              |
|--------|-----------------------------------|--------------------------------------------------------|
| GET    | `/api/motas`                      | Lista com filtros: estado, ordemId, semVin             |
| GET    | `/api/motas/{id}`                 | Detalhe de uma mota                                    |
| GET    | `/api/motas/by-vin/{vin}`         | Lookup por VIN/NumeroIdentificacao                     |
| GET    | `/api/motas/{id}/pecas-sn`        | Peças serializadas registadas nesta mota               |
| GET    | `/api/motas/{id}/pecas-sn/resumo` | Resumo de peças-SN (obrigatórias vs preenchidas)       |
| GET    | `/api/motas/{id}/pecas-fixas`     | Peças fixas do modelo desta mota (template)            |
| POST   | `/api/motas`                      | Criar mota                                             |
| POST   | `/api/motas/{id}/pecas-sn`        | Registar/atualizar número de série de uma peça         |
| PUT    | `/api/motas/{id}`                 | Atualizar Cor, Quilometragem e VIN em simultâneo       |
| PUT    | `/api/motas/{id}/identificacao`   | Atualizar só o VIN                                     |
| PUT    | `/api/motas/{id}/estado`          | Atualizar só o estado da mota                          |
| DELETE | `/api/motas/pecas-sn/{idSN}`      | Remover registo de número de série                     |

---

## Endpoints — Fase 2 (esta sessão)

### OrdensController — novos endpoints
| Método | Rota                              | Descrição                                                                                       |
|--------|-----------------------------------|-------------------------------------------------------------------------------------------------|
| GET    | `/api/ordens/prontos-expedicao`   | Ordens CONCLUÍDAS com mota, cliente, modelo e VIN (proxy para "pronto para expedir")            |
| POST   | `/api/ordens/{id}/bloquear`       | Bloqueia ordem. Body: `{ "motivo": "..." }` (obrigatório). Motivo não é persistido na BD atual  |
| POST   | `/api/ordens/{id}/desbloquear`    | Desbloqueia ordem → repõe ABERTA ou EM_PRODUÇÃO (heurística). Body opcional: `{ "resolucao": "..." }`. Resolução não é persistida |

### DashboardController — `/api/dashboard` (novo ficheiro)
| Método | Rota                    | Descrição                                                                              |
|--------|-------------------------|----------------------------------------------------------------------------------------|
| GET    | `/api/dashboard/resumo` | Métricas agregadas da fábrica + lista de ordens com estado, cliente, modelo e alertas  |

**Campos do resumo:**
| Campo              | Tipo | Descrição                                                                        |
|--------------------|------|----------------------------------------------------------------------------------|
| `totalOrdens`      | int  | Total de ordens na BD                                                            |
| `emProducao`       | int  | Ordens com Estado=1                                                              |
| `bloqueadas`       | int  | Ordens com Estado=3                                                              |
| `semUnidade`       | int  | Ordens EM_PRODUCAO sem mota associada                                            |
| `controloPendente` | int  | Ordens com pelo menos um item de ChecklistControlo por fechar (ControloFinal=0)  |
| `vinPendente`      | int  | Motas sem VIN preenchido                                                         |
| `equipaAtiva`      | int  | UtilizadorMotum com Estado=1 (atribuições ativas)                                |
| `servicosEmAberto` | int  | Serviços com Estado≠2 (não concluídos)                                           |
| `ordens`           | list | Por ordem: ordemId, numeroOrdem, estado, estadoNome, modeloNome, clienteNome, temMota, vinPreenchido, controloPendente |

### AlertasController — `/api/alertas` (novo ficheiro)
| Método | Rota                               | Descrição                                                      |
|--------|------------------------------------|----------------------------------------------------------------|
| GET    | `/api/alertas?ordemId=X&tipo=X&severidade=X` | Alertas calculados do estado da BD (não persistidos) |

**Tipos de alertas calculados:**
| Tipo        | Severidade | Origem                                                                |
|-------------|------------|-----------------------------------------------------------------------|
| BLOQUEIO    | CRITICA    | Ordens com estado=3 (Bloqueada)                                       |
| OPERACIONAL | ALTA       | Ordens EM_PRODUCAO sem mota associada                                 |
| OPERACIONAL | CRITICA    | Serviços de Avaria em aberto há >7 dias                               |
| OPERACIONAL | ALTA       | Serviços de Garantia em aberto há >7 dias                             |

Todos os alertas têm `"calculado": true` — são derivados em tempo real do estado da BD.

---

## Limitações conhecidas — sem alterar BD

### Bloqueio sem histórico persistido
- `OrdemProducao` não tem campo `MotivosBloqueio`, `HistoricoEstados` nem `Notas`.
- O endpoint `/bloquear` aceita `motivo` no body mas **não o persiste**.
- Para suportar histórico de bloqueios, seria necessário uma migration additive com nova tabela ou coluna.

### Expedição sem campos específicos
- Não existe tabela nem campo de "expedição" (data de envio, transportadora, guia, etc.).
- `GET /api/ordens/prontos-expedicao` devolve as ordens CONCLUÍDAS como proxy para "pronto para expedir".
- Para controlo real de expedição, é necessária migration com tabela `Expedicao`.

### Desbloquear sem estado anterior
- `desbloquear` usa heurística: se existem checklists inicializados → EM_PRODUCAO, caso contrário → ABERTA.
- Para preservar o estado exato anterior ao bloqueio seria necessário um campo `EstadoAnterior` na BD.

### Alertas sem persistência
- Alertas são 100% calculados a cada chamada — não há tabela de alertas nem histórico.
- Alertas de qualidade (checklists incompletos, peças-SN em falta) não estão ainda implementados por complexidade de cálculo em batch.

---

## Comportamentos forçados pela API

### Estado da mota criada numa ordem (POST /api/ordens/{id}/motas)
- A API ignora o campo `estado` enviado no body e força sempre `Estado = 0` (Em Produção).
- O DTO `CriarMotaRequest` mantém o campo por compatibilidade, mas é descartado.
- **Razão:** uma mota criada dentro de uma ordem de produção nunca pode nascer Ativa, Em Manutenção ou Descontinuada.

### Motivo de bloqueio obrigatório (POST /api/ordens/{id}/bloquear)
- Se `motivo` vier null, vazio ou só espaços → 400 `{ "message": "O motivo do bloqueio é obrigatório." }`
- O motivo não é persistido (sem campo na BD). É ecoado na resposta + `aviso`.

### Resolução de desbloqueio opcional (POST /api/ordens/{id}/desbloquear)
- Body opcional: `{ "resolucao": "..." }`. Se preenchida, é ecoada na resposta + `aviso`. Não persistida.

---

## Migrações / Alterações à BD
**Nenhuma.** Todos os endpoints desta fase respeitam a estrutura existente.

O estado `ESTADO_BLOQUEADA=3` é apenas um novo valor inteiro na coluna `Estado` existente — não requer migration.

---

## O que a app Android pode consumir (novos endpoints)

| Necessidade da app Android                        | Endpoint a usar                                     |
|---------------------------------------------------|-----------------------------------------------------|
| Ficha operacional completa (1 chamada)            | `GET /api/ordens/{id}/ficha`                        |
| Lista de ordens com nomes de cliente/modelo       | `GET /api/ordens?includeNomes=true`                 |
| Editar mota (cor, km, VIN) numa chamada           | `PUT /api/motas/{id}`                               |
| Reabrir ordem concluída (tab Ações)               | `POST /api/ordens/{id}/reabrir`                     |
| Bloquear ordem (motivo obrigatório)               | `POST /api/ordens/{id}/bloquear`                    |
| Desbloquear ordem (resolução opcional)            | `POST /api/ordens/{id}/desbloquear`                 |
| Ver peças fixas do modelo da mota                 | `GET /api/motas/{id}/pecas-fixas`                   |
| Lista ordens prontas para expedição               | `GET /api/ordens/prontos-expedicao`                 |
| Alertas calculados (bloqueios, avarias, etc.)     | `GET /api/alertas`                                  |
| Dashboard agregado da fábrica                     | `GET /api/dashboard/resumo`                         |
| Utilizadores atribuídos à mota de uma ordem       | `GET /api/ordens/{id}/utilizadores`                 |
