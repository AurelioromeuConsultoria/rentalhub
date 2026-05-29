# Sprint 1 - Execução

## Entregue

- Entidades base de segurança:
  - `TenantDomain`.
  - `Usuario`.
  - `PerfilAcesso`.
  - `PerfilAcessoPermissao`.
  - `AuditLog`.
- Enum `TipoUsuario`.
- Catálogo inicial de recursos/permissões.
- `ITenantEntity`.
- Filtros globais por `TenantId` no `RentalHubDbContext`.
- Seed inicial:
  - Tenant `rentalhub`.
  - Perfil `Administrador`.
  - Permissões completas.
  - Usuário `admin@rentalhub.com`.
- JWT bearer authentication.
- Refresh token.
- Hash de senha com PBKDF2.
- Contexto HTTP de tenant:
  - Tenant pelo token.
  - Override por `X-Tenant-Id` para platform admin.
- Controllers:
  - `AuthController`.
  - `TenantsController`.
  - `UsuariosController`.
  - `PerfisAcessoController`.
- Inicialização automática de schema em desenvolvimento com `EnsureCreated`.
- Admin:
  - Visual aproximado ao padrão do AppIgreja.
  - Sidebar escura com grupos.
  - Header compacto com breadcrumbs e ações.
  - Login visual no padrão split do AppIgreja.
  - `AuthContext`.
  - Rota protegida.
  - Axios com token, refresh token e tenant override.

## Credenciais Iniciais

```txt
E-mail: admin@rentalhub.com
Senha: RentalHub@2026
```

## Validações Executadas

API:

```txt
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Resultado:

```txt
Build: sucesso
Testes: 1 aprovado, 0 falhas
```

Admin:

```txt
npm run lint
npm run build
```

Resultado:

```txt
Lint: sucesso, com 1 warning de Fast Refresh no AuthContext
Build: sucesso
```

## Pendência De Ambiente

A API está rodando em `http://localhost:5015`. O banco do painel publica a porta no IP do servidor, seguindo o mesmo padrão do AppIgreja:

```txt
Host=<host-publico-ou-local>;Port=<porta-publicada>
```

Se a API for configurada com `localhost:5435`, o login retorna `503` e o health check retorna:

```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "database",
      "status": "Unhealthy",
      "description": "PostgreSQL connection is unavailable."
    }
  ]
}
```

Assim que a porta do PostgreSQL estiver realmente acessível pelo host local, a API cria o schema automaticamente na inicialização e o login inicial passa a funcionar.
