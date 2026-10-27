param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
# Requer PowerShell 7. Cria dados de demonstração; não apaga solicitações existentes.
function Call([string]$Method, [string]$Path, [string]$User, $Body, [int]$Expected) {
    $argsHttp = @{ Uri="$BaseUrl$Path"; Method=$Method; SkipHttpErrorCheck=$true }
    if ($User) { $argsHttp.Headers = @{'X-Lab-User'=$User} }
    if ($null -ne $Body) { $argsHttp.Body = $Body | ConvertTo-Json; $argsHttp.ContentType='application/json' }
    $response = Invoke-WebRequest @argsHttp
    if ([int]$response.StatusCode -ne $Expected) { throw "$Method $Path esperado=$Expected recebido=$($response.StatusCode) $($response.Content)" }
    Write-Host "OK $Method $Path -> $Expected"
    if ($response.Content -and ($response.Headers["Content-Type"] -join "") -match "json") { return ($response.Content | ConvertFrom-Json) }
}
function Assert($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
$body = @{title='Laboratório HTTP'; description='Solicitação criada pelo roteiro'; category='SuporteDeTI'; priority='Alta'}
$null = Call GET /health '' $null 200
$null = Call GET /requests '' $null 401
$null = Call GET /requests desconhecido $null 401
$null = Call POST /requests operador-1 $body 403
$null = Call POST /requests solicitante-1 @{title=' ';description='x';category='SuporteDeTI';priority='Alta'} 400
$null = Call POST /requests solicitante-1 @{title='x';description='x'} 400
$null = Call POST /requests solicitante-1 @{title='x';description='x';category='Outra';priority='Alta'} 400
$a = Call POST /requests solicitante-1 $body 201
Assert ($a.status -eq 'Criada' -and $a.requesterId -eq 'solicitante-1' -and !$a.operatorId) 'Criação incorreta'
$id = $a.id
$null = Call GET "/requests/$id" solicitante-2 $null 404
$null = Call PUT "/requests/$id" solicitante-2 $body 404
$null = Call POST "/requests/$id/cancel" solicitante-2 $null 404
$others = @(Call GET /requests solicitante-2 $null 200)
Assert ($id -notin $others.id) 'Vazamento na listagem'
$body.title = 'Título editado'
$edited = Call PUT "/requests/$id" solicitante-1 $body 200
Assert ($edited.title -eq 'Título editado') 'Edição não persistiu'
$queue = @(Call GET /requests/queue operador-1 $null 200)
Assert ($id -in $queue.id) 'Criada ausente da fila'
$null = Call GET /requests/queue solicitante-1 $null 403
$null = Call POST "/requests/$id/claim" solicitante-1 $null 403
$claimed = Call POST "/requests/$id/claim" operador-1 $null 200
Assert ($claimed.status -eq 'EmAtendimento' -and $claimed.operatorId -eq 'operador-1') 'Assunção incorreta'
$null = Call GET "/requests/$id" operador-2 $null 404
$null = Call POST "/requests/$id/claim" operador-2 $null 404
$null = Call POST "/requests/$id/complete" operador-2 $null 404
$null = Call PUT "/requests/$id" solicitante-1 $body 409
$null = Call POST "/requests/$id/cancel" solicitante-1 $null 409
$done = Call POST "/requests/$id/complete" operador-1 $null 200
Assert ($done.status -eq 'Concluida') 'Conclusão incorreta'
$null = Call POST "/requests/$id/complete" operador-1 $null 409
$null = Call POST "/requests/$id/claim" operador-1 $null 409
$b = Call POST /requests solicitante-1 $body 201
$cancelled = Call POST "/requests/$($b.id)/cancel" solicitante-1 $null 200
Assert ($cancelled.status -eq 'Cancelada') 'Cancelamento incorreto'
$queue = @(Call GET /requests/queue operador-1 $null 200)
Assert ($b.id -notin $queue.id -and $id -notin $queue.id) 'Fila contém itens indisponíveis'
$null = Call GET "/requests/$($b.id)" solicitante-1 $null 200
$null = Call GET "/requests/$($b.id)" admin-1 $null 200
$null = Call GET "/requests/$($b.id)" operador-1 $null 404
$null = Call POST "/requests/$($b.id)/claim" operador-1 $null 409
$null = Call PUT "/requests/$($b.id)" solicitante-1 $body 409
$c = Call POST /requests solicitante-2 $body 201
$null = Call POST "/requests/$($c.id)/assign" operador-1 @{operatorId='operador-2'} 403
$null = Call POST "/requests/$($c.id)/assign" admin-1 @{operatorId='solicitante-1'} 400
$assigned = Call POST "/requests/$($c.id)/assign" admin-1 @{operatorId='operador-2'} 200
Assert ($assigned.operatorId -eq 'operador-2' -and $assigned.status -eq 'EmAtendimento') 'Atribuição incorreta'
$null = Call POST "/requests/$($c.id)/assign" admin-1 @{operatorId='operador-1'} 409
$null = Call GET "/requests/$($c.id)" operador-2 $null 200
$null = Call POST "/requests/$($c.id)/complete" operador-2 $null 200
Write-Host "Verificação concluída. ID para conferir após reiniciar: $id"
