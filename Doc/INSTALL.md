# Install Calendare Server <!-- omit in toc -->

- [Infrastructure requirements](#infrastructure-requirements)
  - [Database](#database)
  - [File storage](#file-storage)
    - [S3 Storage Provider](#s3-storage-provider)
    - [Local storage](#local-storage)
  - [DNS](#dns)
    - [Domain and subdomain](#domain-and-subdomain)
    - [Base Path Configuration](#base-path-configuration)
    - [Service Discovery (DNS Records)](#service-discovery-dns-records)
      - [SRV and TXT Records](#srv-and-txt-records)
- [Installation methods](#installation-methods)
  - [Kubernetes - Helm chart](#kubernetes---helm-chart)
    - [Network \& Security Considerations](#network--security-considerations)
      - [HTTP Methods](#http-methods)
      - [Content Types (MIME Types)](#content-types-mime-types)
      - [Request Body Size Limits](#request-body-size-limits)
  - [Others / Manual](#others--manual)

# Infrastructure requirements

The Calendare Server requires specific infrastructure to operate.

## Database

It uses a **PostgreSQL database** for data persistence and relies on an **OIDC provider** for user authentication. These components must be managed independently and are not covered in the following server installation guide.

The database contains user data and administrative data. The only exception relates to the WebDAV feature (file storage). The file contents are stored externally.

## File storage

File storage is only needed if the WebDAV feature is enabled (**not** for calendar or contact information). The default installation doesn't enable WebDAV.

### S3 Storage Provider

The **preferred** option is to use an external S3 provider (`provider: S3`). A dedicated bucket needs to be configured.

> [!WARNING]
> The Calendare Server requires a dedicated bucket. It will regulary **delete** all content which is not managed by the server.

### Local storage

The local storage (`provider: Filesystem`) requires a base path to the local file system. Using Kubernetes this means that you need to define `PersistentVolumes` and `VolumeMounts`.

While local storage is supported, it is primarily intended for development and testing. For production deployments, using an **S3-compatible provider** is highly recommended.

> [!NOTE]
> Furthermore, if you run a multi-instance (highly available) setup, you must ensure your storage backend is distributed (supporting `ReadWriteMany` access).
>
> In contrast, the **S3 provider option** is much easier to configure, scales seamlessly across multiple instances out-of-the-box, and eliminates the need to manage complex storage volumes in your cluster.

## DNS

### Domain and subdomain

A dedicated subdomain is required (e.g. `calendar.example.com`). The subdomain is used exclusively by the Calendare Server and its dependent services. Installing the server on a root domain (e.g., `example.com`) is only recommended if that domain is dedicated solely to the Calendare Server.

### Base Path Configuration

By default, the server's core functions are served behind a base path. This is configured during installation via the values.yaml file setting `calendare.pathBase`:

- **Default**/Historical: "/caldav.php" (Note: This is for legacy compatibility; PHP is not used).
- **Recommended**: "/caldav"
- **Reserved**: Do not use the reserved paths `/api`, `/admin`, `/ui` or any path starting with a dot (`.`) such as `.well-known`.

The final root URL in this example would be: `https://calendar.example.com/caldav`.

> [!WARNING]
> This final root URL is visible to all client applications. Changing this value after the initial setup will require all users to re-configure their calendar and contact clients.

### Service Discovery (DNS Records)

To assist client applications in automatically discovering your service, you should configure specific DNS records at your provider. These records follow [RFC 6764](https://datatracker.ietf.org/doc/html/rfc6764#section-3).

#### SRV and TXT Records

Since the use of **TLS (HTTPS)** is strongly recommended, you should define the following records (using our example domain and path):

| Record Type | Service Label    | Value                         |
| ----------- | ---------------- | ----------------------------- |
| SRV         | \_caldavs.\_tcp  | 0 1 443 calendar.example.com. |
| TXT         | \_caldavs.\_tcp  | path=/caldav                  |
| SRV         | \_carddavs.\_tcp | 0 1 443 calendar.example.com. |
| TXT         | \_carddavs.\_tcp | path=/caldav                  |

The server automatically handles redirects for `/.well-known/caldav` and `/.well-known/carddav` to your configured pathBase, ensuring compatibility with clients that use these standard discovery endpoints.

# Installation methods

## Kubernetes - Helm chart

A helm chart is provided at `oci://ghcr.io/closureoss/charts/calendare` or within the `deploy` subdirectory of the source repository. For a full list of configuration options, please refer to the [**README**](../deploy/README.md) included with the chart.

```shell
helm upgrade --install calendare oci://ghcr.io/closureoss/charts/calendare -f values.yaml --namespace calendare
```

Check values with

```shell
helm show values oci://ghcr.io/closureoss/charts/calendare
```

### Network & Security Considerations

To serve calendar and contact data to client applications, the Calendare Server requires public network access. The Helm chart supports both **Ingress** and **HTTPRoute** (Kubernetes Gateway API) resources; however, your cluster must already have the necessary infrastructure (such as an Ingress Controller or Gateway) to support them. While not covered in this guide, we strongly recommend using a **Web Application Firewall (WAF)** for enhanced security.

When configuring public network access (even without a WAF), ensure your network infrastructure supports the specific requirements of the **CalDAV** and **CardDAV** protocols. While these protocols are based on HTTP, they utilize several specialized HTTP methods (verbs) that are often blocked by default security policies.

#### HTTP Methods

To ensure full functionality, your proxy, load balancer, or firewall must allow the following methods: PROPFIND, REPORT, MKCALENDAR, MKCOL, PROPPATCH, MOVE, ACL, and DELETE.

As a reference (unsupported), here is an example of a **ModSecurity** configuration to allow these methods:

```yaml
SecAction "id:900200,phase:1,nolog,pass,t:none,setvar:\'tx.allowed_methods=GET HEAD POST PUT OPTIONS PROPFIND DELETE REPORT MOVE PROPPATCH MKCOL MKCALENDAR ACL\'"
```

#### Content Types (MIME Types)

Similarly, ensure that your environment does not filter or block the specific MIME types used by calendar and contact protocols, such as:

- text/calendar (iCalendar)
- text/vcard (vCard)
- application/xml or text/xml (WebDAV responses)

#### Request Body Size Limits

Ensure that your ingress controller, proxy, or web server is configured to allow larger HTTP request bodies. Providing a universal "one-size-fits-all" limit is difficult, as CalDAV and CardDAV payloads can be quite large.
The server has for CalDAV and CardDAV requests an internal limit of 30'000'000 bytes (~ 28.6 MB). Currently this limit is fixed and not configurable.

Individual calendar events with long descriptions or attachments or contact cards containing high-resolution profile photos (vCards) can significantly increase the request size. We recommend starting with a limit of at least 10MB to 20MB and adjusting based on your specific use case.

As a reference (unsupported) if you are using Nginx as an Ingress controller, you might include in your `values.yaml`:

```yaml
ingress:
  annotations:
    nginx.ingress.kubernetes.io/proxy-body-size: "10m"
```

If you enable WebDAV take care to revisit the body size limit. The server removes for WebDAV requests any internal body size limit.

## Others / Manual

The Calendar Server is built with **.NET 10** and is compatible with various platforms. Although native installation is possible, the only officially supported environment is the provided **Alpine-based OCI container**.

Users opting for a non-containerized setup do so at their own risk. Note that **macOS** has known issues with the used encryption methods.
