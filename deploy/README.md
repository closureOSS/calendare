# Helm chart for the Calendare Server <!-- omit in toc -->



![Version: 0.6.0](https://img.shields.io/badge/Version-0.6.0-informational?style=flat-square) ![Type: application](https://img.shields.io/badge/Type-application-informational?style=flat-square) ![AppVersion: 0.6.0](https://img.shields.io/badge/AppVersion-0.6.0-informational?style=flat-square) 

[Calendare - A Caldav/Carddav server](https://github.com/closureOSS/calendare)

> [!IMPORTANT]
> Please read the [server installation guide](../doc/INSTALL.md) first.

# Calendare Configuration

## Initial Configuration

| Key                                 | Type   | Default                | Description                    |
| ----------------------------------- | ------ | ---------------------- | ------------------------------ |
| calendare.pathBase                  | string | `/caldav.php`          | See installation guide         |
| calendare.administrator             | object | `{}`                   | ...                            |
| calendare.administrator.password    | string | `<random>`             | Initial administrator password |
| calendare.administrator.username    | string | `admin`                | Username                       |
| calendare.administrator.email       | string | `calendare@closure.ch` | Email of administrator         |
| calendare.administrator.displayName | string | `Administrator`        | Visible name                   |

The settings for the administrator are only used during the first initial start of the server as long no administrator account exists in the database.

If no initial password is provided the helm chart generates a password. The **initial** password is stored in the secret `[[deployment-name]]-admin`, e.g. `calendare-admin`

> [!NOTE]
> Change administrator settings later in the admin UI. The `username` is immutable. Changes to the admin password aren't reflected in the secret.

## PostgreSQL Database

| Key                          | Type   | Default | Description             |
| ---------------------------- | ------ | ------- | ----------------------- |
| calendare.existingDbmsSecret | string |         | Name of existing secret |

The connection information for the PostgreSQL database need to be provided in a Kubernetes secret. Following keys are supported:

| Key              | Description                            |
| ---------------- | -------------------------------------- |
| ConnectionString | Connection string in NET10 convention  |
| User             | Username. Defaults to app              |
| Password         | Password                               |
| Host             | Host. Defaults to calendare-cluster-rw |
| Port             | Port e.g. 5432                         |
| Dbname           | Name of database. Defaults to app      |

The table represents the priority (User overwrites the corresponding value in ConnectionString).
The default ConnectionString is `Host=calendare-cluster-rw;Username=app;Database=app;`.

> [!TIP]
> The secret created by [CloudNativePG](https://cloudnative-pg.io/) for a PostgreSQL in the same namespace as the application fullfils the criteria.

## OIDC Provider Configuration

While the **Calendare Server** does not use JWT (JSON Web Tokens) or OIDC for authenticating standard calendar and contact clients (which typically use Basic or Digest authentication), an OIDC provider is a mandatory infrastructure requirement for the User Self-Administration UI.

The administration interface relies on OIDC to provide a secure, modern login experience for users managing their accounts, passwords, and service settings.

| Key                 | Type   | Default        | Description                                               |
| ------------------- | ------ | -------------- | --------------------------------------------------------- |
| jwtBearer           | {}     |                | OIDC provider configuration                               |
| jwtBearer.enabled   | bool   | `false`        | OIDC provider configuration                               |
| jwtBearer.provider  | string | `Default`      | Provider, use one of Default, PocketId, Keycloak, Zitadel |
| jwtBearer.authority | uri    | ``             | URI of OIDC provider, e.g. `https://auth.example.com`     |
| jwtBearer.audience  | string | `calendare.ui` | Audience                                                  |

## Webpush for Notifications Configuration

The Calendare Server supports the [specification for WebDAV Push](https://github.com/bitfireAT/webdav-push), for [HTTP Push](https://datatracker.ietf.org/doc/html/rfc8030), [Message encryption for Web Push](https://datatracker.ietf.org/doc/html/rfc8291), [VAPID keys](https://datatracker.ietf.org/doc/html/rfc8292).

To enable the Web push VAPID keys must be supplied. Either with an existing secret

| Key                    | Type | Default | Description                                                                        |
| ---------------------- | ---- | ------- | ---------------------------------------------------------------------------------- |
| webpush.existingSecret | name | ``      | Name of existing secret which contains two keys named `PUBLICKEY` and `PRIVATEKEY` |

Or include the VAPID keys directly in `values.yaml` with:

| Key                   | Type   | Default | Description       |
| --------------------- | ------ | ------- | ----------------- |
| webpush.vapid         | object | `{}`    |                   |
| webpush.vapid.public  | name   | ``      | Public VAPID key  |
| webpush.vapid.private | name   | ``      | Private VAPID key |

## Calendare User Defaults Configuration

| Key                                   | Type   | Default         | Description                              |
| ------------------------------------- | ------ | --------------- | ---------------------------------------- |
| calendare.userDefaults                | object | `{}`            | ...                                      |
| calendare.userDefaults.Locale         | string | `de-CH`         | Users locale setting                     |
| calendare.userDefaults.DateFormatType | string | `E`             | Default date format (currently not used) |
| calendare.userDefaults.TzId           | string | `Europe/Zurich` | Default timezone                         |

These defaults are only used if no value is supplied by the user during account creation. The user can change these values later.

## Calendar Clients Feature Configuration

| Key                | Type  | Default | Description |
| ------------------ | ----- | ------- | ----------- |
| calendare.features | array | `[]`    | ...         |

A list of feature toogles for calendar clients can be supplied. A calendare client is identified by

| Label         | Description                                         |
| ------------- | --------------------------------------------------- |
| Default       | Any client, features always applied                 |
| Thunderbird   | Mozilla Thunderbird calendar client                 |
| MacOSCalendar | Apple MacOS calendar client                         |
| EMClient      | eM Email Client                                     |
| DAVx5         | Android DAVx5 CalDAV / CardDAV / WebDAV for Android |
| NotDetected   | Unknown client                                      |

each client can have a list of enabled features and a second list of disabled features. These features are

| Label                           | Description                                                                                                                 |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| CalendarProxy                   | Calendar proxy (Apple CalendarServer extension)                                                                             |
| VirtualProxyMembers             | Adds members to the proxy group read or read/write based on granted privileges.                                             |
| ResourceSharing                 | Resource sharing (Apple extension)                                                                                          |
| AutoScheduling                  | Server scheduling (**Do not enable**, still under development)                                                              |
| WebdavPush                      | WebDAV Push                                                                                                                 |
| VCard4                          | Allow vCard 4 formatted addresses                                                                                           |
| SyncCollectionSuppressTokenGone | Ignore invalid sync tokens and return all changes (similar to empty token), doesn't send a GONE status as would be required |

The recommended setup for `calendare.feature` is

```yaml
- ClientType: Default
    Enable:
    - CalendarProxy
    # - AutoScheduling
    - SyncCollectionSuppressTokenGone
- ClientType: MacOSCalendar
    Enable:
    - ResourceSharing
    - VirtualProxyMembers
```

Disabled features have a higher priority than enabled features.

If a client is not configured in `calendare.features` the in-built settings are applied, which are taken from the `appsettings.json` file of the server.

## Testing and Debugging Configuration

| Key                        | Type   | Default | Description                                                                                                                                |
| -------------------------- | ------ | ------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| calendare.recorder         | object |         | Record all transactions. The recording contains private information and should not be enabled on a production system without user consent. |
| calendare.recorder.enabled | bool   | false   | Disabled by default (records to the database)                                                                                              |
| calendare.enableTestMode   | bool   | `false` | Test mode allows the completely clear the database and other unsafe API calls. **Never** enable on a production system                     |

> [!WARNING]
> The test mode is **only** for integration testing on a dedicated database. With enabled test mode the integration tests can and will delete the whole content without any further confirmation.

> [!NOTE]
> The recorder can be used to debug the communication with client applications. The whole content of the request and response is stored in a database table. The recorder is not dependent on the test mode.

# Full Configuration

## Values

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| affinity | object | `{}` |  |
| calendare | object | `{}` | Calendare Server configuration |
| fullnameOverride | string | `""` |  |
| httpRoute | object | {} | Expose the service via gateway-api HTTPRoute Requires Gateway API resources and suitable controller installed within the cluster (see: https://gateway-api.sigs.k8s.io/guides/) |
| httpRoute.enabled | bool | `false` | use either ingress or HTTPRoute (gateway API) |
| httpRoute.hostnames | list | `["chart-example.local"]` | Hostnames matching HTTP header. |
| httpRoute.parentRefs | list | {} | Which Gateways this Route is attached to. |
| httpRoute.rules | list | {} | List of rules and filters applied. |
| image.pullPolicy | string | `"IfNotPresent"` | This sets the pull policy for images. |
| image.repository | string | `"ghcr.io/closureoss/calendare/calendare.server"` |  |
| image.tag | string | `""` | Overrides the image tag whose default is the chart appVersion. |
| imagePullSecrets | list | `[]` |  |
| ingress.annotations | object | `{}` |  |
| ingress.className | string | `""` |  |
| ingress.enabled | bool | `false` | use either ingress or HTTPRoute (gateway API) |
| ingress.hosts[0].host | string | `"chart-example.local"` |  |
| ingress.hosts[0].paths[0].path | string | `"/"` |  |
| ingress.hosts[0].paths[0].pathType | string | `"ImplementationSpecific"` |  |
| ingress.tls | list | `[]` |  |
| jwtBearer | object | `{}` | OIDC provider configuration |
| livenessProbe.httpGet.path | string | `"/health"` |  |
| livenessProbe.httpGet.port | string | `"metric"` |  |
| metric.port | int | `5001` |  |
| migration | object | {} | Database schema migration during installation (use with caution) |
| migration.enabled | bool | `false` | Enable automatic schema migration |
| migration.image | object | {} | Image for migration program |
| nameOverride | string | `""` |  |
| nodeSelector | object | `{}` |  |
| podAnnotations | object | `{}` |  |
| podLabels | object | `{}` |  |
| podSecurityContext | object | `{}` |  |
| readinessProbe.httpGet.path | string | `"/health"` |  |
| readinessProbe.httpGet.port | string | `"metric"` |  |
| replicaCount | int | `1` |  |
| resources | object | `{}` |  |
| securityContext | object | `{}` |  |
| service.port | int | `8080` |  |
| service.type | string | `"ClusterIP"` |  |
| serviceAccount.annotations | object | `{}` |  |
| serviceAccount.automount | bool | `true` |  |
| serviceAccount.create | bool | `true` |  |
| serviceAccount.name | string | `""` |  |
| tolerations | list | `[]` |  |
| volumeMounts | list | `[]` |  |
| volumes | list | `[]` |  |
| webpush | object | `{}` | WebPush configuration |



## Source Code

* <https://github.com/closureOSS/calendare/src/deploy>
* <https://github.com/closureOSS/calendare/src/Server>


----------------------------------------------
Autogenerated from chart metadata using [helm-docs v1.14.2](https://github.com/norwoodj/helm-docs/releases/v1.14.2)
