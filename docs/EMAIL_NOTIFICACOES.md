# Notificações por e-mail

O RentalHub envia um digest operacional diário por e-mail quando a rotina estiver habilitada.

O digest usa as mesmas regras da central de notificações do sistema:

- novas reservas recentes;
- check-ins próximos;
- check-outs próximos;
- limpezas pendentes ou em andamento;
- manutenções abertas ou em andamento;
- repasses pendentes ou parcialmente pagos.

## Destinatários

Por tenant, o sistema envia para:

- `EmailOperacional` cadastrado na empresa, quando existir;
- usuários ativos do tipo `Administrador`.

O envio é registrado em `EmailNotificationLogs`, evitando duplicidade no mesmo dia para o mesmo destinatário.

## Variáveis de ambiente

No Coolify, configure na API:

```env
Notifications__Email__Enabled=true
Notifications__Email__DigestHour=8
Notifications__Email__IntervalMinutes=60
Notifications__Email__HorizonDays=3
Notifications__Email__NewReservationHours=24
Notifications__Email__TimeZone=America/Sao_Paulo
Notifications__Email__SendToOperationalEmail=true
Notifications__Email__SendToAdmins=true
Notifications__Email__AdminUrl=https://rentalhub.malachdigital.com.br

Smtp__Host=smtp.seu-provedor.com
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__From=notificacoes@seudominio.com.br
Smtp__FromName=RentalHub
Smtp__Username=notificacoes@seudominio.com.br
Smtp__Password=sua-senha-smtp
```

## Observações

- Com `Notifications__Email__Enabled=false`, nenhum digest é enviado.
- O horário considera `Notifications__Email__TimeZone`.
- Se o SMTP não estiver configurado, o sistema registra log e ignora o envio.
