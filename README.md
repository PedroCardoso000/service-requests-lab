# Laboratório: API de Solicitações de Serviço

Etapa atual: regras de negócio e persistência. ASP.NET Core Minimal API (.NET 10), Entity Framework Core 10.0.12 e SQLite. Uma aplicação, sem serviços adicionais.

## Executar no PowerShell

```powershell
cd C:\repo\service-requests-lab
dotnet tool restore
dotnet restore ServiceRequests.slnx
dotnet ef database update --project src/ServiceRequests.Api
dotnet build ServiceRequests.slnx --no-restore
dotnet run --project src/ServiceRequests.Api --launch-profile http --no-build
```

O perfil http usa http://localhost:5080 e Development. Ctrl+C encerra o servidor. Se a porta estiver ocupada, acrescente `-- --urls http://localhost:5081` ao comando run.

O banco fica em `src/ServiceRequests.Api/data/requests.db`. Reiniciar a API preserva os dados. Não apague esse arquivo para atualizar o schema: utilize migrações. `dotnet ef database update` aplica somente migrações pendentes. O caminho é relativo ao diretório do projeto; pode ser substituído por configuração `Database:Path` (variável de ambiente `Database__Path`).

## Identidade temporária

Envie o header X-Lab-User com um destes valores:

| Valor | Perfil |
|---|---|
| solicitante-1, solicitante-2 | Solicitante |
| operador-1, operador-2 | Operador |
| admin-1 | Administrador |

Essas identidades são fixas e qualquer pessoa pode simulá-las. Não são autenticação. Os endpoints /requests retornam 503 fora de Development. /health continua público e verifica apenas resposta HTTP. Keycloak será integrado na etapa prevista.

## Contrato HTTP

| Método | Caminho | Ação |
|---|---|---|
| GET | /health | Verifica resposta do processo |
| POST | /requests | Solicitante cria solicitação (201) |
| GET | /requests | Lista conforme a visibilidade do perfil |
| GET | /requests/{id} | Consulta dentro da visibilidade do perfil |
| PUT | /requests/{id} | Solicitante edita os quatro campos da própria solicitação Criada |
| GET | /requests/queue | Operador consulta fila Criada e sem operador |
| POST | /requests/{id}/claim | Operador assume e inicia atendimento |
| POST | /requests/{id}/complete | Operador responsável conclui |
| POST | /requests/{id}/cancel | Solicitante cancela a própria solicitação Criada |
| POST | /requests/{id}/assign | Administrador atribui a operador e inicia atendimento |

POST e PUT recebem title, description, category e priority. O PUT substitui esses quatro campos, não é uma edição parcial. Título: 1 a 200 caracteres; descrição: 1 a 5000; espaços nas extremidades são removidos. Categoria e prioridade são obrigatórias e representadas por strings, sem valores numéricos.

Categorias: `LevantamentoDeRequisitos`, `CriacaoDePlataforma`, `CriacaoDeAplicativo`, `SuporteDeTI`.
Prioridades: `Alta`, `Media`, `Baixa`.
Estados: `Criada`, `EmAtendimento`, `Concluida`, `Cancelada`.

ID, solicitante, estado e data UTC são determinados pelo servidor. Não são campos editáveis no contrato. O operador só consulta a fila disponível e seus próprios atendimentos, inclusive os concluídos. O solicitante só consulta suas solicitações, inclusive as canceladas. Administrador consulta todas.

Decisões adotadas: administrador pode atribuir apenas solicitações Criadas; atribuição equivale a iniciar atendimento. Não há reatribuição, reabertura, exclusão ou poderes administrativos de edição/cancelamento. Canceladas continuam consultáveis pelo dono e administrador, mas não pelo operador. A fila usa ordem de criação, sem regra adicional de ordenação por prioridade.

Erros: 400 entrada inválida; 401 identidade ausente/desconhecida; 403 perfil não permitido; 404 inexistente ou fora do acesso do usuário; 409 estado incompatível ou conflito de concorrência. JSON malformado também retorna 400.

## Experimentar manualmente (outro terminal)

```powershell
$base = 'http://localhost:5080'
$solicitante = @{'X-Lab-User'='solicitante-1'}
$operador = @{'X-Lab-User'='operador-1'}
$body = @{title='Configurar estação'; description='Preciso configurar a estação de trabalho'; category='SuporteDeTI'; priority='Alta'} | ConvertTo-Json
$r = Invoke-RestMethod "$base/requests" -Method Post -Headers $solicitante -ContentType 'application/json' -Body $body
$r
Invoke-RestMethod "$base/requests" -Headers $solicitante
Invoke-RestMethod "$base/requests/queue" -Headers $operador
Invoke-RestMethod "$base/requests/$($r.id)/claim" -Method Post -Headers $operador
# Cancelar agora deve falhar com 409, pois o atendimento já começou.
Invoke-WebRequest "$base/requests/$($r.id)/cancel" -Method Post -Headers $solicitante -SkipHttpErrorCheck
Invoke-RestMethod "$base/requests/$($r.id)/complete" -Method Post -Headers $operador
```

Para editar antes de assumir: envie PUT à URL do item com os mesmos quatro campos do POST. Para cancelar: crie outro item e chame /cancel antes de /claim. Para atribuir, use admin-1 e POST /assign com `{"operatorId":"operador-2"}`.

## Roteiro reproduzível de verificação HTTP

Com a API em execução e PowerShell 7 disponível:

```powershell
pwsh -NoProfile -File scripts/verify-api.ps1
```

O roteiro cria três solicitações e verifica sucesso, validação, isolamento, restrições de perfil, transições, atribuição e exclusão das canceladas da fila. Não apaga dados existentes. Pode ser repetido; cada execução cria novos IDs. Para outra porta, passe `-BaseUrl http://localhost:5081`.

Ao final, copie o ID exibido, reinicie a API e consulte GET /requests/{id} com solicitante-1. O registro deve continuar Concluida. Este é um roteiro HTTP da etapa atual, não o projeto de testes unitários e integração da etapa de qualidade.

## Organização e decisões

- `Program.cs`: configura JSON, caminho SQLite, injeção do DbContext e rotas.
- `Domain/ServiceRequest.cs`: dados e transições. Setters privados impedem alteração arbitrária dos estados pelo restante da aplicação.
- `Data/RequestsDbContext.cs`: traduz entidades para tabelas, configura enums em texto, índices e token de concorrência.
- `Data/Migrations/`: histórico do schema e snapshot gerados pelo EF Core. A migração inicial cria a tabela Requests.
- `Development/LabIdentity.cs`: catálogo fixo de usuários para exercitar as regras de acesso.
- `Endpoints/RequestEndpoints.cs`: DTOs, validação de entrada, visibilidade, permissões e respostas HTTP.
- `dotnet-tools.json`: fixa dotnet-ef como ferramenta local do projeto.
- `global.json`: fixa SDK 10.0.401; `.csproj` fixa dependências e destino net10.0.
- `appsettings*.json` e `Properties/launchSettings.json`: configurações gerais e perfil local.

Os caminhos acima, exceto manifesto e global.json, são relativos a src/ServiceRequests.Api. Não foi adicionado um repositório genérico: o DbContext já atende à persistência deste laboratório.

Cada alteração troca um token Version. O EF usa a versão lida na condição do UPDATE. Se outra operação gravar primeiro, a segunda falha com 409 em vez de sobrescrever a primeira. Assim, assumir e cancelar simultaneamente não podem ambos vencer. Uma tentativa posterior de outro operador pode retornar 404, pois o item já saiu da sua visibilidade.

Referências: https://learn.microsoft.com/ef/core/saving/concurrency e https://learn.microsoft.com/ef/core/managing-schemas/migrations/

## Etapa 3: Git local

O primeiro commit registra o estado atual da aplicação, incluindo os arquivos de integração Docker/Visual Studio que já estavam na pasta. Esses arquivos foram preservados; a etapa Docker não foi validada aqui. O banco SQLite, bin/, obj/ e .vs/ ficam fora do histórico. Código de Data/ e migrações ficam dentro dele.

Git é o histórico local do projeto. GitHub é um serviço remoto para hospedar e compartilhar esse histórico; nenhum remoto foi configurado nesta etapa.

Fluxo básico, executado na raiz do repositório:

```powershell
git status
# Edite os arquivos necessários.
git diff
# Selecione explicitamente os arquivos da alteração, por exemplo:
git add README.md
git diff --staged
git commit -m "docs: esclarece execução local"
git status
git log --oneline -5
```

O diretório de trabalho contém as edições. `git add` seleciona a versão atual de um arquivo para a área de preparação (staging). `git commit` registra somente o conteúdo preparado. Se editar novamente depois de `git add`, precisa adicioná-lo outra vez para incluir essa nova edição. `git diff` mostra alterações ainda não preparadas; `git diff --staged` mostra o que entrará no commit. Arquivos novos aparecem em `git status`, mas não no `git diff` comum até serem preparados.

Para tirar um arquivo da preparação sem perder a edição, use `git restore --staged README.md`. O .gitignore não remove do histórico arquivos já versionados; ele evita adicionar arquivos locais ainda não rastreados. Commit não publica nada na internet e não substitui uma cópia remota.
